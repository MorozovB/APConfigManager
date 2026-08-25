using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;
using Microsoft.Extensions.Logging;
using static MAVLink;

namespace APConfigManager.Infrastructure.Drivers.Ardupilot;

/// <summary>
/// MAVLink v1 protocol implementation for ArduPilot telemetry and commands.
/// </summary>
public class MavLinkProtocol : ITelemetryProtocol
{
    private readonly ISerialPortAdapter port;
    private readonly MavlinkParse parser;
    private readonly ILogger<MavLinkProtocol> logger;

    private const int HeartbeatPreambleDelayMs = 500;

    /// <summary>
    /// Initializes the MAVLink protocol with a serial port adapter.
    /// </summary>
    public MavLinkProtocol(ISerialPortAdapter port, ILogger<MavLinkProtocol> logger)
    {
        this.port = port;
        this.logger = logger;
        parser = new MavlinkParse();
    }

    // ─── Private helpers (shared building blocks) ────────────────────────────

    /// <summary>Builds and writes any MAVLink packet to the port.</summary>
    private async Task SendPacketAsync(MAVLINK_MSG_ID msgId, object message, CancellationToken ct)
    {
        var packet = parser.GenerateMAVLinkPacket20(
            msgId,
            message,
            false,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        await port.WriteAsync(packet, 0, packet.Length, ct);
    }

    /// <summary>
    /// Sends the 3-heartbeat preamble so the autopilot routes ACKs/replies back to us
    /// (it ignores commands without a recent GCS heartbeat stream).
    /// </summary>
    private async Task EstablishGcsPresenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 3; i++)
        {
            await SendHeartbeatAsync(ct);
            await Task.Delay(HeartbeatPreambleDelayMs, ct);
        }
    }

    /// <summary>Builds and sends a COMMAND_LONG (target is always 1/1).</summary>
    private Task SendCommandAsync(
        ushort command,
        CancellationToken ct,
        float param1 = 0, float param2 = 0, float param3 = 0, float param4 = 0,
        float param5 = 0, float param6 = 0, float param7 = 0)
    {
        var cmd = new mavlink_command_long_t
        {
            target_system = 1,
            target_component = 1,
            command = command,
            confirmation = 0,
            param1 = param1,
            param2 = param2,
            param3 = param3,
            param4 = param4,
            param5 = param5,
            param6 = param6,
            param7 = param7
        };

        return SendPacketAsync(MAVLINK_MSG_ID.COMMAND_LONG, cmd, ct);
    }

    /// <summary>
    /// Sends a COMMAND_LONG and waits for the COMMAND_ACK. Returns the ACK, or null on timeout.
    /// The caller decides how to interpret command/result.
    /// </summary>
    private async Task<mavlink_command_ack_t?> SendCommandAndWaitAckAsync(
        ushort command,
        int timeoutMs,
        CancellationToken ct,
        float param1 = 0, float param2 = 0, float param3 = 0, float param4 = 0,
        float param5 = 0, float param6 = 0, float param7 = 0)
    {
        await SendCommandAsync(command, ct, param1, param2, param3, param4, param5, param6, param7);

        var ackMsg = await WaitForMessageAsync(MAVLINK_MSG_ID.COMMAND_ACK, timeoutMs, ct);
        if (ackMsg is null)
        {
            return null;
        }

        return (mavlink_command_ack_t)ackMsg.data;
    }

    /// <summary>Establishes presence, requests a specific message via REQUEST_MESSAGE, and waits for it.</summary>
    private async Task<MAVLinkMessage?> RequestMessageAsync(MAVLINK_MSG_ID msgId, int timeoutMs, CancellationToken ct)
    {
        await EstablishGcsPresenceAsync(ct);
        await SendCommandAsync((ushort)MAV_CMD.REQUEST_MESSAGE, ct, param1: (uint)msgId);
        return await WaitForMessageAsync(msgId, timeoutMs, ct);
    }

    // ─── Public protocol methods ─────────────────────────────────────────────

    /// <summary>
    /// Sends a MAVLink HEARTBEAT message to maintain connection with the autopilot.
    /// </summary>
    public async Task SendHeartbeatAsync(CancellationToken ct)
    {
        var heartbeat = new mavlink_heartbeat_t
        {
            type = (byte)MAV_TYPE.GCS,
            autopilot = (byte)MAV_AUTOPILOT.INVALID,
            base_mode = 0,
            system_status = (byte)MAV_STATE.ACTIVE,
            mavlink_version = 3
        };

        await SendPacketAsync(MAVLINK_MSG_ID.HEARTBEAT, heartbeat, ct);
    }

    /// <summary>
    /// Sends MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246) to reboot into bootloader.
    /// </summary>
    public Task RebootToBootloaderAsync(CancellationToken ct)
        => SendCommandAsync((ushort)MAV_CMD.PREFLIGHT_REBOOT_SHUTDOWN, ct, param1: 3);

    /// <summary>
    /// Requests all parameters from the autopilot via PARAM_REQUEST_LIST.
    /// </summary>
    public async Task<List<Parameter>> RequestAllParamsAsync(CancellationToken ct)
    {
        // Establish GCS presence — autopilot ignores commands without heartbeat stream
        await EstablishGcsPresenceAsync(ct);

        var request = new mavlink_param_request_list_t
        {
            target_system = 1,
            target_component = 1
        };

        // Generated once and re-sent in the retry loop (keeps the same sequence bytes on the wire).
        var packet = parser.GenerateMAVLinkPacket20(
            MAVLINK_MSG_ID.PARAM_REQUEST_LIST,
            request,
            false,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        var parameters = new List<Parameter>();
        var totalExpected = -1;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            parameters.Clear();
            totalExpected = -1;

            await port.WriteAsync(packet, 0, packet.Length, ct);
            logger.LogDebug("RequestAllParams: attempt {Attempt}, sent PARAM_REQUEST_LIST", attempt);

            // Time-based deadline instead of consecutive-null counter.
            // Right after boot the autopilot sends many non-PARAM_VALUE packets
            // (heartbeat, statustext, system_time) which were incorrectly
            // eating through the null budget and breaking the loop early.
            var idleDeadline = DateTime.UtcNow.AddSeconds(4); // reset on each received param
            var absoluteLimit = DateTime.UtcNow.AddSeconds(60);

            while (DateTime.UtcNow < absoluteLimit)
            {
                ct.ThrowIfCancellationRequested();

                // Idle too long with no new params — device stopped sending
                if (DateTime.UtcNow > idleDeadline)
                {
                    logger.LogDebug("RequestAllParams: idle timeout ({Got}/{Expected})", parameters.Count, totalExpected);
                    break;
                }

                var msg = await ReadMessageAsync(ct);

                if (msg?.data is null)
                {
                    continue;
                }

                // Keep heartbeat stream alive during long param reads.
                if (msg.msgid == (uint)MAVLINK_MSG_ID.HEARTBEAT)
                {
                    await SendHeartbeatAsync(ct);
                    continue;
                }

                if (msg.msgid != (uint)MAVLINK_MSG_ID.PARAM_VALUE)
                {
                    continue;
                }

                var paramValue = (mavlink_param_value_t)msg.data;
                var name = System.Text.Encoding.ASCII
                    .GetString(paramValue.param_id)
                    .TrimEnd('\0');

                parameters.Add(new Parameter
                {
                    Name = name,
                    Value = paramValue.param_value,
                    ParamType = paramValue.param_type
                });

                totalExpected = paramValue.param_count;
                idleDeadline = DateTime.UtcNow.AddSeconds(4); // reset idle window

                if (parameters.Count >= totalExpected)
                {
                    logger.LogDebug("RequestAllParams: complete ({Got}/{Expected})", parameters.Count, totalExpected);
                    break;
                }
            }

            logger.LogDebug("RequestAllParams: attempt {Attempt} received {Got}/{Expected}", attempt, parameters.Count, totalExpected);

            if (parameters.Count > 0 && parameters.Count >= totalExpected)
            {
                break;
            }

            // Partial read — request only missing indices before next attempt.
            if (parameters.Count > 0 && totalExpected > 0)
            {
                logger.LogDebug($"RequestAllParams: partial read, requesting missing...");
                await RequestMissingParamsAsync(parameters, totalExpected, ct);

                if (parameters.Count >= totalExpected)
                {
                    break;
                }
            }

            await Task.Delay(2000, ct);
        }

        logger.LogInformation("RequestAllParams: final {Got}/{Expected}", parameters.Count, totalExpected);

        if (parameters.Count < totalExpected)
        {
            logger.LogWarning("Parameter read incomplete: {Got}/{Expected}", parameters.Count, totalExpected);
        }

        // Remove duplicates by name, keeping the last received value.
        return parameters
            .GroupBy(p => p.Name)
            .Select(g => g.Last())
            .ToList();
    }

    /// <summary>
    /// Sets a single parameter on the autopilot via PARAM_SET.
    /// </summary>
    public async Task<bool> SetParamAsync(Parameter parameter, CancellationToken ct)
    {
        var paramId = new byte[16];
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(parameter.Name);
        Array.Copy(nameBytes, paramId, Math.Min(nameBytes.Length, 16));

        var paramSet = new mavlink_param_set_t
        {
            target_system = 1,
            target_component = 1,
            param_id = paramId,
            param_value = parameter.Value,
            param_type = parameter.ParamType
        };

        await SendPacketAsync(MAVLINK_MSG_ID.PARAM_SET, paramSet, ct);

        var response = await WaitForMessageAsync(
            MAVLINK_MSG_ID.PARAM_VALUE,
            3000,
            ct);

        if (response is null)
        {
            return false;
        }

        var confirmed = (mavlink_param_value_t)response.data;
        var confirmedName = System.Text.Encoding.ASCII.GetString(confirmed.param_id).TrimEnd('\0');

        // Check that the confirmed parameter matches the one we set
        if (!confirmedName.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check that the confirmed value is close to the one we set
        if (Math.Abs(confirmed.param_value - parameter.Value) > 0.001f)
        {
            logger.LogDebug("Note: {ParameterName} sent={SentValue}, device stored={StoredValue}", parameter.Name, parameter.Value, confirmed.param_value);
        }

        return true;
    }

    /// <summary>
    /// Retrieves the firmware version string from the autopilot (AUTOPILOT_VERSION.flight_sw_version).
    /// </summary>
    public async Task<string> GetFirmwareVersionAsync(CancellationToken ct)
    {
        var response = await RequestMessageAsync(MAVLINK_MSG_ID.AUTOPILOT_VERSION, 3000, ct);

        if (response is null)
            return string.Empty;

        var version = (mavlink_autopilot_version_t)response.data;

        var major = (version.flight_sw_version >> 24) & 0xFF;
        var minor = (version.flight_sw_version >> 16) & 0xFF;
        var patch = (version.flight_sw_version >> 8) & 0xFF;
        var typeCode = version.flight_sw_version & 0xFF;

        var typeSuffix = typeCode switch
        {
            255 => "",
            192 => "-rc",
            128 => "-beta",
            64 => "-alpha",
            0 => "-dev",
            _ => $"-t{typeCode}"
        };

        var versionString = $"{major}.{minor}.{patch}{typeSuffix}";

        logger.LogInformation("Firmware version: {VersionString} (raw: 0x{RawVersion:X8})", versionString, version.flight_sw_version);

        return versionString;
    }

    /// <summary>
    /// Waiting for a heartbeat message from the device, with a specified timeout.
    /// </summary>
    public async Task<bool> WaitForHeartbeatAsync(int timeoutMs, CancellationToken ct)
    {
        var msg = await WaitForMessageAsync(MAVLINK_MSG_ID.HEARTBEAT, timeoutMs, ct);
        return msg is not null;
    }

    /// <summary>
    /// Sends MAV_CMD_PREFLIGHT_STORAGE (245) to reset parameters to defaults.
    /// </summary>
    public async Task<bool> ResetParamsAsync(CancellationToken ct)
    {
        await EstablishGcsPresenceAsync(ct);

        var ack = await SendCommandAndWaitAckAsync(
            (ushort)MAV_CMD.PREFLIGHT_STORAGE, 10000, ct, param1: 2);

        if (ack is null)
        {
            logger.LogWarning("ResetParams: no COMMAND_ACK (timeout 10s)");

            return false;
        }

        if (ack.Value.command != (ushort)MAV_CMD.PREFLIGHT_STORAGE)
        {
            logger.LogDebug("ResetParams: ACK for wrong command ({Command}), ignoring", ack.Value.command);

            return false;
        }

        if (ack.Value.result == (byte)MAV_RESULT.ACCEPTED)
        {
            logger.LogInformation("Parameters reset accepted by device");

            return true;
        }

        logger.LogWarning("ResetParams: device rejected reset (result={Result})", ack.Value.result);

        return false;
    }

    public Task RebootNormalAsync(CancellationToken ct)
        => SendCommandAsync((ushort)MAV_CMD.PREFLIGHT_REBOOT_SHUTDOWN, ct, param1: 1); // 1 = normal reboot, NOT bootloader

    /// <summary>
    /// Sends MAV_CMD_FLASH_BOOTLOADER command and waits for ACK.
    /// Returns true if bootloader was updated successfully.
    /// </summary>
    public async Task<bool> FlashBootloaderAsync(CancellationToken ct)
    {
        // Establish GCS presence
        await EstablishGcsPresenceAsync(ct);

        logger.LogDebug("FlashBootloader: sending MAV_CMD_FLASH_BOOTLOADER ({Command}), param5={Param5}", ArduPilotConstants.MavCmdFlashBootloader, ArduPilotConstants.BootloaderMagicNumber);

        // Bootloader write takes 5-15 seconds
        var ack = await SendCommandAndWaitAckAsync(
            ArduPilotConstants.MavCmdFlashBootloader, 30000, ct,
            param5: ArduPilotConstants.BootloaderMagicNumber);

        if (ack is null)
        {
            logger.LogWarning("FlashBootloader: no ACK received (timeout 30s)");

            return false;
        }

        logger.LogDebug("FlashBootloader: ACK received, command={Command}, result={Result}", ack.Value.command, ack.Value.result);

        // Check that ACK is for our command
        if (ack.Value.command != ArduPilotConstants.MavCmdFlashBootloader)
        {
            logger.LogDebug("FlashBootloader: ACK for wrong command ({Command}), ignoring", ack.Value.command);

            return false;
        }

        // MAV_RESULT.ACCEPTED = 0
        if (ack.Value.result == (byte)MAV_RESULT.ACCEPTED)
        {
            logger.LogInformation("FlashBootloader: bootloader updated successfully");

            return true;
        }

        var resultName = ack.Value.result switch
        {
            1 => "TEMPORARILY_REJECTED",
            2 => "DENIED",
            3 => "UNSUPPORTED",
            4 => "FAILED",
            _ => $"UNKNOWN ({ack.Value.result})"
        };

        logger.LogWarning("FlashBootloader: rejected with result={ResultName}", resultName);

        return false;
    }

    /// <summary>
    /// Reboots the device into bootloader mode and reads boot messages for a specified timeout.
    /// </summary>
    public async Task<List<string>> ReadBootMessagesAsync(int timeoutMs, CancellationToken ct)
    {
        var messages = new List<string>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            while (true)
            {
                var msg = await ReadMessageAsync(cts.Token);

                if (msg is null)
                {
                    continue;
                }

                if (msg.msgid == (uint)MAVLINK_MSG_ID.STATUSTEXT)
                {
                    var statusText = (mavlink_statustext_t)msg.data;
                    var text = System.Text.Encoding.UTF8.GetString(statusText.text).TrimEnd('\0');

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        messages.Add(text);

                        logger.LogInformation("STATUSTEXT: {Text}", text);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
        }

        return messages;
    }

    /// <summary>
    /// Checks if the core sensors (gyro and accelerometer) are healthy by reading SYS_STATUS messages within a specified timeout.
    /// </summary>
    public async Task<bool> AreCoreSensorsHealthyAsync(int timeoutMs, CancellationToken ct)
    {
        await EstablishGcsPresenceAsync(ct);

        // MAV_SYS_STATUS_SENSOR bits
        const uint Gyro = 1;   // 3D gyro
        const uint Accel = 2;   // 3D accel
        const uint Core = Gyro | Accel;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            MAVLinkMessage? msg = null;
            try
            {
                msg = await RequestMessageAsync(MAVLINK_MSG_ID.SYS_STATUS, 2000, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {

            }

            if (msg?.data is mavlink_sys_status_t status)
            {
                var enabled = status.onboard_control_sensors_enabled & Core;
                var healthy = status.onboard_control_sensors_health & Core;

                if (enabled == Core && healthy == Core)
                {
                    return true;
                }

                logger.LogWarning(
                    "Core sensors not healthy: enabled=0x{En:X} health=0x{He:X}",
                    status.onboard_control_sensors_enabled,
                    status.onboard_control_sensors_health);
            }

            await Task.Delay(300, ct);
        }

        return false;
    }


    /// <summary>
    /// Reads and parses a single MAVLink message from the port.
    /// </summary>
    private async Task<MAVLinkMessage?> ReadMessageAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var msg = await Task.Run(() =>
            {
                try
                {
                    return parser.ReadPacket(port.BaseStream);
                }
                catch (TimeoutException)
                {
                    return null;
                }
            });

            ct.ThrowIfCancellationRequested();
            return msg;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task ReadTelemetryLoopAsync(
    Action<float> onAltitude,
    Action? onDisconnected,
    CancellationToken ct)
    {
        var consecutiveErrors = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var msg = await ReadMessageAsync(ct);

                if (msg is null)
                {
                    consecutiveErrors++;
                    if (consecutiveErrors >= 10)
                    {
                        logger.LogWarning("Telemetry: device lost (10 consecutive read failures)");

                        onDisconnected?.Invoke();
                        break;
                    }
                    continue;
                }

                consecutiveErrors = 0;

                if (msg.data is null)
                {
                    continue;
                }

                if (msg.msgid == (uint)MAVLINK_MSG_ID.GLOBAL_POSITION_INT)
                {
                    var pos = (mavlink_global_position_int_t)msg.data;
                    var altitudeM = pos.relative_alt / 1000.0f;
                    onAltitude(altitudeM);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                logger.LogWarning("Telemetry: IOException — device disconnected");

                onDisconnected?.Invoke();

                break;
            }
            catch
            {
                consecutiveErrors++;
                if (consecutiveErrors >= 10)
                {
                    logger.LogWarning("Telemetry: device lost");

                    onDisconnected?.Invoke();

                    break;
                }
            }
        }
    }

    /// <summary>
    /// Waits for a specific MAVLink message by ID within the given timeout.
    /// </summary>
    private async Task<MAVLinkMessage?> WaitForMessageAsync(
        MAVLINK_MSG_ID messageId,
        int timeoutMs,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            while (true)
            {
                var msg = await ReadMessageAsync(cts.Token);

                if (msg is null)
                {
                    continue;
                }

                if (msg.data is null)
                {
                    continue;
                }

                if (msg.msgid == (uint)messageId)
                {
                    return msg;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the running firmware's git hash from AUTOPILOT_VERSION.flight_custom_version.
    /// </summary>
    public async Task<string> GetFirmwareGitHashAsync(CancellationToken ct)
    {
        var response = await RequestMessageAsync(MAVLINK_MSG_ID.AUTOPILOT_VERSION, 3000, ct);

        if (response is null)
        {
            logger.LogWarning("GetFirmwareGitHash: no AUTOPILOT_VERSION response");
            return string.Empty;
        }

        var version = (mavlink_autopilot_version_t)response.data;
        var hash = System.Text.Encoding.ASCII.GetString(version.flight_custom_version).TrimEnd('\0');

        logger.LogDebug("Device firmware git hash: {Hash}", hash);

        return hash;
    }

    /// <summary>
    /// Requests individual missing parameters by index.
    /// Used when PARAM_REQUEST_LIST returns a partial set.
    /// </summary>
    private async Task RequestMissingParamsAsync(
        List<Parameter> received,
        int totalExpected,
        CancellationToken ct)
    {
        // Build set of received indices from param_index if available,
        // otherwise approximate by position
        var receivedNames = received.Select(p => p.Name).ToHashSet();

        for (var idx = 0; idx < totalExpected; idx++)
        {
            ct.ThrowIfCancellationRequested();

            // Request by index
            var command = new mavlink_param_request_read_t
            {
                target_system = 1,
                target_component = 1,
                param_index = (short)idx,
                param_id = new byte[16]
            };

            await SendPacketAsync(MAVLINK_MSG_ID.PARAM_REQUEST_READ, command, ct);

            // Wait up to 500 ms for this specific param 
            var deadline = DateTime.UtcNow.AddMilliseconds(500);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                var msg = await ReadMessageAsync(ct);
                if (msg?.data is null) continue;
                if (msg.msgid != (uint)MAVLINK_MSG_ID.PARAM_VALUE) continue;

                var pv = (mavlink_param_value_t)msg.data;
                var name = System.Text.Encoding.ASCII
                    .GetString(pv.param_id)
                    .TrimEnd('\0');

                if (!receivedNames.Contains(name))
                {
                    received.Add(new Parameter
                    {
                        Name = name,
                        Value = pv.param_value,
                        ParamType = pv.param_type
                    });
                    _ = receivedNames.Add(name);
                }
                break;
            }

            await Task.Delay(20, ct);
        }
    }
}
