namespace APConfigManager.Infrastructure.Transport
{
        /// <summary>
    /// Read-only wrapper that guarantees fully-satisfied reads over an inner stream
    /// whose Read may return fewer bytes than requested (as System.IO.Ports does on
    /// Linux). MavlinkParse.ReadPacket reads the stream byte-by-byte and assumes the
    /// requested bytes are delivered, so a partial read desyncs the frame parser
    /// ("Unknown Packet"). This wrapper loops until the buffer is filled or the
    /// inner stream's ReadTimeout elapses (then throws TimeoutException, which the
    /// caller already handles).
    /// </summary>
    public sealed class BlockingReadStream : Stream
    {
        private readonly Stream _inner;

        public BlockingReadStream(Stream inner) => _inner = inner;

        public override int ReadByte()
        {
            var b = new byte[1];
            var n = Read(b, 0, 1);
            return n == 1 ? b[0] : -1;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (total < count)
            {
                int n;
                try
                {
                    n = _inner.Read(buffer, offset + total, count - total);
                }
                catch (TimeoutException)
                {
                    // Nothing arrived within ReadTimeout.
                    // If we already have some bytes, return them; otherwise rethrow
                    // so MavlinkParse/ReadMessageAsync treat it as a timeout.
                    if (total > 0) return total;
                    throw;
                }

                if (n == 0)
                {
                    if (total > 0) return total;
                    throw new TimeoutException("Serial stream returned no data.");
                }

                total += n;
            }
            return total;
        }

        // Pass-throughs / not supported (parser only reads).
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override void Flush() => _inner.Flush();
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

