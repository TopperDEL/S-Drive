using NC.DokanFS;
using S_Drive.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uplink.NET.Interfaces;
using uplink.NET.Models;

namespace S_Drive
{
    internal class StorjDriveContext : IDokanFileContext
    {
        private static object ContextDictionaryLockObject = new object();

        private static Dictionary<string, StorjDriveContext> _activeContexts = new Dictionary<string, StorjDriveContext>();

        public static StorjDriveContext GetOrCreateContext(IDokanDisk disk, string path, IObjectService objectService, Bucket bucket, bool fileExists)
        {
            lock (ContextDictionaryLockObject)
            {
                if (_activeContexts.ContainsKey(path))
                {
                    return _activeContexts[path];
                }
                else
                {
                    StorjDriveContext context = new StorjDriveContext(objectService, bucket, path, fileExists);
                    context.Disk = disk;
                    context.File = new StorjFile { Id = path, FileInformation = new DokanNet.FileInformation { FileName = path, CreationTime = DateTime.Now } };
                    _activeContexts.Add(path, context);

                    return context;
                }
            }
        }

        public static void ReleaseContext(string path)
        {
            lock (ContextDictionaryLockObject)
            {
                if (_activeContexts.ContainsKey(path))
                {
                    _activeContexts.Remove(path);
                }
            }
        }

        public static List<FolderContent> GetFilesWithContext()
        {
            lock (ContextDictionaryLockObject)
            {
                return _activeContexts.Select(s => new FolderContent(s.Key, s.Value.File.FileInformation.CreationTime.Value, s.Value.File.FileInformation.Length)).ToList();
            }
        }

        public IDokanDisk Disk { get; private set; }

        public IDokanFile File { get; private set; }

        public string FileName { get; private set; }

        private ChunkedUploadOperation _uploadOperation;
        private readonly IObjectService _objectService;
        private readonly Bucket _bucket;
        private Stream _stream;
        private bool _needsFlushing;

        public event EventHandler FileChanged;

        public StorjDriveContext(IObjectService objectService, Bucket bucket, string fileName, bool fileExists)
        {
            _objectService = objectService;
            _bucket = bucket;
            FileName = fileName;
            if (fileExists)
            {
                var obj = _objectService.GetObjectAsync(bucket, FileName).Result;
                _stream = new SeekableBufferedStream(obj.SystemMetadata.ContentLength, new DownloadStream(bucket, (int)obj.SystemMetadata.ContentLength, obj.Key));
            }
            else
            {
                _stream = new MemoryStream();
            }
        }

        public void Append(byte[] buffer)
        {
            _stream.Write(buffer, (int)_stream.Position, buffer.Length);
            _needsFlushing = true;
        }

        public void Dispose()
        {
            Flush();
            ReleaseContext(FileName);

            //Notwendig, aber kollidiert noch mit READS - der Stream wird unter dem Arsch weggezogen
            //if (_stream != null)
            //{
            //    _stream.Dispose();
            //    _stream = null;
            //}

            FileChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Flush()
        {
            if (_stream is MemoryStream)
            {
                var uploadOperation = _objectService.UploadObjectAsync(_bucket, FileName, new UploadOptions(), ((MemoryStream)_stream).ToArray(),false).Result;
                uploadOperation.StartUploadAsync().Wait();

                _needsFlushing = false;
                FileChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Lock(long offset, long length)
        {
            throw new System.IO.IOException();
        }

        public int Read(byte[] buffer, long offset)
        {
            if (_stream == null)
                return 0;

            _stream.Position = offset;
            int bytesRead = 0;
            try
            {
                if (_stream.Length > 0 && _stream.Length < offset + buffer.Length)
                    bytesRead = _stream.Read(buffer, 0, (int)(_stream.Length - offset));
                else
                    bytesRead = _stream.Read(buffer, 0, buffer.Length);
            }
            catch { }

            return bytesRead;
        }

        public void SetLength(long length)
        {
            var fi = new DokanNet.FileInformation
            {
                FileName = FileName,
                Length = length,
                CreationTime = DateTime.Now //Todo
            };

            if (File != null)
            {
                File.FileInformation = fi;
            }
        }

        public void Unlock(long offset, long length)
        {
            throw new System.IO.IOException();
        }

        public void Write(byte[] buffer, long offset)
        {
            _stream.Position = offset;
            _stream.Write(buffer, 0, buffer.Length);
            _needsFlushing = true;
            
            FileChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
