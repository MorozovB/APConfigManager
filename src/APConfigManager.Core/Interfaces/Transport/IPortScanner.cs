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

        Task<string?> WaitForBootloaderPortAsync(string originalPort, List<string> portsBefore, TimeSpan timeout, CancellationToken ct);
    }
}
