using NC.DokanFS;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S_Drive
{
    public class StorjDriveContextMemoryMapped : IDokanFileContext
    {
        public IDokanDisk Disk { get; set; }

        public IDokanFile File { get; set; }

        public string FileName { get; set; }

        private string _tempFileName;

        public StorjDriveContextMemoryMapped(string fileName)
        {
            FileName = fileName;
            _tempFileName = Path.GetTempFileName();
        }

        public void Append(byte[] buffer)
        {
            using (var stream = new FileStream(_tempFileName, FileMode.Append))
            {
                stream.Write(buffer);
            };
        }

        public void Dispose()
        {
            Debug.WriteLine("Dispose tempfile");
        }

        public void Flush()
        {
            Debug.WriteLine("Flush tempfile");
        }

        public void Lock(long offset, long length)
        {
            throw new NotImplementedException();
        }

        public int Read(byte[] buffer, long offset)
        {
            using (var stream = new FileStream(_tempFileName, FileMode.Open))
            {
                return stream.Read(buffer, (int)offset, buffer.Length);
            };
        }

        public void SetLength(long length)
        {
        }

        public void Unlock(long offset, long length)
        {
            throw new NotImplementedException();
        }

        public void Write(byte[] buffer, long offset)
        {
            using (var stream = new FileStream(_tempFileName, FileMode.OpenOrCreate))
            {
                stream.Write(buffer, 0, buffer.Length);
            };
        }
    }
}
