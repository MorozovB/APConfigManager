using System.IO.Ports;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Infrastructure.Drivers.Ardupilot;
using Microsoft.Extensions.Logging;

namespace APConfigManager.Infrastructure.Transport;

/// <summary>
/// Wrapper over System.IO.Ports.SerialPort with retry logic.
/// </summary>
public sealed class SerialPortAdapter : ISerialPortAdapter
{
    private SerialPort? _serialPort;

    private const int MaxRetries = 5;
    private const int RetryDelayMs = 500;
    private const int DefaultReadTimeout = 3000;
    private const int DefaultWriteTimeout = 3000;

    private readonly ILogger<SerialPortAdapter> logger;

    public SerialPortAdapter(ILogger<SerialPortAdapter> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Indicates whether the serial port is currently open.
    /// </summary>
    public bool IsOpen => _serialPort?.IsOpen ?? false;

    /// <summary>
    /// Provides access to the underlying stream for low-level operations.
    /// </summary>
    public Stream BaseStream
    {
        get
        {
            if (_serialPort is null || !_serialPort.IsOpen)
            {
                throw new InvalidOperationException("Serial port is not open.");
            }

            return _serialPort.BaseStream;
        }
    }

    /// <summary>
    /// Opens the serial port with retry logic.
    /// </summary>
    public void Open(string port, int baudRate)
    {
        Close();

        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _serialPort = new SerialPort(port, baudRate)
                {
                    ReadTimeout = DefaultReadTimeout,
                    WriteTimeout = DefaultWriteTimeout,
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serialPort.Open();

                logger.LogDebug("Port {Port} opened at {Baud}", port, baudRate);

                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                logger.LogWarning(ex, "Failed to open {Port}, attempt {Attempt}/{Max}", port, attempt, MaxRetries);
                 
                _serialPort?.Dispose();
                _serialPort = null;

                if (attempt < MaxRetries)
                {
                    Thread.Sleep(RetryDelayMs);
                }
            }
        }

        throw new DeviceConnectionException(
            $"Failed to open port {port} after {MaxRetries} attempts.",
            lastException!);
    }

    /// <summary>
    /// Closes the serial port and releases resources.
    /// </summary>
    public void Close()
    {
        if (_serialPort is null)
        {
            return;
        }

        try
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
                
        }
        finally
        {
            _serialPort.Dispose();
            _serialPort = null;
        }
    }

    /// <summary>
    /// Reads data from the port asynchronously via BaseStream.
    /// </summary>
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (_serialPort is null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open.");
        }
            
        return await _serialPort.BaseStream.ReadAsync(buffer, offset, count, ct);
    }

    /// <summary>
    /// Writes data to the port asynchronously via BaseStream.
    /// </summary>
    public async Task WriteAsync(byte[] data, int offset, int count, CancellationToken ct)
    {
        if (_serialPort is null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open.");
        }            

        await _serialPort.BaseStream.WriteAsync(data, offset, count, ct);
    }

    /// <summary>
    /// Changes the baud rate on an open port.
    /// </summary>
    public void ChangeBaudRate(int baudRate)
    {
        if (_serialPort is null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("Serial port is not open.");
        }
            
        _serialPort.BaudRate = baudRate;
    }

    /// <summary>
    /// Discards both input and output buffers.
    /// </summary>
    public void Purge()
    {
        if (_serialPort is null || !_serialPort.IsOpen)
        {
            return;
        }

        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();
    }

    /// <summary>
    /// Disposes the serial port if still open.
    /// </summary>
    public void Dispose()
    {
        Close();
    }
}
