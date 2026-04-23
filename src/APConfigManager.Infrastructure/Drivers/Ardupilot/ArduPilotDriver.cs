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
    private const int ChunkSize = 64;
    private const int HeartbeatTimeoutMs = 3000;
    private const int PortSwitchTimeoutSeconds = 20;
    private const int WriteParamsPasses = 3;

    private readonly ISerialPortAdapter _port;
    private readonly IBootloaderProtocol _bootloader;
    private readonly ITelemetryProtocol _telemetry;
    private readonly IPortScanner _portScanner;

    private DeviceSession? _session;
    private BootMode _currentMode = BootMode.Normal;

    /// <summary>
    /// Initializes the driver with protocol and transport dependencies.
    /// </summary>
    public ArduPilotDriver(
        ISerialPortAdapter port,
        IBootloaderProtocol bootloaderProtocol,
        ITelemetryProtocol telemetryProtocol,
        IPortScanner portScanner)
    {
        _port = port;
        _bootloader = bootloaderProtocol;
        _telemetry = telemetryProtocol;
        _portScanner = portScanner;
    }

    /// <summary>
    /// Opens the serial port and establishes MAVLink communication.
    /// Sends a heartbeat and waits for a response to confirm the device is present.
    /// Closes the port if connection fails.
    /// </summary>
    public async Task<DeviceSession> ConnectAsync(string port, int baudRate, CancellationToken ct)
    {
        _port.Open(port, baudRate);

        try
        {
            await _telemetry.SendHeartbeatAsync(ct);
            await WaitForDeviceHeartbeatAsync(ct);
        }
        catch
        {
            _port.Close();
            throw;
        }

        _currentMode = BootMode.Normal;
        _session = new DeviceSession
        {
            Id = Guid.NewGuid(),
            Port = port,
            BaudRate = baudRate,
            State = DeviceState.Connected,
            ConnectedAt = DateTime.UtcNow
        };

        return _session;
    }

    /// <summary>
    /// Retrieves device information from the bootloader.
    /// Switches to bootloader mode if not already in it.
    /// </summary>
    public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct)
    {
        EnsureConnected();
        await EnsureModeAsync(BootMode.Bootloader, ct);
        return await _bootloader.GetDeviceInfoAsync(ct);
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
            await _bootloader.ChipEraseAsync(ct);

            var bytesWritten = 0;
            var totalBytes = firmware.ImageBytes.Length;

            for (var offset = 0; offset < totalBytes; offset += ChunkSize)
            {
                ct.ThrowIfCancellationRequested();

                var size = Math.Min(ChunkSize, totalBytes - offset);
                var chunk = new byte[size];
                Array.Copy(firmware.ImageBytes, offset, chunk, 0, size);

                await _bootloader.ProgramMultiAsync(chunk, ct);
                bytesWritten += size;

                var percent = (int)(80.0 * bytesWritten / totalBytes);
                progress.Report((percent, $"Writing {bytesWritten}/{totalBytes}"));
            }

            progress.Report((90, "Verifying..."));
            var crcOk = await _bootloader.VerifyCrcAsync(expectedCrc, ct);
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
            await _bootloader.BootAsync(ct);
            _currentMode = BootMode.Normal;
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
            progress.Report((0, "Erasing..."));
            await _bootloader.ChipEraseAsync(ct);

            progress.Report((80, "Booting..."));
            await _bootloader.BootAsync(ct);

            _currentMode = BootMode.Normal;
            UpdateSessionState(DeviceState.Connected);
            progress.Report((100, "Done"));

            return new EraseResult
            {
                Success = true
            };
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
        return await _telemetry.RequestAllParamsAsync(ct);
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

                    var confirmed = await _telemetry.SetParamAsync(parameter, ct);
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
            if (mode == BootMode.Bootloader)
            {
                await _telemetry.RebootToBootloaderAsync(ct);
                _port.Close();

                var newPort = await _portScanner.WaitForBootloaderPortAsync(
                    _session!.Port,
                    TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                    ct);

                if (string.IsNullOrWhiteSpace(newPort))
                {
                    throw new DeviceConnectionException("Bootloader port not found.");
                }

                _port.Open(newPort, ArduPilotConstants.BootloaderBaudRate);

                var syncOk = await _bootloader.SyncAsync(ct);
                if (!syncOk)
                {
                    _port.Close();
                    throw new DeviceConnectionException("Bootloader sync failed.");
                }

                _currentMode = BootMode.Bootloader;
                UpdateSessionPortAndState(newPort, DeviceState.InBootloader);

                return new BootResult
                {
                    Success = true,
                    NewPort = newPort
                };
            }

            // Normal mode: boot from bootloader, wait for device to reappear
            await _bootloader.BootAsync(ct);
            _port.Close();

            var portsBeforeNormal = _portScanner.GetAvailablePorts();
            var newNormalPort = await _portScanner.WaitForNewPortAsync(
                portsBeforeNormal,
                TimeSpan.FromSeconds(PortSwitchTimeoutSeconds),
                ct);

            var targetPort = string.IsNullOrWhiteSpace(newNormalPort)
                ? _session!.Port
                : newNormalPort;

            _port.Open(targetPort, _session!.BaudRate);

            try
            {
                await _telemetry.SendHeartbeatAsync(ct);
                await WaitForDeviceHeartbeatAsync(ct);
            }
            catch
            {
                _port.Close();
                throw;
            }

            _currentMode = BootMode.Normal;
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
        return await _telemetry.GetFirmwareVersionAsync(ct);
    }

    /// <summary>
    /// Resets all parameters to factory defaults via MAVLink command.
    /// </summary>
    public async Task ResetParamsAsync(CancellationToken ct)
    {
        EnsureConnected();
        await EnsureModeAsync(BootMode.Normal, ct);
        await _telemetry.ResetParamsAsync(ct);
    }

    /// <summary>
    /// Closes the serial port and releases resources.
    /// Resets the session and mode to initial state.
    /// </summary>
    public Task DisconnectAsync()
    {
        if (_port.IsOpen)
        {
            _port.Close();
        }

        _session = null;
        _currentMode = BootMode.Normal;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Throws SessionException if no active session or port is closed.
    /// </summary>
    private void EnsureConnected()
    {
        if (_session is null || !_port.IsOpen)
        {
            throw new SessionException("No active session. Call ConnectAsync first.");
        }
    }

    /// <summary>
    /// Switches device mode if current mode doesn't match the required one.
    /// </summary>
    private async Task EnsureModeAsync(BootMode mode, CancellationToken ct)
    {
        if (_currentMode == mode)
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
        if (_session is null)
        {
            return;
        }

        _session = new DeviceSession
        {
            Id = _session.Id,
            Port = _session.Port,
            BaudRate = _session.BaudRate,
            State = state,
            ConnectedAt = _session.ConnectedAt
        };
    }

    /// <summary>
    /// Updates the session port and state, preserving all other fields.
    /// </summary>
    private void UpdateSessionPortAndState(string port, DeviceState state)
    {
        if (_session is null)
        {
            return;
        }

        _session = new DeviceSession
        {
            Id = _session.Id,
            Port = port,
            BaudRate = _session.BaudRate,
            State = state,
            ConnectedAt = _session.ConnectedAt
        };
    }

    /// <summary>
    /// Waits for a heartbeat response from the autopilot within timeout.
    /// Sends heartbeat and listens for any MAVLink response to confirm device is alive.
    /// Throws DeviceConnectionException if no response received.
    /// </summary>
    private async Task WaitForDeviceHeartbeatAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(HeartbeatTimeoutMs);

        try
        {
            while (true)
            {
                await _telemetry.SendHeartbeatAsync(cts.Token);
                await Task.Delay(500, cts.Token);

                var version = await _telemetry.GetFirmwareVersionAsync(cts.Token);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
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
