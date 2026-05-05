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
                this.currentMode = BootMode.Bootloader;
            }
            else
            {
                this.port.Close();
                throw new DeviceConnectionException(
                    $"No response from device on port {port} (neither MAVLink nor bootloader).");
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
        await EnsureModeAsync(BootMode.Bootloader, ct);

        try
        {
            if (firmware.ImageBytes.Length == 0)
            {
                return new FlashResult
                {
                    Success = false,
                    ErrorMessage = "Firmware image is empty."
                };
            }

            var expectedCrc = CalculateCrc32(firmware.ImageBytes);

            progress.Report((0, "Erasing..."));
            await this.bootloader.SyncAsync(ct);
            await this.bootloader.ChipEraseAsync(ct);

            var bytesWritten = 0;
            var totalBytes = firmware.ImageBytes.Length;

            for (var offset = 0; offset < totalBytes; offset += ChunkSize)
            {
                ct.ThrowIfCancellationRequested();

                var size = Math.Min(ChunkSize, totalBytes - offset);
                var chunk = new byte[size];
                Array.Copy(firmware.ImageBytes, offset, chunk, 0, size);

                await this.bootloader.ProgramMultiAsync(chunk, ct);
                bytesWritten += size;

                var percent = (int)(80.0 * bytesWritten / totalBytes);
                progress.Report((percent, $"Writing {bytesWritten}/{totalBytes}"));
            }

            progress.Report((90, "Verifying..."));
            var crcOk = await this.bootloader.VerifyCrcAsync(expectedCrc, ct);
            if (!crcOk)
            {
                return new FlashResult
                {
                    Success = false,
                    BytesWritten = bytesWritten,
                    FirmwareVersion = firmware.Version,
                    ErrorMessage = "CRC verification failed."
                };
            }

            progress.Report((95, "Booting..."));
            await this.bootloader.BootAsync(ct);
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
            progress.Report((0, "Reading device info..."));
            await this.bootloader.GetDeviceInfoAsync(ct);

            progress.Report((10, "Erasing..."));
            await this.bootloader.SyncAsync(ct);
            await this.bootloader.ChipEraseAsync(ct);

            progress.Report((80, "Booting..."));
            await this.bootloader.BootAsync(ct);

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

        var pending = new List<Parameter>(parameters);
        var sent = 0;

        try
        {
            for (var pass = 1; pass <= WriteParamsPasses && pending.Count > 0; pass++)
            {
                var failed = new List<Parameter>();

                foreach (var parameter in pending)
                {
                    ct.ThrowIfCancellationRequested();

                    var confirmed = await this.telemetry.SetParamAsync(parameter, ct);
                    if (confirmed)
                    {
                        sent++;
                    }
                    else
                    {
                        failed.Add(parameter);
                    }

                    progress.Report((sent + failed.Count, parameters.Count));
                }

                pending = failed;
            }

            return new ParameterUploadResult
            {
                Success = pending.Count == 0,
                Sent = sent,
                Failed = pending.Count,
                Total = parameters.Count
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ParameterUploadResult
            {
                Success = false,
                Sent = sent,
                Failed = pending.Count,
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
            //if (mode == BootMode.Bootloader)
            //{
            //    await telemetry.RebootToBootloaderAsync(ct);
            //    port.Close();

            //    var newPort = await portScanner.WaitForBootloaderPortAsync(
            //        session!.Port,
            //        TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
            //        ct);

            //    if (string.IsNullOrWhiteSpace(newPort))
            //    {
            //        throw new DeviceConnectionException("Bootloader port not found.");
            //    }

            //    port.Open(newPort, ArduPilotConstants.BootloaderBaudRate);

            //    var syncOk = await bootloader.SyncAsync(ct);
            //    if (!syncOk)
            //    {
            //        port.Close();
            //        throw new DeviceConnectionException("Bootloader sync failed.");
            //    }

            //    // Read device info to complete bootloader initialization
            //    await bootloader.GetDeviceInfoAsync(ct);

            //    currentMode = BootMode.Bootloader;
            //    UpdateSessionPortAndState(newPort, DeviceState.InBootloader);

            //    return new BootResult
            //    {
            //        Success = true,
            //        NewPort = newPort
            //    };
            //}
            if (mode == BootMode.Bootloader)
            {
                Console.WriteLine("1. Sending reboot command...");
                await telemetry.RebootToBootloaderAsync(ct);

                Console.WriteLine("2. Closing port...");
                port.Close();

                Console.WriteLine("3. Waiting for bootloader port...");
                var newPort = await portScanner.WaitForBootloaderPortAsync(
                    session!.Port,
                    TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                    ct);

                Console.WriteLine($"4. Found port: {newPort}");
                if (string.IsNullOrWhiteSpace(newPort))
                {
                    throw new DeviceConnectionException("Bootloader port not found.");
                }

                Console.WriteLine($"5. Opening {newPort}...");
                port.Open(newPort, ArduPilotConstants.BootloaderBaudRate);

                Console.WriteLine("6. Syncing...");
                var syncOk = await bootloader.SyncAsync(ct);
                Console.WriteLine($"7. Sync result: {syncOk}");
                if (!syncOk)
                {
                    port.Close();
                    throw new DeviceConnectionException("Bootloader sync failed.");
                }

                Console.WriteLine("8. Getting device info...");
                await bootloader.GetDeviceInfoAsync(ct);
                Console.WriteLine("9. Done!");

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

    /// <summary>
    /// Calculates CRC-32/POSIX checksum used by STM32 bootloader for flash verification.
    /// </summary>
    private static uint CalculateCrc32(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        const uint polynomial = 0x04C11DB7;
        uint crc = 0x00000000;

        foreach (var value in data)
        {
            crc ^= (uint)value << 24;

            for (var i = 0; i < 8; i++)
            {
                if ((crc & 0x80000000) != 0)
                    crc = (crc << 1) ^ polynomial;
                else
                    crc <<= 1;
            }
        }

        return ~crc;
    }
}
