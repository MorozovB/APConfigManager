using System.Runtime.InteropServices;
using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using APConfigManager.Infrastructure.Transport;

namespace APConfigManager.Infrastructure.Drivers.Ardupilot;

/// <summary>
/// ArduPilot driver. Orchestrates all operations for ArduPilot-compatible boards
/// by coordinating bootloader protocol, MAVLink protocol, and serial transport.
/// </summary>
public class ArduPilotDriver : IAutopilotDriver
{
    private const int ChunkSize = 64;
    private const int HeartbeatTimeoutMs = 3000;
    private const int PortSwitchTimeoutSeconds = 20;
    private const int WriteParamsPasses = 3;

    private readonly ISerialPortAdapter port;
    private readonly IBootloaderProtocol bootloader;
    private readonly ITelemetryProtocol telemetry;
    private readonly IPortScanner portScanner;

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

    private DeviceSession? session;
    private BootMode currentMode = BootMode.Normal;

    /// <summary>
    /// Initializes the driver with protocol and transport dependencies.
    /// </summary>
    public ArduPilotDriver(
        ISerialPortAdapter port,
        IBootloaderProtocol bootloaderProtocol,
        ITelemetryProtocol telemetryProtocol,
        IPortScanner portScanner)
    {
        this.port = port;
        this.bootloader = bootloaderProtocol;
        this.telemetry = telemetryProtocol;
        this.portScanner = portScanner;
    }

    /// <summary>
    /// Opens the serial port and establishes MAVLink communication.
    /// Sends a heartbeat and waits for a response to confirm the device is present.
    /// Closes the port if connection fails.
    /// </summary>
    public async Task<DeviceSession> ConnectAsync(string port, int baudRate, CancellationToken ct)
    {
        this.port.Open(port, baudRate);

        try
        {
            await this.telemetry.SendHeartbeatAsync(ct);
            await WaitForDeviceHeartbeatAsync(ct);
            currentMode = BootMode.Normal;
        }
        catch
        {
            this.port.Flush();
            var syncOk = await this.bootloader.SyncAsync(ct);

            if (syncOk)
            {
                await bootloader.GetDeviceInfoAsync(ct);

                try
                {
                    await bootloader.SetBaudRateAsync(ArduPilotConstants.FlashBaudRate, ct);
                }
                catch
                {
                    // Stay at default baud rate
                }

                currentMode = BootMode.Bootloader;
            }
        }

        var state = this.currentMode == BootMode.Bootloader
            ? DeviceState.InBootloader
            : DeviceState.Connected;

        session = new DeviceSession
        {
            Id = Guid.NewGuid(),
            Port = port,
            BaudRate = baudRate,
            State = state,
            ConnectedAt = DateTime.UtcNow
        };

        return session;
    }

    /// <summary>
    /// Retrieves device information from the bootloader.
    /// Switches to bootloader mode if not already in it.
    /// </summary>
    public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct)
    {
        EnsureConnected();
        await EnsureModeAsync(BootMode.Bootloader, ct);
        return await this.bootloader.GetDeviceInfoAsync(ct);
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
        ArgumentNullException.ThrowIfNull(firmware);
        ArgumentNullException.ThrowIfNull(progress);
        EnsureConnected();
        // await EnsureModeAsync(BootMode.Bootloader, ct);

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
                    imageToFlash[i] = 0xFF;
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

                var percent = (int)(80.0 * bytesWritten / totalBytes);
                var prevPercent = (int)(80.0 * (bytesWritten - size) / totalBytes);
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
            port.Close();

            // Wait for device to come back on normal port
            var portsAfterBoot = portScanner.GetAvailablePorts();
            var normalPort = await portScanner.WaitForNewPortAsync(
                portsAfterBoot,
                TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                ct);

            var targetPort = !string.IsNullOrWhiteSpace(normalPort)
                ? normalPort
                : session!.Port;

            try
            {
                port.Open(targetPort, session!.BaudRate);
                await telemetry.SendHeartbeatAsync(ct);
                await WaitForDeviceHeartbeatAsync(ct);
            }
            catch
            {
                // Device might take time to boot — session will reconnect on next operation
            }

            currentMode = BootMode.Normal;
            UpdateSessionState(DeviceState.Connected);
            progress.Report((100, "Done"));

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
        ArgumentNullException.ThrowIfNull(progress);
        EnsureConnected();
        await EnsureModeAsync(BootMode.Bootloader, ct);

        try
        {
            await EnsureModeAsync(BootMode.Bootloader, ct);

            progress.Report((0, "Reading device info..."));
            await this.bootloader.GetDeviceInfoAsync(ct);

            progress.Report((10, "Erasing..."));
            await this.bootloader.SyncAsync(ct);
            await this.bootloader.ChipEraseAsync(ct);

            progress.Report((80, "Booting..."));
            await this.bootloader.BootAsync(ct);
            port.Close();

            var portsAfterBoot = portScanner.GetAvailablePorts();
            var normalPort = await portScanner.WaitForNewPortAsync(
                portsAfterBoot,
                TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                ct);

            var targetPort = !string.IsNullOrWhiteSpace(normalPort)
                ? normalPort
                : session!.Port;

            try
            {
                port.Open(targetPort, session!.BaudRate);
            }
            catch
            {
                // Will reconnect on next operation
            }
            currentMode = BootMode.Normal;
            UpdateSessionState(DeviceState.Connected);
            progress.Report((100, "Done"));

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
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(progress);
        EnsureConnected();
        await EnsureModeAsync(BootMode.Normal, ct);

        var sent = 0;
        var skippedSame = 0;
        var skippedMissing = 0;

        try
        {
            // Read current parameters from device
            Console.WriteLine("Reading parameters from device...");
            var deviceParams = await telemetry.RequestAllParamsAsync(ct);
            var deviceMap = deviceParams.ToDictionary(p => p.Name, p => p.Value);
            Console.WriteLine($"Device has {deviceMap.Count} parameters");

            // Compare and find differences
            var toUpload = new List<Parameter>();
            var missing = new List<Parameter>();

            foreach (var param in parameters)
            {
                if (!deviceMap.TryGetValue(param.Name, out var deviceValue))
                {
                    missing.Add(param);
                    skippedMissing++;
                    continue;
                }

                if (Math.Abs(deviceValue - param.Value) < 0.001f)
                {
                    skippedSame++;
                    continue;
                }

                toUpload.Add(param);
            }

            Console.WriteLine($"To upload: {toUpload.Count}, already same: {skippedSame}, not on device: {missing.Count}");

            if (toUpload.Count == 0 && missing.Count == 0)
            {
                return new ParameterUploadResult
                {
                    Success = true,
                    Sent = 0,
                    Failed = 0,
                    Total = parameters.Count
                };
            }

            // Multi-pass upload with reboot between passes
            var pending = new List<Parameter>(toUpload);

            for (var pass = 1; pass <= WriteParamsPasses && pending.Count > 0; pass++)
            {
                Console.WriteLine($"Pass {pass}: sending {pending.Count} parameters");

                var failed = new List<Parameter>();

                foreach (var param in pending)
                {
                    ct.ThrowIfCancellationRequested();

                    var confirmed = await telemetry.SetParamAsync(param, ct);
                    if (confirmed)
                    {
                        sent++;
                        Console.WriteLine($"  OK: {param.Name} = {param.Value}");
                    }
                    else
                    {
                        failed.Add(param);
                        Console.WriteLine($"  FAIL: {param.Name} = {param.Value}");
                    }

                    progress.Report((sent + skippedSame + failed.Count + skippedMissing, parameters.Count));
                    await Task.Delay(50, ct);
                }

                pending = failed;

                if (pending.Count == 0)
                    break;

                // Try missing params too — they might appear after reboot
                if (pass == 1 && missing.Count > 0)
                {
                    pending.AddRange(missing);
                    skippedMissing = 0;
                    Console.WriteLine($"Added {missing.Count} missing params for retry after reboot");
                }

                if (pass < WriteParamsPasses)
                {
                    Console.WriteLine($"Pass {pass} done. Rebooting to apply parameters...");

                    // Reboot: bootloader → boot → reconnect
                    await RebootAsync(BootMode.Bootloader, ct);
                    await bootloader.BootAsync(ct);
                    port.Close();

                    var portsAfterBoot = portScanner.GetAvailablePorts();
                    var normalPort = await portScanner.WaitForNewPortAsync(
                        portsAfterBoot,
                        TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                        ct);

                    var targetPort = !string.IsNullOrWhiteSpace(normalPort)
                        ? normalPort
                        : session!.Port;

                    port.Open(targetPort, session!.BaudRate);
                    await telemetry.SendHeartbeatAsync(ct);
                    await WaitForDeviceHeartbeatAsync(ct);

                    currentMode = BootMode.Normal;
                    UpdateSessionPortAndState(targetPort, DeviceState.Connected);

                    Console.WriteLine($"Reconnected on {targetPort}. Refreshing device params...");

                    // Re-read params after reboot — new params might be available
                    deviceParams = await telemetry.RequestAllParamsAsync(ct);
                    deviceMap = deviceParams.ToDictionary(p => p.Name, p => p.Value);

                    // Re-check pending — some might now match after reboot
                    var stillPending = new List<Parameter>();
                    foreach (var param in pending)
                    {
                        if (deviceMap.TryGetValue(param.Name, out var currentValue)
                            && Math.Abs(currentValue - param.Value) < 0.001f)
                        {
                            sent++;
                            Console.WriteLine($"  Applied after reboot: {param.Name}");
                        }
                        else
                        {
                            stillPending.Add(param);
                        }
                    }

                    pending = stillPending;
                    Console.WriteLine($"After reboot: {pending.Count} still pending");
                }
            }

            // Count remaining missing as skipped
            skippedMissing = pending.Count(p => !deviceMap.ContainsKey(p.Name));
            var finalFailed = pending.Count - skippedMissing;

            Console.WriteLine($"Result: sent={sent}, same={skippedSame}, missing={skippedMissing}, failed={finalFailed}");

            return new ParameterUploadResult
            {
                Success = pending.Count == 0,
                Sent = sent,
                Failed = finalFailed,
                ReadOnly = 0,
                Hidden = skippedMissing,
                Total = parameters.Count
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ParameterUploadResult
            {
                Success = false,
                Sent = sent,
                Failed = parameters.Count - sent - skippedSame - skippedMissing,
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

                // If no new port appeared, maybe original port came back
                if (string.IsNullOrWhiteSpace(newPort))
                {
                    await Task.Delay(2000, ct);
                    var currentPorts =  portScanner.GetAvailablePorts();
                    if (currentPorts.Contains(session!.Port))
                        newPort = session.Port;
                }

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

                await bootloader.GetDeviceInfoAsync(ct);

                currentMode = BootMode.Bootloader;
                UpdateSessionPortAndState(newPort, DeviceState.InBootloader);

                return new BootResult
                {
                    Success = true,
                    NewPort = newPort
                };
            }

            // Normal mode: boot from bootloader, wait for device to reappear
            await this.bootloader.BootAsync(ct);
            this.port.Close();

            var portsBeforeNormal = this.portScanner.GetAvailablePorts();
            var newNormalPort = await this.portScanner.WaitForNewPortAsync(
                portsBeforeNormal,
                TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                ct);

            var targetPort = string.IsNullOrWhiteSpace(newNormalPort)
                ? session!.Port
                : newNormalPort;

            this.port.Open(targetPort, session!.BaudRate);

            try
            {
                await this.telemetry.SendHeartbeatAsync(ct);
                await WaitForDeviceHeartbeatAsync(ct);
            }
            catch
            {
                this.port.Close();
                throw;
            }

            this.currentMode = BootMode.Normal;
            UpdateSessionPortAndState(targetPort, DeviceState.Connected);

            return new BootResult
            {
                Success = true,
                NewPort = targetPort
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
        port.Close();

        var portsAfterBoot = portScanner.GetAvailablePorts();
        var normalPort = await portScanner.WaitForNewPortAsync(
            portsAfterBoot,
            TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
            ct);

        var targetPort = !string.IsNullOrWhiteSpace(normalPort)
            ? normalPort
            : session!.Port;

        port.Open(targetPort, session!.BaudRate);
        await telemetry.SendHeartbeatAsync(ct);
        await WaitForDeviceHeartbeatAsync(ct);

        this.currentMode = BootMode.Normal;
        UpdateSessionPortAndState(targetPort, DeviceState.Connected);

        Console.WriteLine($"Rebooted. Reconnected on {targetPort}");
    }

    /// <summary>
    /// Closes the serial port and releases resources.
    /// Resets the session and mode to initial state.
    /// </summary>
    public Task DisconnectAsync()
    {
        if (this.port.IsOpen)
        {
            this.port.Close();
        }

        session = null;
        this.currentMode = BootMode.Normal;
        return Task.CompletedTask;
    }

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
    /// Switches device mode if current mode doesn't match the required one.
    /// </summary>
    private async Task EnsureModeAsync(BootMode mode, CancellationToken ct)
    {
        if (this.currentMode == mode)
        {
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
    /// Updates the session state, preserving all other fields.
    /// </summary>
    private void UpdateSessionState(DeviceState state)
    {
        if (session is null)
        {
            return;
        }

        session = new DeviceSession
        {
            Id = session.Id,
            Port = session.Port,
            BaudRate = session.BaudRate,
            State = state,
            ConnectedAt = session.ConnectedAt
        };
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
            ConnectedAt = session.ConnectedAt
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
                aligned[i] = 0xFF;
        }

        // CRC over firmware bytes
        uint state = Crc32(aligned, 0);

        // Continue CRC over remaining flash (filled with 0xFF)
        for (var i = aligned.Length; i < flashSize - 1; i += 4)
            state = Crc32(CrcPad, state);

        return state;
    }

    private static uint Crc32(byte[] data, uint state)
    {
        foreach (var b in data)
        {
            var index = (state ^ b) & 0xFF;
            state = CrcTable[index] ^ (state >> 8);
        }
        return state;
    }
}
