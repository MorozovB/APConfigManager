using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;

namespace APConfigManager.Infrastructure.Transport
{
    public class PortScanner : IPortScanner
    {
        /// <summary>
        /// Used to get a list of available COM ports on the system, sorted by port number.
        /// </summary>
        public List<string> GetAvailablePorts()
        {
            return SerialPort.GetPortNames()
                .OrderBy(ExtractPortNumber)
                .ToList();
        }

        /// <summary>
        /// Returns a list of available COM ports with detailed information, including description,
        /// vendor ID, product ID, device serial number, and location path.
        /// The list is filtered to exclude certain ports (e.g., SLCAN) and sorted by port number.
        /// </summary>
        public List<PortDescription> GetAvailablePortsDetailed()
        {
            var result = new List<PortDescription>();

            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

            foreach (var device in searcher.Get())
            {
                var name = device["Name"]?.ToString() ?? "";
                var pnpId = device["PNPDeviceID"]?.ToString() ?? "";

                var m = Regex.Match(name, @"\((COM\d+)\)");
                if (!m.Success) continue;

                var portName = m.Groups[1].Value;
                var description = Regex.Replace(name, @"\s*\(COM\d+\)\s*", "");

                var vendorId = "";
                var productId = "";
                var serial = "";

                var parts = pnpId.Split('\\');
                if (parts.Length >= 3 && parts[0] == "USB")
                {
                    var vid = Regex.Match(parts[1], @"VID_([0-9A-Fa-f]+)");
                    var pid = Regex.Match(parts[1], @"PID_([0-9A-Fa-f]+)");

                    if (vid.Success) vendorId = vid.Groups[1].Value;
                    if (pid.Success) productId = pid.Groups[1].Value;

                    serial = parts[2];
                }

                result.Add(new PortDescription
                {
                    Name = portName,
                    Description = description,
                    VendorId = vendorId,
                    ProductId = productId,
                    DeviceSerial = serial,
                    IsMavlink = description.Contains("Mavlink", StringComparison.OrdinalIgnoreCase),
                    LocationPath = GetLocationPath(device)
                });
            }

            return result
                .Where(IsVisiblePort)
                .OrderBy(p => ExtractPortNumber(p.Name))
                .ToList();
        }

        /// <summary>
        /// Gets the detailed description of a specific COM port by its name. Returns null if the port is not found.
        /// </summary>
        public PortDescription? GetPortDescription(string portName)
        {
            return GetAvailablePortsDetailed()
                .FirstOrDefault(p => p.Name.Equals(portName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Waits for a new COM port to appear that is not in the list of existing ports.
        /// </summary>
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
                    await Task.Delay(300, cts.Token);

                    var now = GetAvailablePorts();
                    var added = now.FirstOrDefault(p => !existingPorts.Contains(p));

                    if (added != null)
                    {
                        return added;
                    }    
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Waits for a bootloader COM port to appear, replacing the original port if it disappears.
        /// </summary>
        public async Task<string?> WaitForBootloaderPortAsync(
            string originalPort,
            TimeSpan timeout,
            CancellationToken ct)
        {
            var before = GetAvailablePorts();
            return await WaitForBootloaderPortAsync(originalPort, before, timeout, ct);
        }

        /// <summary>
        /// Waits for a bootloader COM port to appear, comparing the list of ports before and after the original port disappears.
        /// </summary>
        /// <param name="originalPort">The original port to monitor.</param>
        /// <param name="portsBefore">The list of ports available before the operation.</param>
        /// <param name="timeout">The maximum time to wait for a port to appear.</param>
        /// <param name="ct">The cancellation token to observe.</param>
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
                // Wait for the original port to disappear
                while (true)
                {
                    await Task.Delay(300, cts.Token);
                    var now = GetAvailablePorts();

                    if (!now.Contains(originalPort))
                    {
                        break;
                    }
                }

                // Wait for a new port to appear or the original port to reappear
                while (true)
                {
                    await Task.Delay(300, cts.Token);

                    var now = GetAvailablePorts();
                    var newPort = now.FirstOrDefault(p => !portsBefore.Contains(p));

                    if (newPort != null)
                    {
                        return newPort;
                    }

                    if (now.Contains(originalPort))
                    {
                        return originalPort;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }


        /// <summary>
        /// Waits for a MAVLink port to appear, optionally filtering by device serial and excluding specific ports.
        /// </summary>
        /// <param name="deviceSerial">The serial number of the device to match.</param>
        /// <param name="portsBefore">The list of ports available before the operation.</param>
        /// <param name="excludePorts">The list of ports to exclude from consideration.</param>
        /// <param name="timeOut">The maximum time to wait for a port to appear.</param>
        public async Task<string?> WaitForMavlinkPortAsync(
            string deviceSerial,
            List<string> portsBefore,
            List<string> excludePorts,
            TimeSpan timeOut,
            CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeOut);

            var hasSerial = !string.IsNullOrWhiteSpace(deviceSerial);

            try
            {
                while (true)
                {
                    await Task.Delay(500, cts.Token);

                    var ports = GetAvailablePortsDetailed()
                        .Where(p => !excludePorts.Contains(p.Name))
                        .ToList();

                    // Try to find a port that matches the device serial and is a MAVLink port
                    if (hasSerial)
                    {
                        var bySerial = ports.FirstOrDefault(p =>
                            p.DeviceSerial.Equals(deviceSerial, StringComparison.OrdinalIgnoreCase) &&
                            p.IsMavlink);

                        if (bySerial != null)
                        {
                            return bySerial.Name;
                        }
                    }

                    // If no serial match, try to find any new MAVLink port that wasn't in the list before
                    var newMav = ports.FirstOrDefault(p =>
                        p.IsMavlink && !portsBefore.Contains(p.Name));

                    if (newMav != null)
                    {
                        return newMav.Name;
                    }

                    // If still no match, just return any new port that wasn't in the list before
                    var anyNew = ports.FirstOrDefault(p =>
                        !portsBefore.Contains(p.Name));

                    if (anyNew != null)
                    {
                        return anyNew.Name;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private static string GetLocationPath(ManagementBaseObject device)
        {
            try
            {
                var mo = (ManagementObject)device;
                var inParams = mo.GetMethodParameters("GetDeviceProperties");
                inParams["devicePropertyKeys"] = new[] { "DEVPKEY_Device_LocationPaths" };

                using var outParams = mo.InvokeMethod("GetDeviceProperties", inParams, null);

                if (outParams?["deviceProperties"] is ManagementBaseObject[] props &&
                    props.Length > 0)
                {
                    if (props[0]["Data"] is string[] arr && arr.Length > 0)
                    {
                        return arr[0];
                    }
                }

            }
            catch
            {
                // Ignore exceptions and return an empty string if unable to retrieve the location path
            }

            return "";
        }

        private static int ExtractPortNumber(string port)
        {
            var m = Regex.Match(port, @"COM(\d+)");

            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        private static bool IsVisiblePort(PortDescription port)
        {
            if (string.IsNullOrWhiteSpace(port.Description))
            {
                return true;
            }

            return !port.Description.Contains(
                "SLCAN",
                StringComparison.OrdinalIgnoreCase);
        }

    }
}
