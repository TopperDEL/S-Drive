using NC.DokanFS;
using S_Drive.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                    Debug.WriteLine("Create context for " + path);
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
                    Debug.WriteLine("Release context for " + path);
                    _activeContexts.Remove(path);
                }
            }
        }

        public static List<FolderContent> GetFilesWithContext()
        {
            lock (ContextDictionaryLockObject)
            {
                Debug.WriteLine("Listing contexts " + _activeContexts.Count);
                return _activeContexts.Select(s => new FolderContent(s.Key, s.Value.File.FileInformation.CreationTime.Value, s.Value.File.FileInformation.Length)).ToList();
            }
        }

        private StorjDriveContext()
        {

        }

        /// <summary>
        /// The dictionary maps a filename to it's currently running upload. IDokanFileInfo.Context was used here before, but that
        /// sometimes does not really keep track of that Upload leading to errors on file transfer. Therefore the mapping is hold
        /// seperately from Dokan.
        /// </summary>
        //public static Dictionary<string, StorjFile> _currentUploads = new Dictionary<string, StorjFile>();

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
                //_stream = new DownloadStream(bucket, (int)obj.SystemMetadata.ContentLength, obj.Key);
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
                Debug.WriteLine("Flushing file to storj: " + FileName);
                var uploadOperation = _objectService.UploadObjectAsync(_bucket, FileName, new UploadOptions(), ((MemoryStream)_stream).ToArray(),false).Result;
                uploadOperation.StartUploadAsync().Wait();
                Debug.WriteLine("Flushed: " + FileName + uploadOperation.Completed);

                _needsFlushing = false;
                FileChanged?.Invoke(this, EventArgs.Empty);
            }
            //if (_uploadOperation != null)
            //{
            //    _uploadOperation.Commit();
            //}
            
        }

        public void Lock(long offset, long length)
        {
            throw new System.IO.IOException();
        }

        public int Read(byte[] buffer, long offset)
        {
            if (_stream == null)
                return 0;
            //ToDo: Das Read klappt mit DownloadStream nicht im Random Access.
            //Der Stream muss irgendwie gepuffert oder gekapselt werden. Oder
            //erstmal nur mit DownloadOperation.


            Debug.WriteLine("*** READ " + buffer.Length + " - " + offset);
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
            //return _stream.Read(buffer, 0, buffer.Length);
            //Debug.WriteLine("Reading " + FileName);
            //InitDownload(FileName);

            //if(_downloadOperation != null)
            //{
            //    while (!_downloadOperation.Failed && _downloadOperation.BytesReceived == 0)
            //    {
            //        Task.Delay(10);
            //    }
            //    if (_downloadOperation.BytesReceived > offset + buffer.Length)
            //    {
            //        Array.Copy(_downloadOperation.DownloadedBytes, offset, buffer, 0, buffer.Length);
            //        return buffer.Length;
            //    }
            //    else if (_downloadOperation.BytesReceived > offset)
            //    {
            //        var bytesRead = (int)(_downloadOperation.BytesReceived - offset);
            //        Array.Copy(_downloadOperation.DownloadedBytes, offset, buffer, 0, bytesRead);
            //        return bytesRead;
            //    }
            //    else
            //    {
            //        //Still no data yet - come back later :)
            //        return 0;
            //    }
            //}
            //else if (_downloadStream != null)
            //{
            //    _downloadStream.Position = offset;
            //    int bytesRead;
            //    if (_downloadStream.Length > 0 && _downloadStream.Length < offset + buffer.Length)
            //        bytesRead = _downloadStream.Read(buffer, 0, (int)(_downloadStream.Length - offset));
            //    else
            //        bytesRead = _downloadStream.Read(buffer, 0, buffer.Length);

            //    return bytesRead;
            //}
            //else
            //{
            //    throw new IOException();
            //}
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
            //if(_currentUploads.ContainsKey(FileName))
            //{
            //    _currentUploads[FileName].FileInformation = fi;
            //}
        }

        public void Unlock(long offset, long length)
        {
            throw new System.IO.IOException();
        }

        public void Write(byte[] buffer, long offset)
        {
            Debug.WriteLine("*** WRITE " + buffer.Length + " - " + offset);
            _stream.Position = offset;
            _stream.Write(buffer, 0, buffer.Length);
            _needsFlushing = true;
            //Debug.WriteLine("Writing to " + FileName);
            //////If not yet in the current uploads (i.e. SetEndOfFile not yet called) init the chunked upload
            ////if (_uploadOperation == null && _currentUploads.ContainsKey(FileName))
            ////{
            ////    _uploadOperation = _currentUploads[FileName].ChunkedUpload;
            ////}
            ////else if (_uploadOperation == null && !_currentUploads.ContainsKey(FileName))
            ////{
            //InitChunkedUpload(FileName);
            ////}

            //var chunkUploaded = _uploadOperation.WriteBytes(buffer); //Write the bytes to storj
            //if (!chunkUploaded)
            //{
            //    //Something went wrong - file saving not possible
            //    throw new IOException();
            //}

            FileChanged?.Invoke(this, EventArgs.Empty);
        }

        //private DownloadOperation _downloadOperation;
        //private Stream _downloadStream;
        //private void InitDownload(string fileName)
        //{
        //    if (_downloadStream != null || _downloadOperation != null)
        //        return;

        //    try
        //    {
        //        var getObjectTask = _objectService.GetObjectAsync(_bucket, fileName);
        //        getObjectTask.Wait();
        //        if (getObjectTask.Result.SystemMetadata.ContentLength < 10000000)
        //        {
        //            //Download that object using a DownloadOperation
        //            _downloadOperation = (_objectService.DownloadObjectAsync(_bucket, fileName, new DownloadOptions(), false)).Result;
        //            //_downloadOperation.DownloadOperationEnded += DownloadOperation_DownloadOperationEnded;
        //            _ = _downloadOperation.StartDownloadAsync();
        //        }
        //        else
        //        {
        //            //Download that object using a DownloadStream
        //            _downloadStream = new BufferedStream(new DownloadStream(_bucket, (int)getObjectTask.Result.SystemMetadata.ContentLength, fileName));
        //        }
        //    }
        //    catch
        //    {
        //        //Object does not exist - could be a newly created folder or file
        //    }
        //}

        ///// <summary>
        ///// Initializes a chunked upload.
        ///// </summary>
        ///// <param name="fileName">The filename to upload</param>
        //private void InitChunkedUpload(string fileName)
        //{
        //    if (_uploadOperation != null)
        //        return;

        //    if (!_currentUploads.ContainsKey(fileName))
        //    {
        //        StorjFile file = new StorjFile();
        //        file.Id = fileName;
        //        file.FileInformation = new DokanNet.FileInformation
        //        {
        //            FileName = fileName//,
        //            //Attributes
        //        };

        //        var uploadTask = _objectService.UploadObjectChunkedAsync(_bucket, fileName, new UploadOptions(), null);
        //        uploadTask.Wait();
        //        file.ChunkedUpload = uploadTask.Result;
        //        _currentUploads.Add(fileName, file);

        //        _uploadOperation = file.ChunkedUpload;
        //    }
        //}
    }
}
