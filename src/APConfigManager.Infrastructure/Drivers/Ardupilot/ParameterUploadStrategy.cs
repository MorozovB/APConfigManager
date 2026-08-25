using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using Microsoft.Extensions.Logging;

namespace APConfigManager.Infrastructure.Drivers.Ardupilot;

/// <summary>
/// Default multi-pass parameter upload strategy for ArduPilot devices.
/// Resets the device to defaults, writes only the parameters that differ, reboots
/// between passes so dependent parameters can settle, force-writes a small
/// mandatory set, applies deferred parameters last, and finally verifies the
/// on-device state. Behaviour matches the logic previously embedded in the driver.
/// </summary>
public class ParameterUploadStrategy : IParameterUploadStrategy
{
    private const int WriteParamsPasses = 6;

    private static readonly HashSet<string> ReadOnlyPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "STAT_",
        "INS_GYR1_CALTEMP",
        "INS_GYR2_CALTEMP",
        "INS_GYR3_CALTEMP",
        "INS4_GYR_CALTEMP",
        "INS5_GYR_CALTEMP",
        "INS_ACC1_CALTEMP",
        "INS_ACC2_CALTEMP",
        "INS_ACC3_CALTEMP",
        "INS4_ACC_CALTEMP",
        "INS5_ACC_CALTEMP",
    };

    private static readonly HashSet<string> AutoCalculatedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BARO1_GND_PRESS",
        "BARO2_GND_PRESS",
        "BARO3_GND_PRESS",
        "BARO1_WCF_",
        "BARO2_WCF_",
        "SR0_EXT_STAT",
        "SR0_EXTRA1",
        "SR0_EXTRA2",
        "SR0_EXTRA3",
        "SR0_RAW_SENS",
        "SR0_RC_CHAN",
    };

    private static readonly string[] FinalForceParams =
    {
        "COMPASS_EXTERNAL",
    };

    private static readonly string[] DeferredParams =
    {
        "ARMING_REQUIRE",
    };

    private static readonly string[] PriorityPrefixes =
    {
        "CAN_D1_PROTOCOL",
        "CAN_D2_PROTOCOL",
        "CAN_P1_DRIVER",
        "CAN_P2_DRIVER",
        "GPS_TYPE",
        "SERIAL",
        "BRD_",
    };

    private readonly ILogger<ParameterUploadStrategy> logger;

    public ParameterUploadStrategy(ILogger<ParameterUploadStrategy> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Writes the given parameters to the device with multi-pass retry and final
    /// verification. The device must already be connected and in normal mode.
    /// </summary>
    public async Task<ParameterUploadResult> UploadAsync(
        ParameterUploadContext context,
        IReadOnlyList<Parameter> parameters,
        IProgress<(int current, int total)> progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(progress);

        var telemetry = context.Telemetry;
        var port = context.Port;

        var sent = 0;
        var skippedSame = 0;

        ParameterUploadResult Fail(string message) => new()
        {
            Success = false,
            Sent = sent,
            Failed = 0,
            Total = parameters.Count,
            ErrorMessage = message
        };

        if (!await telemetry.AreCoreSensorsHealthyAsync(5000, ct))
        {
            logger.LogError("Core sensors not healthy after upload — board appears faulty");

            return Fail("The board is connected, but the sensors are not working (zero readings in the GCS)—a memory or sensor failure is likely. Success has not been confirmed.");
        }

        try
        {
            logger.LogInformation("Resetting parameters to defaults before upload...");

            _ = await telemetry.ResetParamsAsync(ct);
            await Task.Delay(1000, ct);
            await telemetry.RebootNormalAsync(ct);
            port.Close();
            await context.ReconnectAfterReboot(ct);

            var deviceParams = await telemetry.RequestAllParamsAsync(ct);

            if (deviceParams.Count == 0)
            {
                logger.LogError("Parameter read returned 0 — storage controller not responding");

                return Fail("Unable to read parameters from the board—the storage controller is not responding. The write operation has been interrupted.");
            }

            // Build a dictionary of device parameters for quick lookup by name.
            var deviceMap = deviceParams
                 .GroupBy(p => p.Name)
                 .ToDictionary(g => g.Key, g => g.Last().Value);

            // Build a dictionary of device parameter types for quick lookup by name.
            var deviceTypeMap = deviceParams
                .GroupBy(p => p.Name)
                .ToDictionary(g => g.Key, g => g.Last().ParamType);

            logger.LogDebug("Device has {Count} parameters", deviceMap.Count);

            var toUpload = new List<Parameter>();
            var missing = new List<Parameter>();
            var skippedReadOnly = 0;
            var skippedAutoCalc = 0;

            foreach (var param in parameters)
            {
                // Skip read-only params (STAT_*, calibration temps)
                if (IsReadOnly(param.Name))
                {
                    skippedReadOnly++;
                    continue;
                }

                // Skip auto-calculated params (BARO*_GND_PRESS)
                if (IsAutoCalculated(param.Name))
                {
                    skippedAutoCalc++;
                    continue;
                }

                // Skip deferred params (ARMING_REQUIRE, etc.) — these are applied after all others
                if (DeferredParams.Any(d => param.Name.Equals(d, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!deviceMap.TryGetValue(param.Name, out var deviceValue))
                {
                    missing.Add(param);
                    continue;
                }

                if (AreParamsEqual(deviceValue, param.Value))
                {
                    skippedSame++;
                    continue;
                }

                // Apply device param type to file param (file doesn't have type info)
                var typedParam = new Parameter
                {
                    Name = param.Name,
                    Value = param.Value,
                    ParamType = deviceTypeMap.TryGetValue(param.Name, out var ptype)
                        ? ptype
                        : param.ParamType
                };

                toUpload.Add(typedParam);
            }

            if (toUpload.Count == 0 && missing.Count == 0)
            {
                progress.Report((parameters.Count, parameters.Count));
                return new ParameterUploadResult
                {
                    Success = true,
                    Sent = 0,
                    Failed = 0,
                    Hidden = missing.Count,
                    Total = parameters.Count
                };
            }

            var pending = new List<Parameter>(toUpload);
            var previousPendingCount = -1;
            toUpload.Sort((a, b) => GetParamPriority(a.Name).CompareTo(GetParamPriority(b.Name)));

            for (var pass = 1; pass <= WriteParamsPasses && pending.Count > 0; pass++)
            {
                // No progress since last pass — stop wasting time
                if (pending.Count == previousPendingCount)
                {
                    logger.LogWarning("No progress after pass {Pass}, stopping", pass - 1);

                    break;
                }
                previousPendingCount = pending.Count;

                logger.LogDebug("Pass {Pass}: {Count} parameters", pass, pending.Count);

                var failed = new List<Parameter>();

                foreach (var param in pending)
                {
                    ct.ThrowIfCancellationRequested();

                    var confirmed = await telemetry.SetParamAsync(param, ct);

                    if (confirmed)
                    {
                        sent++;
                    }
                    else
                    {
                        failed.Add(param);
                    }

                    progress.Report((sent + skippedSame, parameters.Count));

                    await Task.Delay(50, ct);
                }

                pending = failed;

                if (pending.Count == 0 && missing.Count == 0)
                {
                    break;
                }

                // Reboot to apply params (some depend on others)
                if (pass < WriteParamsPasses && (pending.Count > 0 || (pass == 1 && missing.Count > 0)))
                {
                    logger.LogDebug("Rebooting to apply parameters...");

                    await telemetry.RebootNormalAsync(ct);
                    port.Close();
                    await context.ReconnectAfterReboot(ct);

                    // Re-read params after reboot
                    deviceParams = await telemetry.RequestAllParamsAsync(ct);

                    deviceMap = deviceParams
                         .GroupBy(p => p.Name)
                         .ToDictionary(g => g.Key, g => g.Last().Value);

                    deviceTypeMap = deviceParams
                        .GroupBy(p => p.Name)
                        .ToDictionary(g => g.Key, g => g.Last().ParamType);

                    // Check if pending params applied after reboot
                    var stillPending = new List<Parameter>();

                    foreach (var param in pending)
                    {
                        if (deviceMap.TryGetValue(param.Name, out var val)
                            && Math.Abs(val - param.Value) < 0.001f)
                        {
                            sent++;
                        }
                        else
                        {
                            stillPending.Add(param);
                        }
                    }

                    pending = stillPending;

                    // Check missing — some might now exist after reboot
                    if (pass == 1 && missing.Count > 0)
                    {
                        var nowExists = new List<Parameter>();
                        var stillMissing = new List<Parameter>();

                        foreach (var param in missing)
                        {
                            if (deviceMap.ContainsKey(param.Name))
                            {
                                // Check if value already matches
                                if (Math.Abs(deviceMap[param.Name] - param.Value) < 0.001f)
                                {
                                    skippedSame++;
                                }
                                else
                                {
                                    nowExists.Add(param);
                                }
                            }
                            else
                            {
                                stillMissing.Add(param);
                            }
                        }

                        missing = stillMissing;
                        pending.AddRange(nowExists);
                    }

                }
            }

            // Force-write params that get overwritten by device after reboots
            var forceList = parameters
                .Where(p => FinalForceParams.Any(f =>
                    p.Name.Equals(f, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (forceList.Count > 0)
            {
                logger.LogInformation("Force-writing {Count} mandatory parameters...", forceList.Count);

                foreach (var param in forceList)
                {
                    ct.ThrowIfCancellationRequested();

                    var typedParam = new Parameter
                    {
                        Name = param.Name,
                        Value = param.Value,
                        ParamType = deviceTypeMap.TryGetValue(param.Name, out var pt) ? pt : param.ParamType
                    };

                    var ok = await telemetry.SetParamAsync(typedParam, ct);

                    await Task.Delay(30, ct);
                }
            }

            try
            {
                await telemetry.RebootNormalAsync(ct);
                port.Close();
                await context.ReconnectAfterReboot(ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Final reboot failed");
            }

            // Write deferred params last (ARMING_REQUIRE etc.)
            var deferredList = parameters
                .Where(p => DeferredParams.Any(d =>
                    p.Name.Equals(d, StringComparison.OrdinalIgnoreCase)))
                .Where(p => deviceTypeMap.ContainsKey(p.Name))
                .ToList();

            if (deferredList.Count > 0)
            {
                foreach (var param in deferredList)
                {
                    ct.ThrowIfCancellationRequested();

                    var typedParam = new Parameter
                    {
                        Name = param.Name,
                        Value = param.Value,
                        ParamType = deviceTypeMap.TryGetValue(param.Name, out var pt) ? pt : param.ParamType
                    };

                    var ok = await telemetry.SetParamAsync(typedParam, ct);

                    if (ok)
                    {
                        sent++;
                    }
                    else
                    {
                        pending.Add(param);
                    }

                    progress.Report((sent + skippedSame, parameters.Count));
                    await Task.Delay(30, ct);
                }
            }

            // Final verification: read actual state from device
            var verifyParams = await telemetry.RequestAllParamsAsync(ct);
            var verifyMap = verifyParams
                .GroupBy(p => p.Name)
                .ToDictionary(g => g.Key, g => g.Last().Value);

            var realFailed = 0;
            foreach (var param in parameters)
            {
                if (IsReadOnly(param.Name) || IsAutoCalculated(param.Name))
                    continue;

                if (DeferredParams.Any(d => param.Name.Equals(d, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (FinalForceParams.Any(f => param.Name.Equals(f, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (!verifyMap.TryGetValue(param.Name, out var actualValue))
                    continue;

                if (!AreParamsEqual(actualValue, param.Value))
                {
                    realFailed++;
                }
            }

            return new ParameterUploadResult
            {
                Success = realFailed == 0,
                Sent = sent,
                Failed = realFailed,
                Hidden = missing.Count,
                ReadOnly = skippedReadOnly + skippedAutoCalc,
                Total = parameters.Count
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ParameterUploadResult
            {
                Success = false,
                Sent = sent,
                Failed = parameters.Count - sent - skippedSame,
                Total = parameters.Count,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Checks if a parameter name is considered read-only based on known prefixes.
    /// </summary>
    private static bool IsReadOnly(string paramName)
    {
        if (ReadOnlyPrefixes.Contains(paramName))
        {
            return true;
        }

        foreach (var prefix in ReadOnlyPrefixes)
        {
            if (prefix.EndsWith("_") && paramName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a parameter name is considered auto-calculated based on known prefixes.
    /// </summary>
    private static bool IsAutoCalculated(string paramName)
    {
        if (AutoCalculatedPrefixes.Contains(paramName))
        {
            return true;
        }

        foreach (var prefix in AutoCalculatedPrefixes)
        {
            if (prefix.EndsWith("_") && paramName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the priority of a parameter based on its name prefix.
    /// </summary>
    private static int GetParamPriority(string name)
    {
        for (var i = 0; i < PriorityPrefixes.Length; i++)
        {
            if (name.StartsWith(PriorityPrefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 100;
    }

    /// <summary>
    /// Compares two float values for equality with a tolerance to account for floating-point precision issues.
    /// </summary>
    private static bool AreParamsEqual(float a, float b)
    {
        if (a == b) return true;

        var diff = Math.Abs(a - b);

        // Additional figure for small values to avoid false negatives due to floating-point precision
        if (Math.Abs(a) < 1f && Math.Abs(b) < 1f)
        {
            return diff < 0.00001f;
        }

        // To big values, use relative comparison
        var max = Math.Max(Math.Abs(a), Math.Abs(b));

        return diff / max < 0.00001f;
    }
}
