namespace APConfigManager.Core.Interfaces.Transport
{
    /// <summary>
    /// Abstraction over the COM port. Allows to test protocols without real hardware (substituting the moq),
    /// </summary>
    public interface ISerialPortAdapter : IDisposable
    {
        bool IsOpen { get; }

        Stream BaseStream { get; }

        /// <summary>
        /// Open a port at the specified rate
        /// </summary>
        void Open(string port, int baudRate);

        /// <summary>
        /// Close a port.
        /// </summary>
        void Close();

        /// <summary>
        /// Reading async data.
        /// </summary>
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct);

        /// <summary>
        /// Writing async data
        /// </summary>
        Task WriteAsync(byte[] data, int offset, int coun, CancellationToken ct);

        /// <summary>
        /// Change baud rate for COM-port
        /// </summary>
        void ChangeBaudRate(int baudRate);

        /// <summary>
        /// Flush buffer.
        /// </summary>
        void Flush();
    }
}
