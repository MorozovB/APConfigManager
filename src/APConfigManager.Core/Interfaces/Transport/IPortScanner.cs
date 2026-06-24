using APConfigManager.Core.Models;

namespace APConfigManager.Core.Interfaces.Transport
{
    /// <summary>
    /// Discovery and monitoring of COM ports in the system(When the device reboots, the port will be change)
    /// </summary>
    public interface IPortScanner
    {
        /// <summary>
        /// Returns list of available COM-ports.
        /// </summary>
        /// <returns></returns>
        List<string> GetAvailablePorts();

        /// <summary>
        /// Waiting for a new port to appear (after reboot).
        /// </summary>
        Task<string?> WaitForNewPortAsync(List<string> existingPorts, TimeSpan timeout, CancellationToken ct);

        /// <summary>
        /// Waiting for the bootloader port (may differ from the original).
        /// </summary>
        Task<string?> WaitForBootloaderPortAsync(string originalPort, TimeSpan timeout, CancellationToken ct);

        /// <summary>
        /// Waiting for the bootloader port by comparing the list of ports before and after reboot (handles cases where the port name changes).
        /// </summary>
        Task<string?> WaitForBootloaderPortAsync(string originalPort, List<string> portsBefore, TimeSpan timeout, CancellationToken ct);

        /// <summary>
        /// Returns available COM ports with full USB device details (description, serial, VID/PID).
        /// </summary>
        List<PortDescription> GetAvailablePortsDetailed();

        /// <summary>
        /// Returns USB device details for a specific COM port.
        /// </summary>
        PortDescription? GetPortDescription(string portName);

        /// <summary>
        /// Waits for a MAVLink port belonging to the specified device (by USB serial) to appear after reboot.
        /// </summary>
        Task<string?> WaitForMavlinkPortAsync(
            string deviceSerial,
            List<string> portsBefore,
            List<string> excludePorts,
            TimeSpan timeOut,
            CancellationToken ct);
    }
}
