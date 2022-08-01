using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace S_Drive
{
    //Based on: https://github.com/mlhpdx/seekable-s3-stream
    public class SeekableBufferedStream : Stream
    {
        const long DEFAULT_PAGE_LENGTH = 25 * 1024 * 1024;
        const int DEFAULT_MAX_PAGE_COUNT = 20;

        internal class MetaData
        {
            public long Length = 0;
            public long Position = 0;

            public long PageSize = DEFAULT_PAGE_LENGTH;
            public long MaxPages = DEFAULT_MAX_PAGE_COUNT;

            public Dictionary<long, byte[]> Pages = new Dictionary<long, byte[]>(DEFAULT_MAX_PAGE_COUNT);
            public Dictionary<long, long> HotList = new Dictionary<long, long>(DEFAULT_MAX_PAGE_COUNT);
        }

        MetaData _metadata = null;
        Stream _baseStream;

        public long TotalRead { get; private set; }
        public long TotalLoaded { get; private set; }

        public SeekableBufferedStream(long contentLength, Stream baseStream, long page = DEFAULT_PAGE_LENGTH, int maxpages = DEFAULT_MAX_PAGE_COUNT)
        {
            _metadata = new MetaData()
            {
                PageSize = page,
                MaxPages = maxpages,
            };

            _metadata.Length = contentLength;
            _baseStream = baseStream;
        }

        protected override void Dispose(bool disposing) => base.Dispose(disposing);

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;

        public override long Length => _metadata.Length;
        public override long Position
        {
            get => _metadata.Position;
            set => Seek(value, value >= 0 ? SeekOrigin.Begin : SeekOrigin.End);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_metadata.Position < 0 || _metadata.Position >= Length)
                return 0;

            long p = _metadata.Position;
            do
            {
                long i = p / _metadata.PageSize;
                long o = p % _metadata.PageSize;

                byte[] b = null;
                if (_metadata.Pages.ContainsKey(i))
                {
                    b = _metadata.Pages[i];
                }

                if (b == null)
                {
                    // if we have too many pages, drop the coolest
                    while (_metadata.Pages.Count >= _metadata.MaxPages)
                    {
                        var trim = _metadata.Pages.OrderBy(kv => _metadata.HotList[kv.Key]).First().Key;
                        _metadata.Pages.Remove(trim);
                    }

                    long s = i * _metadata.PageSize;
                    long e = s + Math.Min(_metadata.PageSize, _metadata.Length - s); // read in a single page (we're looping)

                    _metadata.Pages[i] = (b = new byte[e - s]);
                    if (!_metadata.HotList.ContainsKey(i))
                        _metadata.HotList[i] = 1;

                    int read = 0;
                    _baseStream.Seek(offset, SeekOrigin.Begin);
                    read = _baseStream.Read(b, 0, b.Length);
                    
                    TotalLoaded += read;
                    Debug.WriteLine("Total loaded: " + TotalLoaded);
                }
                else _metadata.HotList[i] += 1;

                long l = Math.Min(b.Length - o, count);
                Array.Copy(b, (int)o, buffer, offset, (int)l);
                offset += (int)l;
                count -= (int)l;
                p += (int)l;
            } while (count > 0 && p < _metadata.Length);

            long c = p - _metadata.Position;
            TotalRead += c;
            _metadata.Position = p;
            return (int)c;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newpos = _metadata.Position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newpos = offset; // offset must be positive
                    break;
                case SeekOrigin.Current:
                    newpos += offset; // + or -
                    break;
                case SeekOrigin.End:
                    newpos = _metadata.Length - Math.Abs(offset); // offset must be negative?
                    break;
            }
            if (newpos < 0 || newpos > _metadata.Length)
                throw new InvalidOperationException("Stream position is invalid.");
            return _metadata.Position = newpos;
        }

        public override void SetLength(long value) => throw new NotImplementedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override void Flush() => throw new NotImplementedException();
    }
}
