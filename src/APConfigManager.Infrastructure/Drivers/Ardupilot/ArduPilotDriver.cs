using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;

namespace APConfigManager.Infrastructure.Drivers.Ardupilot;

/// <summary>
/// ArduPilot driver. Orchestrates all operations for ArduPilot-compatible boards
/// by coordinating bootloader protocol, MAVLink protocol, and serial transport.
/// </summary>
public class ArduPilotDriver : IAutopilotDriver
{
    private const int ChunkSize = ArduPilotConstants.ProgMultiMaxSize; // 64 bytes per write
    private const int HeartbeatTimeoutMs = 3000;
    private const int PortSwitchTimeoutSeconds = 20;
    private const int WriteParamsPasses = 6;
    private Action? onDeviceDisconnected;
    private readonly Func<Guid?, List<string>>? getOccupiedPorts;

    private readonly ISerialPortAdapter port;
    private readonly IBootloaderProtocol bootloader;
    private readonly ITelemetryProtocol telemetry;
    private readonly IPortScanner portScanner;

    private DeviceSession? session;
    private BootMode currentMode = BootMode.Normal;

    // Telemetry
    private CancellationTokenSource? telemetryCts;
    private Task? telemetryTask;
    private Action<float>? onAltitudeUpdate;

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
    };

    private static readonly string[] FinalForceParams =
    {
        "COMPASS_EXTERNAL",
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

    private static readonly byte[] CrcPad = { 0xFF, 0xFF, 0xFF, 0xFF };

    private static readonly uint[] CrcTable =
    {
        0x00000000, 0x77073096, 0xee0e612c, 0x990951ba, 0x076dc419, 0x706af48f, 0xe963a535, 0x9e6495a3,
        0x0edb8832, 0x79dcb8a4, 0xe0d5e91e, 0x97d2d988, 0x09b64c2b, 0x7eb17cbd, 0xe7b82d07, 0x90bf1d91,
        0x1db71064, 0x6ab020f2, 0xf3b97148, 0x84be41de, 0x1adad47d, 0x6ddde4eb, 0xf4d4b551, 0x83d385c7,
        0x136c9856, 0x646ba8c0, 0xfd62f97a, 0x8a65c9ec, 0x14015c4f, 0x63066cd9, 0xfa0f3d63, 0x8d080df5,
        0x3b6e20c8, 0x4c69105e, 0xd56041e4, 0xa2677172, 0x3c03e4d1, 0x4b04d447, 0xd20d85fd, 0xa50ab56b,
        0x35b5a8fa, 0x42b2986c, 0xdbbbc9d6, 0xacbcf940, 0x32d86ce3, 0x45df5c75, 0xdcd60dcf, 0xabd13d59,
        0x26d930ac, 0x51de003a, 0xc8d75180, 0xbfd06116, 0x21b4f4b5, 0x56b3c423, 0xcfba9599, 0xb8bda50f,
        0x2802b89e, 0x5f058808, 0xc60cd9b2, 0xb10be924, 0x2f6f7c87, 0x58684c11, 0xc1611dab, 0xb6662d3d,
        0x76dc4190, 0x01db7106, 0x98d220bc, 0xefd5102a, 0x71b18589, 0x06b6b51f, 0x9fbfe4a5, 0xe8b8d433,
        0x7807c9a2, 0x0f00f934, 0x9609a88e, 0xe10e9818, 0x7f6a0dbb, 0x086d3d2d, 0x91646c97, 0xe6635c01,
        0x6b6b51f4, 0x1c6c6162, 0x856530d8, 0xf262004e, 0x6c0695ed, 0x1b01a57b, 0x8208f4c1, 0xf50fc457,
        0x65b0d9c6, 0x12b7e950, 0x8bbeb8ea, 0xfcb9887c, 0x62dd1ddf, 0x15da2d49, 0x8cd37cf3, 0xfbd44c65,
        0x4db26158, 0x3ab551ce, 0xa3bc0074, 0xd4bb30e2, 0x4adfa541, 0x3dd895d7, 0xa4d1c46d, 0xd3d6f4fb,
        0x4369e96a, 0x346ed9fc, 0xad678846, 0xda60b8d0, 0x44042d73, 0x33031de5, 0xaa0a4c5f, 0xdd0d7cc9,
        0x5005713c, 0x270241aa, 0xbe0b1010, 0xc90c2086, 0x5768b525, 0x206f85b3, 0xb966d409, 0xce61e49f,
        0x5edef90e, 0x29d9c998, 0xb0d09822, 0xc7d7a8b4, 0x59b33d17, 0x2eb40d81, 0xb7bd5c3b, 0xc0ba6cad,
        0xedb88320, 0x9abfb3b6, 0x03b6e20c, 0x74b1d29a, 0xead54739, 0x9dd277af, 0x04db2615, 0x73dc1683,
        0xe3630b12, 0x94643b84, 0x0d6d6a3e, 0x7a6a5aa8, 0xe40ecf0b, 0x9309ff9d, 0x0a00ae27, 0x7d079eb1,
        0xf00f9344, 0x8708a3d2, 0x1e01f268, 0x6906c2fe, 0xf762575d, 0x806567cb, 0x196c3671, 0x6e6b06e7,
        0xfed41b76, 0x89d32be0, 0x10da7a5a, 0x67dd4acc, 0xf9b9df6f, 0x8ebeeff9, 0x17b7be43, 0x60b08ed5,
        0xd6d6a3e8, 0xa1d1937e, 0x38d8c2c4, 0x4fdff252, 0xd1bb67f1, 0xa6bc5767, 0x3fb506dd, 0x48b2364b,
        0xd80d2bda, 0xaf0a1b4c, 0x36034af6, 0x41047a60, 0xdf60efc3, 0xa867df55, 0x316e8eef, 0x4669be79,
        0xcb61b38c, 0xbc66831a, 0x256fd2a0, 0x5268e236, 0xcc0c7795, 0xbb0b4703, 0x220216b9, 0x5505262f,
        0xc5ba3bbe, 0xb2bd0b28, 0x2bb45a92, 0x5cb36a04, 0xc2d7ffa7, 0xb5d0cf31, 0x2cd99e8b, 0x5bdeae1d,
        0x9b64c2b0, 0xec63f226, 0x756aa39c, 0x026d930a, 0x9c0906a9, 0xeb0e363f, 0x72076785, 0x05005713,
        0x95bf4a82, 0xe2b87a14, 0x7bb12bae, 0x0cb61b38, 0x92d28e9b, 0xe5d5be0d, 0x7cdcefb7, 0x0bdbdf21,
        0x86d3d2d4, 0xf1d4e242, 0x68ddb3f8, 0x1fda836e, 0x81be16cd, 0xf6b9265b, 0x6fb077e1, 0x18b74777,
        0x88085ae6, 0xff0f6a70, 0x66063bca, 0x11010b5c, 0x8f659eff, 0xf862ae69, 0x616bffd3, 0x166ccf45,
        0xa00ae278, 0xd70dd2ee, 0x4e048354, 0x3903b3c2, 0xa7672661, 0xd06016f7, 0x4969474d, 0x3e6e77db,
        0xaed16a4a, 0xd9d65adc, 0x40df0b66, 0x37d83bf0, 0xa9bcae53, 0xdebb9ec5, 0x47b2cf7f, 0x30b5ffe9,
        0xbdbdf21c, 0xcabac28a, 0x53b39330, 0x24b4a3a6, 0xbad03605, 0xcdd70693, 0x54de5729, 0x23d967bf,
        0xb3667a2e, 0xc4614ab8, 0x5d681b02, 0x2a6f2b94, 0xb40bbe37, 0xc30c8ea1, 0x5a05df1b, 0x2d02ef8d
    };

    /// <summary>
    /// Initializes the driver with protocol and transport dependencies.
    /// </summary>
    public ArduPilotDriver(
        ISerialPortAdapter port,
        IBootloaderProtocol bootloaderProtocol,
        ITelemetryProtocol telemetryProtocol,
        IPortScanner portScanner,
        Func<Guid?, List<string>>? getOccupiedPorts = null)
    {
        this.port = port;
        this.bootloader = bootloaderProtocol;
        this.telemetry = telemetryProtocol;
        this.portScanner = portScanner;
        this.getOccupiedPorts = getOccupiedPorts;
    }

    /// <summary>
    /// Opens the serial port and establishes MAVLink communication.
    /// Sends a heartbeat and waits for a response to confirm the device is present.
    /// Closes the port if connection fails.
    /// </summary>
    public async Task<DeviceSession> ConnectAsync(string port, int baudRate, CancellationToken ct)
    {
        this.port.Open(port, baudRate);

        var fwVersion = string.Empty;
        var fwDescription = string.Empty;
        uint blRevision = 0;

        try
        {
            await this.telemetry.SendHeartbeatAsync(ct);
            await WaitForDeviceHeartbeatAsync(ct);
            currentMode = BootMode.Normal;

            try
            {
                fwVersion = await telemetry.GetFirmwareVersionAsync(ct);
            }
            catch
            {
            }

            try
            {
                var bootMessages = await telemetry.ReadBootMessagesAsync(3000, ct);

                foreach (var msg in bootMessages)
                {
                    // ArduPilot format: "ArduPlane V4.5.5-beta1 (2ff5b966)"
                    // Custom format: "MyVersion: 2.0.0"
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        if (fwDescription.Length > 0)
                        {
                            fwDescription += " | ";
                        }

                        fwDescription += msg;
                    }
                }
            }
            catch { }
        }
        catch
        {
            this.port.Purge();
            var syncOk = await this.bootloader.SyncAsync(ct);
            if (syncOk)
            {
                var deviceInfo = await this.bootloader.GetDeviceInfoAsync(ct);
                blRevision = deviceInfo.BootloaderRevision;
                currentMode = BootMode.Bootloader;
            }
            else
            {
                this.port.Close();
                throw new DeviceConnectionException(
                    $"No response from device on port {port} (neither MAVLink nor bootloader).");
            }
        }

        var portInfo = this.portScanner.GetPortDescription(port);
        // this.currentMode = currentMode;

        var state = this.currentMode == BootMode.Bootloader
            ? DeviceState.InBootloader
            : DeviceState.Connected;

        session = new DeviceSession
        {
            Id = Guid.NewGuid(),
            Port = port,
            BaudRate = baudRate,
            State = state,
            ConnectedAt = DateTime.UtcNow,
            DeviceSerial = portInfo?.DeviceSerial ?? string.Empty,
            UsbLocation = portInfo?.LocationPath ?? string.Empty,
            FirmwareVersion = fwVersion,
            FirmwareDescription = fwDescription,
            BootloaderRevision = blRevision
        };

        return session;
    }

    /// <summary>
    /// Reconnects to the device after a reboot, binding strictly to the port
    /// that belongs to this session's device (identified by its USB serial).
    /// Throws if the device does not reappear or another device is found instead.
    /// </summary>
    private async Task ReconnectAfterBootAsync(CancellationToken ct)
    {
        var oldPort = session!.Port;
        var oldSerial = session.DeviceSerial;
        var oldLocation = session.UsbLocation;
        var sessionId = session.Id;
        var baudRate = session.BaudRate;

        // Strip the #USBMI(...) suffix — it changes between boot modes.
        // The stable part is everything up to the last #USBMI segment.
        var locationPrefix = StripUsbMiSuffix(oldLocation);

        port.Close();
        await Task.Delay(2000, ct);

        var portsAfterBoot = portScanner.GetAvailablePorts();
        var excludePorts = getOccupiedPorts?.Invoke(sessionId) ?? new List<string>();

        string? targetPort = null;

        // Phase 1: match by location prefix — works across all boot mode transitions
        if (!string.IsNullOrWhiteSpace(locationPrefix))
        {

            targetPort = await WaitForPortByLocationPrefixAsync(
                locationPrefix,
                excludePorts,
                TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                ct);

        }

        // Phase 2: serial-preferred MAVLink fallback
        if (string.IsNullOrWhiteSpace(targetPort))
        {

            targetPort = await portScanner.WaitForMavlinkPortAsync(
                oldSerial,
                portsAfterBoot,
                excludePorts,
                TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                ct);
        }

        // Phase 3: last resort — original port name
        if (string.IsNullOrWhiteSpace(targetPort)
            && !excludePorts.Contains(oldPort, StringComparer.OrdinalIgnoreCase))
        {
            targetPort = oldPort;
        }

        if (string.IsNullOrWhiteSpace(targetPort))
        {
            throw new DeviceConnectionException("Device did not reappear after reboot.");
        }


        port.Open(targetPort, baudRate);
        port.Purge();

        try
        {
            await telemetry.SendHeartbeatAsync(ct);
            await WaitForDeviceHeartbeatAsync(ct);
        }
        catch { }

        try
        {
            var fwVer = await telemetry.GetFirmwareVersionAsync(ct);

            if (session is not null && !string.IsNullOrWhiteSpace(fwVer))
            {
                session.FirmwareVersion = fwVer;
            }
        }
        catch { }

        var newPortInfo = portScanner.GetPortDescription(targetPort);
        currentMode = BootMode.Normal;
        UpdateSessionPortAndState(targetPort, DeviceState.Connected);

        if (session is not null
            && newPortInfo is not null
            && !string.IsNullOrWhiteSpace(newPortInfo.DeviceSerial))
        {
            session.DeviceSerial = newPortInfo.DeviceSerial;
            session.UsbLocation = newPortInfo.LocationPath;
        }
    }

    /// <summary>
    /// Retrieves device information from the bootloader.
    /// Switches to bootloader mode if not already in it.
    /// </summary>
    public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct)
    {
        EnsureConnected();
        await StopTelemetryAsync();
        port.Purge();

        await EnsureModeAsync(BootMode.Bootloader, ct);

        if (currentMode == BootMode.Bootloader)
        {
            var synced = await bootloader.SyncAsync(ct);
            if (!synced)
            {
                throw new DeviceConnectionException("Bootloader sync failed.");
            }
        }

        return await bootloader.GetDeviceInfoAsync(ct);
    }

    /// <summary>
    /// Performs full flash cycle: erase, write chunks, verify CRC, boot.
    /// Reports progress through IProgress with percent (0-100) and status message.
    /// Returns FlashResult with success status, bytes written, and firmware version.
    /// </summary>
    public async Task<FlashResult> FlashAsync(
        FirmwarePackage firmware,
        IProgress<(int percent, string message)> progress,
        CancellationToken ct)
    {
        await StopTelemetryAsync();

        ArgumentNullException.ThrowIfNull(firmware);
        ArgumentNullException.ThrowIfNull(progress);
        EnsureConnected();

        try
        {
            await EnsureModeAsync(BootMode.Bootloader, ct);

            if (firmware.ImageBytes.Length == 0)
            {
                return new FlashResult
                {
                    Success = false,
                    ErrorMessage = "Firmware image is empty."
                };
            }

            // Pad firmware to 4-byte alignment (bootloader CRC covers padded area)
            var imageToFlash = firmware.ImageBytes;
            var alignRemainder = imageToFlash.Length % 4;

            if (alignRemainder != 0)
            {
                imageToFlash = new byte[imageToFlash.Length + (4 - alignRemainder)];

                Array.Copy(firmware.ImageBytes, imageToFlash, firmware.ImageBytes.Length);

                for (var i = firmware.ImageBytes.Length; i < imageToFlash.Length; i++)
                {
                    imageToFlash[i] = 0xFF;
                }
            }

            var deviceInfo = await bootloader.GetDeviceInfoAsync(ct);
            var expectedCrc = CalculateFirmwareCrc(firmware.ImageBytes, deviceInfo.FlashSize);

            progress.Report((0, "Erasing..."));
            await this.bootloader.SyncAsync(ct);
            await this.bootloader.ChipEraseAsync(ct);

            var bytesWritten = 0;
            var totalBytes = imageToFlash.Length;

            for (var offset = 0; offset < totalBytes; offset += ChunkSize)
            {
                ct.ThrowIfCancellationRequested();

                var size = Math.Min(ChunkSize, totalBytes - offset);
                var chunk = new byte[size];
                Array.Copy(imageToFlash, offset, chunk, 0, size);

                await this.bootloader.ProgramMultiAsync(chunk, ct);
                bytesWritten += size;

                // Report progress as 0-80% for writing phase. 
                var percent = (int)(80.0 * bytesWritten / totalBytes);
                var prevPercent = (int)(80.0 * (bytesWritten - size) / totalBytes);

                // Only report if percent increased to avoid flooding progress updates
                if (percent > prevPercent)
                {
                    progress.Report((percent, $"Writing {bytesWritten}/{totalBytes}"));
                }
            }

            progress.Report((90, "Verifying..."));
            var deviceCrc = await bootloader.GetCrcAsync(ct);

            if (deviceCrc != expectedCrc)
            {
                return new FlashResult
                {
                    Success = false,
                    BytesWritten = bytesWritten,
                    FirmwareVersion = firmware.Version,
                    ErrorMessage = $"CRC mismatch: expected 0x{expectedCrc:X8}, device 0x{deviceCrc:X8}"
                };
            }

            progress.Report((95, "Booting..."));
            await bootloader.BootAsync(ct);
            await ReconnectAfterBootAsync(ct);
            progress.Report((100, "Done"));

            if (onAltitudeUpdate != null)
            {
                StartTelemetry(onAltitudeUpdate);
            }

            return new FlashResult
            {
                Success = true,
                BytesWritten = bytesWritten,
                FirmwareVersion = firmware.Version
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FlashResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Erases the flash memory via bootloader.
    /// Switches to bootloader mode, erases, then boots back to normal mode.
    /// </summary>
    public async Task<EraseResult> EraseAsync(
    IProgress<(int percent, string message)> progress,
    CancellationToken ct)
    {
        await StopTelemetryAsync();

        ArgumentNullException.ThrowIfNull(progress);
        EnsureConnected();

        try
        {
            await EnsureModeAsync(BootMode.Bootloader, ct);

            progress.Report((0, "Reading device info..."));
            await this.bootloader.GetDeviceInfoAsync(ct);

            progress.Report((10, "Erasing..."));
            await this.bootloader.SyncAsync(ct);
            await this.bootloader.ChipEraseAsync(ct);

            progress.Report((80, "Booting..."));
            await bootloader.BootAsync(ct);
            await ReconnectAfterBootAsync(ct);
            progress.Report((100, "Done"));

            if (onAltitudeUpdate != null)
            {
                StartTelemetry(onAltitudeUpdate);
            }

            return new EraseResult { Success = true };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new EraseResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Reads all parameters from the autopilot via MAVLink.
    /// Switches to normal mode if currently in bootloader.
    /// </summary>
    public async Task<List<Parameter>> ReadParamsAsync(CancellationToken ct)
    {
        EnsureConnected();
        await EnsureModeAsync(BootMode.Normal, ct);

        return await this.telemetry.RequestAllParamsAsync(ct);
    }

    /// <summary>
    /// Writes parameters to the autopilot with multi-pass retry (up to 3 passes).
    /// Failed parameters are retried on subsequent passes.
    /// Reports progress as percent (0-100) with pass information.
    /// </summary>
    public async Task<ParameterUploadResult> WriteParamsAsync(
    List<Parameter> parameters,
    IProgress<(int current, int total)> progress,
    CancellationToken ct)
    {
        await StopTelemetryAsync();

        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(progress);
        EnsureConnected();
        await EnsureModeAsync(BootMode.Normal, ct);

        var sent = 0;
        var skippedSame = 0;

        try
        {
            Console.WriteLine("Resetting parameters to defaults before upload...");
            await telemetry.ResetParamsAsync(ct);
            await Task.Delay(1000, ct);
            await telemetry.RebootNormalAsync(ct);
            port.Close();
            await ReconnectAfterBootAsync(ct);

            var deviceParams = await telemetry.RequestAllParamsAsync(ct);

            // Build a dictionary of device parameters for quick lookup by name.
            var deviceMap = deviceParams
                 .GroupBy(p => p.Name)
                 .ToDictionary(g => g.Key, g => g.Last().Value);

            // Build a dictionary of device parameter types for quick lookup by name.
            var deviceTypeMap = deviceParams
                .GroupBy(p => p.Name)
                .ToDictionary(g => g.Key, g => g.Last().ParamType);

            Console.WriteLine($"Device has {deviceMap.Count} parameters");

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
                    Console.WriteLine($"No progress after pass {pass - 1}, stopping");
                    break;
                }
                previousPendingCount = pending.Count;

                Console.WriteLine($"Pass {pass}: {pending.Count} parameters");

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
                    Console.WriteLine($"Rebooting to apply parameters...");

                    await this.telemetry.RebootNormalAsync(ct);
                    port.Close();
                    await ReconnectAfterBootAsync(ct);

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

                    // Console.WriteLine($"After reboot: {pending.Count} pending, {missing.Count} missing");
                }
            }

            //Console.WriteLine($"Done: sent={sent}, same={skippedSame}, missing={missing.Count}, failed={pending.Count}");

            // Force-write params that get overwritten by device after reboots
            var forceList = parameters
                .Where(p => FinalForceParams.Any(f =>
                    p.Name.Equals(f, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (forceList.Count > 0)
            {
                Console.WriteLine($"Force-writing {forceList.Count} mandatory parameters...");

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

            if (onAltitudeUpdate != null)
            {
                StartTelemetry(onAltitudeUpdate);
            }

            return new ParameterUploadResult
            {
                Success = pending.Count == 0,
                Sent = sent,
                Failed = pending.Count,
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
    /// Reboots the device into the specified mode (Normal or Bootloader).
    /// When switching to Bootloader: sends MAVLink reboot command, waits for new port,
    /// opens it, and syncs with the bootloader.
    /// When switching to Normal: sends boot command, waits for new port,
    /// opens it, and waits for heartbeat.
    /// </summary>
    public async Task<BootResult> RebootAsync(BootMode mode, CancellationToken ct)
    {
        await StopTelemetryAsync();

        EnsureConnected();

        try
        {
            if (mode == BootMode.Bootloader)
            {
                var portsBefore = portScanner.GetAvailablePorts();

                await telemetry.RebootToBootloaderAsync(ct);
                port.Close();

                var newPort = await portScanner.WaitForBootloaderPortAsync(
                    session!.Port,
                    portsBefore,
                    TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                    ct);

                if (string.IsNullOrWhiteSpace(newPort))
                {
                    throw new DeviceConnectionException("Bootloader port not found.");
                }

                port.Open(newPort, ArduPilotConstants.BootloaderBaudRate);

                var syncOk = await bootloader.SyncAsync(ct);
                if (!syncOk)
                {
                    port.Close();
                    throw new DeviceConnectionException("Bootloader sync failed.");
                }

                var info = await bootloader.GetDeviceInfoAsync(ct);
                if (session != null)
                    session.BootloaderRevision = info.BootloaderRevision;

                currentMode = BootMode.Bootloader;
                UpdateSessionPortAndState(newPort, DeviceState.InBootloader);

                return new BootResult
                {
                    Success = true,
                    NewPort = newPort
                };
            }

            // ── Normal mode: boot from bootloader, reconnect to MAVLink port ──

            await bootloader.BootAsync(ct);

            try
            {
                await ReconnectAfterBootAsync(ct);
            }
            catch (Exception ex)
            {
                return new BootResult
                {
                    Success = false,
                    ErrorMessage = $"Device booted but reconnection failed: {ex.Message}"
                };
            }

            return new BootResult
            {
                Success = true,
                NewPort = session!.Port
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BootResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Retrieves the firmware git hash from the autopilot via MAVLink.
    /// </summary>
    public async Task<string> GetFirmwareVersionAsync(CancellationToken ct)
    {
        EnsureConnected();
        await EnsureModeAsync(BootMode.Normal, ct);
        return await this.telemetry.GetFirmwareVersionAsync(ct);
    }

    /// <summary>
    /// Resets all parameters to factory defaults via MAVLink command.
    /// </summary>
    public async Task ResetParamsAsync(CancellationToken ct)
    {
        EnsureConnected();
        await EnsureModeAsync(BootMode.Normal, ct);
        await this.telemetry.ResetParamsAsync(ct);

        Console.WriteLine("Parameters reset. Rebooting to apply...");

        await RebootAsync(BootMode.Bootloader, ct);
        await bootloader.BootAsync(ct);
        await ReconnectAfterBootAsync(ct);

        Console.WriteLine($"Rebooted. Parameters applied.");
    }

    /// <summary>
    /// Updates the bootloader from the running firmware.
    /// Requires the device to be in normal mode with firmware installed.
    /// </summary>
    public async Task<BootloaderUpdateResult> UpdateBootloaderAsync(CancellationToken ct)
    {
        await StopTelemetryAsync();

        EnsureConnected();

        try
        {
            await EnsureModeAsync(BootMode.Normal, ct);
        }
        catch (DeviceConnectionException)
        {
            return new BootloaderUpdateResult
            {
                Success = false,
                ErrorMessage = "Device must have firmware installed to update bootloader. Flash firmware first."
            };
        }

        try
        {
            var success = await telemetry.FlashBootloaderAsync(ct);

            if (!success)
            {
                return new BootloaderUpdateResult
                {
                    Success = false,
                    ErrorMessage = "Bootloader update failed (no ACK or command rejected). " +
                                   "Check API console for MAV_RESULT. Device must be in normal flight mode with ArduPilot 4.0+."
                };
            }

            Console.WriteLine("Bootloader updated. Rebooting to apply...");

            // Wait for the device to finish writing before reboot
            await Task.Delay(2000, ct);

            await telemetry.RebootNormalAsync(ct);
            port.Close();
            await ReconnectAfterBootAsync(ct);

            Console.WriteLine("Bootloader update complete. Reconnected.");

            if (onAltitudeUpdate != null)
            {
                StartTelemetry(onAltitudeUpdate);
            }

            return new BootloaderUpdateResult
            {
                Success = true
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bootloader update error: {ex.Message}");

            return new BootloaderUpdateResult
            {
                Success = false,
                ErrorMessage = $"Bootloader update error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Disconnects from the device, stops telemetry, and closes the serial port.
    /// </summary>
    /// <returns></returns>
    public async Task DisconnectAsync()
    {
        await StopTelemetryAsync();

        if (this.port.IsOpen)
        {
            this.port.Close();
        }

        session = null;
        currentMode = BootMode.Normal;
    }

    public DeviceSession? GetCurrentSession() => session;

    /// <summary>
    /// Throws SessionException if no active session or port is closed.
    /// </summary>
    private void EnsureConnected()
    {
        if (session is null || !this.port.IsOpen)
        {
            throw new SessionException("No active session. Call ConnectAsync first.");
        }
    }

    /// <summary>
    /// Starts a background task to read telemetry (altitude) from the autopilot.
    /// </summary>
    public void StartTelemetry(Action<float> onAltitude)
    {
        StopTelemetrySync();
        onAltitudeUpdate = onAltitude;
        telemetryCts = new CancellationTokenSource();
        var token = telemetryCts.Token;

        telemetryTask = Task.Run(async () =>
        {
            try
            {
                await telemetry.ReadTelemetryLoopAsync(
                    alt =>
                    {
                        if (session != null)
                        {
                            session.LastAltitude = alt;
                            session.LastTelemetryAt = DateTime.UtcNow;
                        }
                        onAltitudeUpdate?.Invoke(alt);
                    },
                    () => onDeviceDisconnected?.Invoke(),
                    token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Telemetry loop error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Stops the telemetry background task and cancels it. Waits for the task to complete.
    /// </summary>
    private void StopTelemetrySync()
    {
        if (telemetryCts is null) return;

        telemetryCts.Cancel();

        if (telemetryTask is not null)
        {
            try
            {
                telemetryTask.Wait(3000);
            }
            catch { }
        }

        telemetryCts.Dispose();
        telemetryCts = null;
        telemetryTask = null;

        if (port.IsOpen)
        {
            port.Purge();
        }
    }

    /// <summary>
    /// Stops the telemetry background task and cancels it. Waits asynchronously for the task to complete.
    /// </summary>
    public async Task StopTelemetryAsync()
    {
        if (telemetryCts is null) return;

        telemetryCts.Cancel();

        if (telemetryTask is not null)
        {
            try
            {
                await telemetryTask.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch { }
        }

        telemetryCts.Dispose();
        telemetryCts = null;
        telemetryTask = null;

        if (port.IsOpen)
        {
            port.Purge();
        }
    }

    public void SetDisconnectCallback(Action onDisconnected)
    {
        onDeviceDisconnected = onDisconnected;
    }

    /// <summary>
    /// Switches device mode if current mode doesn't match the required one.
    /// </summary>
    private async Task EnsureModeAsync(BootMode mode, CancellationToken ct)
    {
        if (this.currentMode == mode)
        {
            if (mode == BootMode.Bootloader)
            {
                await StopTelemetryAsync();
                port.Purge();
            }
            return;
        }

        var rebootResult = await RebootAsync(mode, ct);

        if (!rebootResult.Success)
        {
            throw new DeviceConnectionException(
                rebootResult.ErrorMessage ?? $"Cannot switch to {mode} mode.");
        }
    }

    /// <summary>
    /// Updates the session port and state, preserving all other fields.
    /// </summary>
    private void UpdateSessionPortAndState(string port, DeviceState state)
    {
        if (session is null)
        {
            return;
        }

        session = new DeviceSession
        {
            Id = session.Id,
            Port = port,
            BaudRate = session.BaudRate,
            State = state,
            ConnectedAt = session.ConnectedAt,
            DeviceSerial = session.DeviceSerial,
            UsbLocation = session.UsbLocation,
            FirmwareVersion = session.FirmwareVersion,
            FirmwareDescription = session.FirmwareDescription,
            BootloaderRevision = session.BootloaderRevision
        };
    }

    /// <summary>
    /// Waits for a heartbeat response from the autopilot within timeout.
    /// Sends heartbeat and listens for any MAVLink response to confirm device is alive.
    /// Throws DeviceConnectionException if no response received.
    /// </summary>
    private async Task WaitForDeviceHeartbeatAsync(CancellationToken ct)
    {
        await telemetry.SendHeartbeatAsync(ct);

        var received = await telemetry.WaitForHeartbeatAsync(HeartbeatTimeoutMs, ct);

        if (!received)
        {
            throw new DeviceConnectionException("No heartbeat response from device.");
        }
    }

    /// <summary>
    /// Calculates the expected CRC32 of the firmware image,
    /// including padding to 4-byte alignment and filling remaining flash with 0xFF.
    /// </summary>
    private static uint CalculateFirmwareCrc(byte[] imageBytes, uint flashSize)
    {
        // Align image to 4 bytes
        var aligned = imageBytes;
        var remainder = aligned.Length % 4;

        if (remainder != 0)
        {
            aligned = new byte[aligned.Length + (4 - remainder)];
            Array.Copy(imageBytes, aligned, imageBytes.Length);

            for (var i = imageBytes.Length; i < aligned.Length; i++)
            {
                aligned[i] = 0xFF;
            }
        }

        // CRC over firmware bytes
        uint state = Crc32(aligned, 0);

        // Continue CRC over remaining flash (filled with 0xFF)
        for (var i = aligned.Length; i < flashSize - 1; i += 4)
        {
            state = Crc32(CrcPad, state);
        }

        return state;
    }

    /// <summary>
    /// Calculates the CRC32 of the given data starting from the specified state.
    /// </summary>
    private static uint Crc32(byte[] data, uint state)
    {
        foreach (var b in data)
        {
            var index = (state ^ b) & 0xFF;
            state = CrcTable[index] ^ (state >> 8);
        }
        return state;
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
    /// Polls until a port appears whose LocationPath starts with the given prefix.
    /// When a device changes boot mode it keeps the same hub path but appends
    /// a different #USBMI(N) suffix — the prefix is the stable part.
    /// If multiple ports match (e.g. MAVLink + SLCAN), prefers the MAVLink one.
    /// </summary>
    private async Task<string?> WaitForPortByLocationPrefixAsync(
        string locationPrefix,
        IReadOnlyList<string> excludePorts,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            while (true)
            {
                var candidates = portScanner.GetAvailablePortsDetailed()
                    .Where(p => !excludePorts.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    .Where(p => !string.IsNullOrWhiteSpace(p.LocationPath)
                                && p.LocationPath.StartsWith(locationPrefix,
                                    StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (candidates.Count > 0)
                {
                    // Prefer the MAVLink port when the device exposes multiple interfaces
                    var mavlink = candidates.FirstOrDefault(p => p.IsMavlink);
                    return (mavlink ?? candidates[0]).Name;
                }

                await Task.Delay(300, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Removes the trailing #USBMI(...) segment from a USB LocationPath.
    /// "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)#USB(4)#USBMI(0)"
    ///   → "PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)#USB(4)"
    /// If no #USBMI segment is present the original string is returned unchanged.
    /// </summary>
    private static string StripUsbMiSuffix(string? locationPath)
    {
        if (string.IsNullOrWhiteSpace(locationPath))
            return string.Empty;

        var idx = locationPath.LastIndexOf("#USBMI(", StringComparison.OrdinalIgnoreCase);
        return idx > 0 ? locationPath[..idx] : locationPath;
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
