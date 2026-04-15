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
        public readonly ISerialPortAdapter port;


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
            port.Flush();

            // Actual sync attempts with retry
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                await port.WriteAsync(command, 0, command.Length, ct);
                try
                {
                    var response = new byte[2];
                    var bytesRead = 0;

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
                        port.Flush();
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                    // Ignore timeout and retry
                }
                port.Flush();
                await Task.Delay(100, ct);
            }

            return false;
        }

        /// <summary>
        /// Reads board ID, revision, flash size, and bootloader revision from the device.
        /// </summary>
        public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct)
        {
            var boardId = await ReadRegisterAsync(ArduPilotConstants.GET_DEVICE, ct);
            var boardRevision = await ReadRegisterAsync(ArduPilotConstants.GET_DEVICE, ct);
            var flashSize = await ReadRegisterAsync(ArduPilotConstants.GET_DEVICE, ct);
            var bootloaderRevision = await ReadRegisterAsync(ArduPilotConstants.GET_DEVICE, ct);

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
        /// Calculates CRC-32/POSIX checksum.
        /// </summary>
        private static uint CalculateCrc32(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            const uint polynomial = 0xEDB88320;
            uint crc = 0xFFFFFFFF;

            foreach (var value in data)
            {
                crc ^= value;
                for (int i = 0; i < 8; i++)
                {
                    var lsb = crc & 1;
                    crc >>= 1;
                    if (lsb != 0)
                    {
                        crc ^= polynomial;
                    }
                }
            }
            return ~crc;
        }

        /// <summary>
        /// Sends a GET_DEVICE command and reads a 4-byte unsigned integer response.
        /// </summary>
        private async Task<uint> ReadRegisterAsync(byte command, CancellationToken ct)
        {
            var request = new byte[] { command, ArduPilotConstants.EOC };
            await port.WriteAsync(request, 0, request.Length, ct);

            // Read 4 data bytes + 2 status bytes (INSYNC + OK)
            var response = new byte[6];
            var bytesRead = 0;

            while (bytesRead < 6)
            {
                var read = await port.ReadAsync(response, bytesRead, 6 - bytesRead, ct);
                if (read == 0)
                    throw new BootloaderException("Connection lost while reading device info.");

                bytesRead += read;
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
                throw new BootloaderException($"Bootloader out of sync: expected 0x{ArduPilotConstants.INSYNC:X2}, got 0x{insync:X2}.");

            if (status == ArduPilotConstants.INVALID)
                throw new BootloaderException("Bootloader rejected the command.");

            if (status != ArduPilotConstants.OK)
                throw new BootloaderException($"Unexpected bootloader status: 0x{status:X2}.");
        }
    }
}
