using System.IO.Ports;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Transport;

namespace APConfigManager.Infrastructure.Transport
{
    /// <summary>
    /// Discovers available COM ports and monitors for new port appearance.
    /// </summary>
    public class PortScanner : IPortScanner
    {
        /// <summary>
        /// Returns a sorted list of available COM ports, excluding Bluetooth ports.
        /// </summary>
        public List<string> GetAvailablePorts()
        {
            string[] ports = SerialPort.GetPortNames();

            return ports
                .Select(port => port.ToString())
                .Distinct()
                .ToList();
        }

        public async Task<string?> WaitForNewPortAsync(
            List<string> existingPorts,
            TimeSpan timeout,
            CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                while (true)
                {
                    await Task.Delay(500, cts.Token);

                    var current = GetAvailablePorts();
                    var newPort = current.FirstOrDefault(p => !existingPorts.Contains(p));

                    if (newPort is not null)
                        return newPort;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout expired, not user cancellation
                return null;
            }
        }

        /// <summary>
        /// Waits for a bootloader port to appear after device reboot.
        /// </summary>
        public async Task<string?> WaitForBootloaderPortAsync(
            string originalPort,
            TimeSpan timeout,
            CancellationToken ct)
        {
            var portsBefore = GetAvailablePorts();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                while (true)
                {
                    await Task.Delay(300, cts.Token);

                    var current = GetAvailablePorts();

                    // New port that didn't exist before
                    var newPort = current.FirstOrDefault(p => !portsBefore.Contains(p));
                    if (newPort is not null)
                        return newPort;

                    // Original port disappeared — wait for it to come back
                    if (!current.Contains(originalPort))
                    {
                        while (true)
                        {
                            await Task.Delay(300, cts.Token);

                            current = GetAvailablePorts();

                            if (current.Contains(originalPort))
                                return originalPort;

                            newPort = current.FirstOrDefault(p => !portsBefore.Contains(p));
                            if (newPort is not null)
                                return newPort;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout expired, not user cancellation
                return null;
            }
        }

    }
}
