using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;

namespace APConfigManager.Infrastructure.Drivers.Ardupilot
{
    /// <summary>
    /// STM32 bootloader protocol implementation for ArduPilot boards.
    /// Handles low-level sync, erase, program, and verify operations.
    /// </summary>
    public class StmBootloaderProtocol : IBootloaderProtocol
    {
        private readonly ISerialPortAdapter port;


        public StmBootloaderProtocol(ISerialPortAdapter port)
        {
            this.port = port;
        }

        /// <summary>
        /// Synchronizes with the bootloader by sending GET_SYNC + EOL.
        /// Sends preliminary sync commands to reset any incomplete dialog.
        /// </summary>
        public async Task<bool> SyncAsync(CancellationToken ct)
        {
            var command = new byte[] { ArduPilotConstants.GET_SYNC, ArduPilotConstants.EOC };

            // Send 3 preliminary syncs to reset any incomplete bootloader dialog
            for (var i = 0; i < 3; i++)
            {
                await port.WriteAsync(command, 0, command.Length, ct);
            }

            await Task.Delay(100, ct);
            port.Purge();

            // Actual sync attempts with retry
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                await port.WriteAsync(command, 0, command.Length, ct);
                try
                {
                    var response = new byte[2];
                    var bytesRead = 0;

                    // Wait for response with a timeout. While loop to read exactly 2 bytes, handling partial reads.
                    while (bytesRead < 2)
                    {
                        var read = await port.ReadAsync(response, bytesRead, 2 - bytesRead, ct);
                        if (read == 0)
                        {
                            break;
                        }
                        bytesRead += read;
                    }

                    if (bytesRead == 2
                        && response[0] == ArduPilotConstants.INSYNC
                        && response[1] == ArduPilotConstants.OK)
                    {
                        await Task.Delay(100, ct);
                        port.Purge();
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                    // Ignore timeout and retry
                }

                port.Purge();
                await Task.Delay(100, ct);
            }

            return false;
        }

        /// <summary>
        /// Reads board ID, revision, flash size, and bootloader revision from the device.
        /// </summary>
        public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct)
        {
            if (!await SyncAsync(ct))
                throw new BootloaderException("Bootloader sync failed before reading device info.");

            var boardId = await ReadRegisterAsync(ArduPilotConstants.GET_DEVICE, ArduPilotConstants.InfoBoardId, ct);
            var boardRevision = await ReadRegisterAsync(ArduPilotConstants.GET_DEVICE, ArduPilotConstants.InfoBoardRev, ct);
            var flashSize = await ReadRegisterAsync(ArduPilotConstants.GET_DEVICE, ArduPilotConstants.InfoFlashSize, ct);
            var bootloaderRevision = await ReadRegisterAsync(ArduPilotConstants.GET_DEVICE, ArduPilotConstants.InfoBlRev, ct);

            return new DeviceInfo
            {
                BoardId = boardId,
                BoardRevision = boardRevision,
                FlashSize = flashSize,
                BootloaderRevision = bootloaderRevision
            };

        }


        /// <summary>
        /// Performs full chip erase. May take up to 30 seconds.
        /// </summary>
        public async Task ChipEraseAsync(CancellationToken ct)
        {
            var command = new byte[] { ArduPilotConstants.CHIP_ERASE, ArduPilotConstants.EOC };

            await port.WriteAsync(command, 0, command.Length, ct);

            var response = new byte[2];
            var bytesRead = 0;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ArduPilotConstants.EraseTimeoutMs);

            try
            {
                while (bytesRead < 2)
                {
                    var read = await port.ReadAsync(response, bytesRead, 2 - bytesRead, cts.Token);
                    if (read == 0)
                    {
                        throw new BootloaderException("Connection lost during chip erase.");
                    }
                    bytesRead += read;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new BootloaderException($"Chip erase timed out after {ArduPilotConstants.EraseTimeoutMs}");
            }

            CheckResponse(response);
        }

        /// <summary>
        /// Writes a single data chunk (up to 64 bytes) to flash memory.
        /// </summary>
        public async Task ProgramMultiAsync(byte[] data, CancellationToken ct)
        {
            if (data.Length > ArduPilotConstants.ProgMultiMaxSize)
            {
                throw new ArgumentException($"Chunk size {data.Length} exceeds maximum of {ArduPilotConstants.ProgMultiMaxSize} bytes.\"");
            }

            // Packet format: [PROG_MULTI, length, data..., EOC]
            var packet = new byte[data.Length + 3];
            packet[0] = ArduPilotConstants.PROG_MULTI;
            packet[1] = (byte)data.Length;
            Array.Copy(data, 0, packet, 2, data.Length);
            packet[^1] = ArduPilotConstants.EOC;

            await SendCommandAsync(packet, ct);
        }

        /// <summary>
        /// Verifies CRC-32 of the programmed flash area.
        /// </summary>
        public async Task<bool> VerifyCrcAsync(uint expectedCrc, CancellationToken ct)
        {
            var deviceCrc = await ReadRegisterAsync(ArduPilotConstants.GET_CRC, ct);
            return deviceCrc == expectedCrc;
        }

        /// <summary>
        /// Changes the bootloader communication baud rate.
        /// </summary>
        public async Task SetBaudRateAsync(int baudrate, CancellationToken ct)
        {
            if (baudrate <= 0)
            {
                throw new ArgumentException("Baud rate must be a positive integer.");
            }

            var baudBytes = BitConverter.GetBytes(baudrate);
            var command = new byte[6];
            command[0] = ArduPilotConstants.SET_BAUD;
            Array.Copy(baudBytes, 0, command, 1, baudBytes.Length);
            command[^1] = ArduPilotConstants.EOC;

            await SendCommandAsync(command, ct);

            port.ChangeBaudRate(baudrate);
        }

        /// <summary>
        /// Commands the bootloader to launch the flashed firmware.
        /// </summary>
        public async Task BootAsync(CancellationToken ct)
        {
            var command = new byte[] { ArduPilotConstants.BOOT, ArduPilotConstants.EOC };
            await SendCommandAsync(command, ct);
        }

        /// <summary>
        /// Sends a raw command to the bootloader.
        /// </summary>
        private async Task SendCommandAsync(byte[] command, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(command);

            await port.WriteAsync(command, 0, command.Length, ct);

            var response = new byte[2];
            var bytesRead = 0;

            while (bytesRead < response.Length)
            {
                var read = await port.ReadAsync(response, bytesRead, response.Length - bytesRead, ct);
                if (read == 0)
                    throw new BootloaderException("Connection lost while waiting for bootloader response.");
                bytesRead += read;
            }

            CheckResponse(response);
        }

        /// <summary>
        /// Sends a GET_DEVICE command and reads a 4-byte unsigned integer response.
        /// </summary>
        private async Task<uint> ReadRegisterAsync(byte command, byte infoType, CancellationToken ct)
        {
            var request = new byte[] { command, infoType, ArduPilotConstants.EOC };
            await port.WriteAsync(request, 0, request.Length, ct);

            var response = new byte[6];
            var bytesRead = 0;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(3000);

            try
            {
                while (bytesRead < 6)
                {
                    var read = await port.ReadAsync(response, bytesRead, 6 - bytesRead, cts.Token);
                    if (read == 0)
                        throw new BootloaderException("Connection lost while reading device info.");
                    bytesRead += read;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new BootloaderException($"Timeout reading device info. Got {bytesRead}/6 bytes.");
            }

            CheckResponse(response);
            return BitConverter.ToUInt32(response, 0);
        }

        /// <summary>
        /// Sends a command and reads a 4-byte unsigned integer response (no info_type).
        /// Used by GET_CRC.
        /// </summary>
        private async Task<uint> ReadRegisterAsync(byte command, CancellationToken ct)
        {
            var request = new byte[] { command, ArduPilotConstants.EOC };
            await port.WriteAsync(request, 0, request.Length, ct);

            var response = new byte[6];
            var bytesRead = 0;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(3000);

            try
            {
                while (bytesRead < 6)
                {
                    var read = await port.ReadAsync(response, bytesRead, 6 - bytesRead, cts.Token);
                    if (read == 0)
                        throw new BootloaderException("Connection lost.");
                    bytesRead += read;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new BootloaderException($"Timeout reading response. Got {bytesRead}/6 bytes.");
            }

            CheckResponse(response);
            return BitConverter.ToUInt32(response, 0);
        }


        /// <summary>
        /// Validates the bootloader response bytes.
        /// </summary>
        private void CheckResponse(byte[] response)
        {
            var insync = response[^2];
            var status = response[^1];

            if (insync != ArduPilotConstants.INSYNC)
            {
                throw new BootloaderException($"Bootloader out of sync: expected 0x{ArduPilotConstants.INSYNC:X2}, got 0x{insync:X2}.");
            }

            if (status == ArduPilotConstants.INVALID)
            {
                throw new BootloaderException("Bootloader rejected the command.");
            }

            if (status != ArduPilotConstants.OK)
            {
                throw new BootloaderException($"Unexpected bootloader status: 0x{status:X2}.");
            }
        }

        /// <summary>
        /// Reads CRC-32 from the device.
        /// </summary>
        public async Task<uint> GetCrcAsync(CancellationToken ct)
        {
            return await ReadRegisterAsync(ArduPilotConstants.GET_CRC, ct);
        }
    }
}
