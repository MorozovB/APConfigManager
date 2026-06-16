using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;

namespace APConfigManager.Infrastructure.Transport
{
    public class PortScanner : IPortScanner
    {
        public List<string> GetAvailablePorts()
        {
            return SerialPort.GetPortNames()
                .OrderBy(ExtractPortNumber)
                .ToList();
        }

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

        public PortDescription? GetPortDescription(string portName)
        {
            return GetAvailablePortsDetailed()
                .FirstOrDefault(p => p.Name.Equals(portName, StringComparison.OrdinalIgnoreCase));
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
                    await Task.Delay(300, cts.Token);

                    var now = GetAvailablePorts();
                    var added = now.FirstOrDefault(p => !existingPorts.Contains(p));

                    if (added != null)
                        return added;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public async Task<string?> WaitForBootloaderPortAsync(
            string originalPort,
            TimeSpan timeout,
            CancellationToken ct)
        {
            var before = GetAvailablePorts();
            return await WaitForBootloaderPortAsync(originalPort, before, timeout, ct);
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
                while (true)
                {
                    await Task.Delay(300, cts.Token);

                    var now = GetAvailablePorts();
                    if (!now.Contains(originalPort))
                        break;
                }

                while (true)
                {
                    await Task.Delay(300, cts.Token);

                    var now = GetAvailablePorts();

                    var newPort = now.FirstOrDefault(p => !portsBefore.Contains(p));
                    if (newPort != null)
                        return newPort;

                    if (now.Contains(originalPort))
                        return originalPort;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public async Task<string?> WaitForMavlinkPortAsync(
            string deviceSerial,
            List<string> portsBefore,
            List<string> excludePorts,
            TimeSpan timeout,
            CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var hasSerial = !string.IsNullOrWhiteSpace(deviceSerial);

            try
            {
                while (true)
                {
                    await Task.Delay(500, cts.Token);

                    var ports = GetAvailablePortsDetailed()
                        .Where(p => !excludePorts.Contains(p.Name))
                        .ToList();

                    if (hasSerial)
                    {
                        var bySerial = ports.FirstOrDefault(p =>
                            p.DeviceSerial.Equals(deviceSerial, StringComparison.OrdinalIgnoreCase) &&
                            p.IsMavlink);

                        if (bySerial != null)
                            return bySerial.Name;
                    }

                    var newMav = ports.FirstOrDefault(p =>
                        p.IsMavlink && !portsBefore.Contains(p.Name));

                    if (newMav != null)
                        return newMav.Name;

                    var anyNew = ports.FirstOrDefault(p =>
                        !portsBefore.Contains(p.Name));

                    if (anyNew != null)
                        return anyNew.Name;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public async Task<string?> WaitForReconnectedPortAsync(
            string? usbLocationPath,
            string originalPort,
            IReadOnlyList<string> excludePorts,
            TimeSpan timeout,
            CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            Console.WriteLine($"[PortScanner] START WaitForReconnected");
            Console.WriteLine($"[PortScanner] originalPort   = {originalPort}");
            Console.WriteLine($"[PortScanner] usbLocation    = {usbLocationPath ?? "<null>"}");
            Console.WriteLine($"[PortScanner] excludePorts   = [{string.Join(", ", excludePorts)}]");
            Console.WriteLine($"[PortScanner] timeout        = {timeout}");

            try
            {
                // Phase 1
                Console.WriteLine($"[PortScanner] Phase 1: waiting for {originalPort} to disappear...");
                await WaitForPortToDisappearAsync(originalPort, cts.Token);
                Console.WriteLine($"[PortScanner] Phase 1: {originalPort} is gone");

                // Phase 2
                if (!string.IsNullOrWhiteSpace(usbLocationPath))
                {
                    Console.WriteLine($"[PortScanner] Phase 2: searching by LocationPath...");
                    var result = await WaitForPortByLocationAsync(usbLocationPath, excludePorts, cts.Token);
                    Console.WriteLine($"[PortScanner] Phase 2: found = {result ?? "<null>"}");
                    return result;
                }

                Console.WriteLine($"[PortScanner] Phase 2: no LocationPath, falling back to new port scan");
                var portsBefore = GetAvailablePorts()
                    .Where(p => !excludePorts.Contains(p, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                Console.WriteLine($"[PortScanner] portsBefore = [{string.Join(", ", portsBefore)}]");

                var fallback = await WaitForNewPortAsync(portsBefore, timeout, cts.Token);
                Console.WriteLine($"[PortScanner] fallback result = {fallback ?? "<null>"}");
                return fallback;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[PortScanner] TIMEOUT or CANCELLED");

                // Dump all ports visible at timeout moment
                Console.WriteLine($"[PortScanner] Ports visible right now:");
                foreach (var p in GetAvailablePortsDetailed())
                {
                    Console.WriteLine($"  {p.Name} | location={p.LocationPath} | desc={p.Description}");
                }

                return null;
            }
        }

        public async Task<string?> WaitForPortByLocationAsync(
            string usbLocation,
            List<string> excludePorts,
            TimeSpan timeout,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(usbLocation))
                return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                while (true)
                {
                    await Task.Delay(300, cts.Token);

                    var match = GetAvailablePortsDetailed()
                        .Where(p => !excludePorts.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                        .FirstOrDefault(p => string.Equals(
                            p.LocationPath, usbLocation,
                            StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                        return match.Name;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private async Task<string?> WaitForPortByLocationAsync(
            string usbLocation,
            IReadOnlyList<string> excludePorts,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(usbLocation))
                return null;

            while (true)
            {
                var match = GetAvailablePortsDetailed()
                    .Where(p => !excludePorts.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    .FirstOrDefault(p => string.Equals(
                        p.LocationPath, usbLocation,
                        StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    return match.Name;

                await Task.Delay(300, ct);
            }
        }

        private async Task WaitForPortToDisappearAsync(string portName, CancellationToken ct)
        {
            while (true)
            {
                var current = GetAvailablePorts();

                if (!current.Contains(portName, StringComparer.OrdinalIgnoreCase))
                    return;

                await Task.Delay(200, ct);
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
                        return arr[0];
                }
            }
            catch { }

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
                return true;

            return !port.Description.Contains(
                "SLCAN",
                StringComparison.OrdinalIgnoreCase);
        }

    }
}
