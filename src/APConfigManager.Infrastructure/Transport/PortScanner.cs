using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;

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

        public async Task<string?> WaitForBootloaderPortAsync(
            string originalPort,
            List<string> portsBefore,
            TimeSpan timeout,
            CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                // Wait for original port to disappear first
                while (true)
                {
                    await Task.Delay(300, cts.Token);
                    var current = GetAvailablePorts();

                    if (!current.Contains(originalPort))
                        break;
                }

                // Now wait for a new port or original port to reappear
                while (true)
                {
                    await Task.Delay(300, cts.Token);
                    var current = GetAvailablePorts();

                    var newPort = current.FirstOrDefault(p => !portsBefore.Contains(p));
                    if (newPort is not null)
                        return newPort;

                    if (current.Contains(originalPort))
                        return originalPort;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }

        /// <summary>
        /// Queries Windows Management Instrumentation for COM port details.
        /// </summary>
        private static List<PortDescription> QueryPortsFromWmi()
        {
            var result = new List<PortDescription>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

                using var collection = searcher.Get();

                foreach (var device in collection)
                {
                    var name = device["Name"]?.ToString() ?? string.Empty;
                    var pnpDeviceId = device["PNPDeviceID"]?.ToString() ?? string.Empty;

                    // Extract COM port number from name: "Cube Orange+ Mavlink (COM9)" → "COM9"
                    var portMatch = Regex.Match(name, @"\((COM\d+)\)");
                    if (!portMatch.Success)
                        continue;

                    var portName = portMatch.Groups[1].Value;

                    // Description = name without "(COMx)" part, trimmed
                    var description = Regex.Replace(name, @"\s*\(COM\d+\)\s*", string.Empty).Trim();

                    // Parse PnPDeviceID: "USB\VID_2DAE&PID_1058\3456789ABC"
                    var vendorId = string.Empty;
                    var productId = string.Empty;
                    var deviceSerial = string.Empty;

                    var pnpParts = pnpDeviceId.Split('\\');
                    if (pnpParts.Length >= 3 && pnpParts[0].Equals("USB", StringComparison.OrdinalIgnoreCase))
                    {
                        var vidMatch = Regex.Match(pnpParts[1], @"VID_([0-9A-Fa-f]+)");
                        if (vidMatch.Success)
                            vendorId = vidMatch.Groups[1].Value;

                        var pidMatch = Regex.Match(pnpParts[1], @"PID_([0-9A-Fa-f]+)");
                        if (pidMatch.Success)
                            productId = pidMatch.Groups[1].Value;

                        deviceSerial = pnpParts[2];
                    }

                    var isMavlink = description.Contains("Mavlink", StringComparison.OrdinalIgnoreCase);

                    result.Add(new PortDescription
                    {
                        Name = portName,
                        Description = description,
                        DeviceSerial = deviceSerial,
                        VendorId = vendorId,
                        ProductId = productId,
                        IsMavlink = isMavlink
                    });
                }
            }
            catch (ManagementException)
            {
                // WMI unavailable — return empty, caller will fallback to SerialPort.GetPortNames()
            }

            result.Sort((a, b) =>
            {
                var numA = int.TryParse(a.Name.Replace("COM", ""), out var na) ? na : 0;
                var numB = int.TryParse(b.Name.Replace("COM", ""), out var nb) ? nb : 0;
                return numA.CompareTo(numB);
            });

            return result;
        }

    }
}
