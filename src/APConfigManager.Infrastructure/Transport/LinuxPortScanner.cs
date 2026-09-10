using System.Text.RegularExpressions;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace APConfigManager.Infrastructure.Transport
{
    /// <summary>
    /// Linux implementation of IPortScanner based on /dev/serial/by-id symlinks
    /// (and /sys for VID/PID). MAVLink vs SLCAN is told apart by the USB
    /// interface number in the by-id name (ifXX), not by a text description.
    /// </summary>
    public class LinuxPortScanner : IPortScanner
    {
        private const string ByIdDir = "/dev/serial/by-id";

        // if00 = MAVLink, if02 = SLCAN on ArduPilot (Cube Orange+ and friends).
        private const string MavlinkInterface = "if00";
        private const string SlcanInterface   = "if02";

        private readonly ILogger<LinuxPortScanner> logger;

        public LinuxPortScanner(ILogger<LinuxPortScanner> logger)
        {
            this.logger = logger;
        }

        public List<string> GetAvailablePorts()
        {
            return EnumerateByIdLinks()
                .Select(link => link.DevPath)
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        public List<PortDescription> GetAvailablePortsDetailed()
        {
            var result = new List<PortDescription>();

            foreach (var link in EnumerateByIdLinks())
            {
                var (vid, pid) = ReadVidPid(link.DevPath);

                result.Add(new PortDescription
                {
                    Name = link.DevPath,
                    Description = link.ByIdName,
                    VendorId = vid,
                    ProductId = pid,
                    DeviceSerial = link.Serial,
                    IsMavlink = link.Interface == MavlinkInterface,
                    LocationPath = link.ByIdName
                });
            }

            return result
                .Where(IsVisiblePort)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();
        }

        public PortDescription? GetPortDescription(string portName)
        {
            return GetAvailablePortsDetailed()
                .FirstOrDefault(p => p.Name.Equals(portName, StringComparison.Ordinal));
        }

        // --- port-appearance helpers: same contract as Windows, polling by-id ---

        public async Task<string?> WaitForNewPortAsync(
            List<string> existingPorts, TimeSpan timeout, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            try
            {
                while (true)
                {
                    await Task.Delay(300, cts.Token);
                    var added = GetAvailablePorts().FirstOrDefault(p => !existingPorts.Contains(p));
                    if (added != null) return added;
                }
            }
            catch (OperationCanceledException) { return null; }
        }

        public async Task<string?> WaitForBootloaderPortAsync(
            string originalPort, TimeSpan timeout, CancellationToken ct)
        {
            var before = GetAvailablePorts();
            return await WaitForBootloaderPortAsync(originalPort, before, timeout, ct);
        }

        public async Task<string?> WaitForBootloaderPortAsync(
            string originalPort, List<string> portsBefore, TimeSpan timeout, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            try
            {
                while (true)
                {
                    await Task.Delay(300, cts.Token);
                    if (!GetAvailablePorts().Contains(originalPort)) break;
                }
                while (true)
                {
                    await Task.Delay(300, cts.Token);
                    var now = GetAvailablePorts();
                    var newPort = now.FirstOrDefault(p => !portsBefore.Contains(p));
                    if (newPort != null) return newPort;
                    if (now.Contains(originalPort)) return originalPort;
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Bootloader port did not appear within {Timeout}s (original {Port})",
                    timeout.TotalSeconds, originalPort);
                return null;
            }
        }

        public async Task<string?> WaitForMavlinkPortAsync(
            string deviceSerial, List<string> portsBefore, List<string> excludePorts,
            TimeSpan timeOut, CancellationToken ct)
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

                    if (hasSerial)
                    {
                        var bySerial = ports.FirstOrDefault(p =>
                            p.DeviceSerial.Equals(deviceSerial, StringComparison.OrdinalIgnoreCase) &&
                            p.IsMavlink);
                        if (bySerial != null) return bySerial.Name;
                    }

                    var newMav = ports.FirstOrDefault(p => p.IsMavlink && !portsBefore.Contains(p.Name));
                    if (newMav != null) return newMav.Name;

                    var anyNew = ports.FirstOrDefault(p => !portsBefore.Contains(p.Name));
                    if (anyNew != null) return anyNew.Name;
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("MAVLink port did not appear within {Timeout}s", timeOut.TotalSeconds);
                return null;
            }
        }

        // --- /dev/serial/by-id parsing ---

        private record ByIdLink(string ByIdName, string DevPath, string Serial, string Interface);

        private IEnumerable<ByIdLink> EnumerateByIdLinks()
        {
            if (!Directory.Exists(ByIdDir))
                yield break;

            foreach (var path in Directory.EnumerateFileSystemEntries(ByIdDir))
            {
                var name = Path.GetFileName(path);
                string devPath;
                try
                {
                    // resolve symlink -> /dev/ttyACMx
                    var target = File.ResolveLinkTarget(path, returnFinalTarget: true);
                    if (target is null) continue;
                    devPath = target.FullName;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to resolve by-id link {Link}", name);
                    continue;
                }

                // name looks like: usb-CubePilot_CubeOrange+_300019000651333233353830-if00
                var iface = "";
                var ifm = Regex.Match(name, @"-(if\d+)$");
                if (ifm.Success) iface = ifm.Groups[1].Value;

                var serial = "";
                var sm = Regex.Match(name, @"_([0-9A-Fa-f]{6,})(?:-if\d+)?$");
                if (sm.Success) serial = sm.Groups[1].Value;

                yield return new ByIdLink(name, devPath, serial, iface);
            }
        }

        private (string vid, string pid) ReadVidPid(string devPath)
        {
            // /sys/class/tty/ttyACM0/device/../../idVendor|idProduct
            try
            {
                var tty = Path.GetFileName(devPath); // ttyACM0
                var sysDevice = $"/sys/class/tty/{tty}/device";
                var usbDir = Directory.GetParent(Path.GetFullPath(sysDevice))?.Parent?.FullName;
                if (usbDir is null) return ("", "");

                var vidFile = Path.Combine(usbDir, "idVendor");
                var pidFile = Path.Combine(usbDir, "idProduct");
                var vid = File.Exists(vidFile) ? File.ReadAllText(vidFile).Trim() : "";
                var pid = File.Exists(pidFile) ? File.ReadAllText(pidFile).Trim() : "";
                return (vid, pid);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to read VID/PID for {Dev}", devPath);
                return ("", "");
            }
        }

        private static bool IsVisiblePort(PortDescription port)
        {
            // Hide SLCAN (if02) from the main list, mirroring the Windows behaviour.
            return port.LocationPath?.EndsWith($"-{SlcanInterface}", StringComparison.Ordinal) != true;
        }
    }
}
