using DokanNet;
using DokanNet.Logging;
using S_Drive.Contracts.Interfaces;
using S_Drive.Contracts.Models;
using S_Drive.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using uplink.NET.Models;
using uplink.NET.Services;
using static DokanNet.FormatProviders;

namespace S_Drive
{
    public class StorjMount : IStorjMount, IDokanOperations
    {
        private Helpers.SingleThreadUplinkRunner _stuRunner;
        /// <summary>
        /// The mount parameters in use.
        /// </summary>
        private MountParameters _mountParameters;

        /// <summary>
        /// Defines the root-folder given from the OS
        /// </summary>
        const string ROOT_FOLDER = "\\";

        /// <summary>
        /// Objects with this name are (probably) empty folders. As Storj cannot create
        /// prefixes out of nowhere, we use this file to fake a folder on the network.
        /// It gets transferred into a Prefix in ListAllAsync().
        /// </summary>
        public const string DOKAN_FOLDER = "/folder.dokan";

        /// <summary>
        /// The MemoryCache-Entry-Name for the result of ListAllAsync().
        /// </summary>
        const string LIST_CACHE = "LIST";

        /// <summary>
        /// A combined field to know the access-type in CreateFile
        /// </summary>
        private const FileAccess DataAccess = FileAccess.ReadData | FileAccess.WriteData | FileAccess.AppendData |
                                              FileAccess.Execute |
                                              FileAccess.GenericExecute | FileAccess.GenericWrite |
                                              FileAccess.GenericRead;

        /// <summary>
        /// A combined field to know the access-type in CreateFile
        /// </summary>
        private const FileAccess DataWriteAccess = FileAccess.WriteData | FileAccess.AppendData |
                                                   FileAccess.Delete |
                                                   FileAccess.GenericWrite;

        /// <summary>
        /// The access to the storj-network
        /// </summary>
        private Access _access;

        /// <summary>
        /// The BucketService to create/access the bucket
        /// </summary>
        private BucketService _bucketService;

        /// <summary>
        /// The ObjectService to upload, list, download and delete objects within a bucket
        /// </summary>
        private ObjectService _objectService;

        /// <summary>
        /// The bucket used for this mount
        /// </summary>
        private Bucket _bucket;

        /// <summary>
        /// The logger used in DEBUG-mode
        /// </summary>
        private ConsoleLogger logger = new ConsoleLogger("[Storj] ");

        /// <summary>
        /// The MemoryCache holds mainly the result of ListAllAsync plus some accessed files.
        /// It reduces the amount of data retrieved from the network lowering costs, saving bandwith and getting overall better performance.
        /// </summary>
        private ObjectCache _memoryCache = MemoryCache.Default;

        /// <summary>
        /// The dictionary maps a filename to it's currently running upload. IDokanFileInfo.Context was used here before, but that
        /// sometimes does not really keep track of that Upload leading to errors on file transfer. Therefore the mapping is hold
        /// seperately from Dokan.
        /// </summary>
        private Dictionary<string, ChunkedUploadOperation> _currentUploads = new Dictionary<string, ChunkedUploadOperation>();

        /// <summary>
        /// Dokan itself
        /// </summary>
        private Dokan _dokan;

        /// <summary>
        /// A helper to unmount the drive
        /// </summary>
        private System.Threading.ManualResetEvent _mre;

        #region Implementation of IStorjMount
        /// <summary>
        /// Mounts a given bucket with given access privileges. This method will block until the drive gets unmounted again. TODO
        /// </summary>
        /// <param name="mountParameters">The parameters to use for a  mount</param>
        /// <returns>A task doing the initialization</returns>
        public void Mount(MountParameters mountParameters)
        {
            _mountParameters = mountParameters;
            Access.SetTempDirectory(System.IO.Path.GetTempPath());

            if (string.IsNullOrEmpty(mountParameters.AccessGrant))
                _access = new Access(mountParameters.SatelliteAddress, mountParameters.ApiKey, mountParameters.EncryptionPassphrase);
            else
                _access = new Access(mountParameters.AccessGrant);

            _stuRunner = Helpers.SingleThreadUplinkRunner.Instance;

            InitUplink(mountParameters.Bucketname);

            var nullLogger = new NullLogger();
            using (_mre = new System.Threading.ManualResetEvent(false))
            using (_dokan = new Dokan(nullLogger))
            {
                var dokanBuilder = new DokanInstanceBuilder(_dokan)
                       .ConfigureOptions(options =>
                       {
                           options.Options = DokanOptions.DebugMode | DokanOptions.StderrOutput;
                           options.MountPoint = mountParameters.DriveLetter.ToString() + ":\\";
                       });
                using (var dokanInstance = dokanBuilder.Build(this))
                {
                    _mre.WaitOne();
                }
            }
        }

        /// <summary>
        /// Unmount this drive
        /// </summary>
        public void Unmount()
        {
            _mre.Set();
        }
        #endregion

        #region uplink-Access
        /// <summary>
        /// Init the services and ensure the bucket to use exists.
        /// </summary>
        /// <param name="bucketName">The bucket to connect to</param>
        private void InitUplink(string bucketName)
        {
            _ = _stuRunner.Run(async () =>
            {
                _bucketService = new BucketService(_access);
                _objectService = new ObjectService(_access);
                try
                {
                    _bucket = await _bucketService.GetBucketAsync(bucketName).ConfigureAwait(false);
                }
                catch
                {
                    _bucket = await _bucketService.EnsureBucketAsync(bucketName).ConfigureAwait(false);
                }
            });
        }
        #endregion

        #region Helper
        /// <summary>
        /// Lists all objects within a bucket to provide the folder-structure
        /// </summary>
        /// <returns>The list of objects</returns>
        private async Task<List<uplink.NET.Models.Object>> ListAllAsync()
        {
            var result = _memoryCache[LIST_CACHE] as List<uplink.NET.Models.Object>;
            if (result == null)
            {
                var objects = await _objectService.ListObjectsAsync(_bucket, new ListObjectsOptions() { Recursive = true, Custom = true, System = true }).ConfigureAwait(false);
                result = new List<uplink.NET.Models.Object>();
                foreach (var obj in objects.Items)
                {
                    if (obj.Key.Contains(DOKAN_FOLDER))
                    {
                        obj.IsPrefix = true;
                        obj.Key = obj.Key.Replace(DOKAN_FOLDER, "");
                    }
                    result.Add(obj);
                }

                //Filter double folder-names
                result = result.GroupBy(x => x.Key).Select(y => y.First()).ToList();

                var cachePolicy = new CacheItemPolicy();
                cachePolicy.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(1);
                _memoryCache.Set(LIST_CACHE, result, cachePolicy);
            }

            return result;
        }

        /// <summary>
        /// Converts a filename to an internal "fake-directory-file". the folder "myfolder" is internally
        /// represented as a storj-file named "myfolder/folder.dokan".
        /// This method adds "/folder.dokan" to a filename.
        /// </summary>
        /// <param name="fileName">The file name to convert</param>
        /// <returns>The internal "fake-folder-object"-name</returns>
        private string ToInternalFolder(string fileName)
        {
            return fileName + "/" + DOKAN_FOLDER;
        }

        /// <summary>
        /// Clears the memory cache, optionally also removes a file from the buffer (if it changed with a WriteFile-Operation)
        /// </summary>
        /// <param name="fileName">Optional - the filename of the file to remove from the cache.</param>
        private void ClearMemoryCache(string fileName = null)
        {
            _memoryCache.Remove(LIST_CACHE);

            if (fileName != null)
                _memoryCache.Remove(fileName);
        }

        /// <summary>
        /// Writes the result of an operation to the given trace - but only in DEBUG mode.
        /// </summary>
        /// <param name="method">The name of the current method</param>
        /// <param name="fileName">The name of the current file</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <param name="result">The result of the operation</param>
        /// <param name="parameters">Additional parameters</param>
        /// <returns>The result of the operation</returns>
        protected NtStatus Trace(string method, string fileName, IDokanFileInfo info, NtStatus result,
            params object[] parameters)
        {
#if TRACE
            var extraParameters = parameters != null && parameters.Length > 0
                ? ", " + string.Join(", ", parameters.Select(x => string.Format(DefaultFormatProvider, "{0}", x)))
                : string.Empty;

            logger.Debug(DokanFormat($"{method}('{fileName}', {info}{extraParameters}) -> {result}"));
#endif

            return result;
        }

        /// <summary>
        /// Writes the result of a FileAccess to the given trace - but only in DEBUG mode.
        /// </summary>
        /// <param name="method">The name of the current method</param>
        /// <param name="fileName">The name of the current file</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <param name="access">The FileAccess</param>
        /// <param name="share">The FileShare</param>
        /// <param name="mode">The FileMode</param>
        /// <param name="options">The FileOptions</param>
        /// <param name="attributes">The FileAttributes</param>
        /// <param name="result">The result of the operation</param>
        /// <returns>The result of the operation</returns>
        private NtStatus Trace(string method, string fileName, IDokanFileInfo info,
            FileAccess access, System.IO.FileShare share, System.IO.FileMode mode, System.IO.FileOptions options, System.IO.FileAttributes attributes,
            NtStatus result)
        {
#if TRACE
            logger.Debug(
                DokanFormat(
                    $"{method}('{fileName}', {info}, [{access}], [{share}], [{mode}], [{options}], [{attributes}]) -> {result}"));
            if (result == NtStatus.ObjectNameNotFound)
            {

            }
#endif

            return result;
        }

        /// <summary>
        /// Returns a path from a filename given by the OS. It mainly removes the leading \\ from the name.
        /// </summary>
        /// <param name="fileName">The filename</param>
        /// <returns>The filename used within storj.</returns>
        protected string GetPath(string fileName)
        {
            if (fileName == ROOT_FOLDER)
                return fileName.Replace("\\", "/");
            else
            {
                if (fileName.StartsWith(ROOT_FOLDER))
                    return fileName.Substring(1).Replace("\\", "/");
                else
                    return fileName.Replace("\\", "/");
            }
        }

        /// <summary>
        /// Searches for files in the current folder.
        /// </summary>
        /// <param name="fileName">The name of the current folder</param>
        /// <param name="searchPattern">A search pattern</param>
        /// <returns>The list of files and folders within the current folder</returns>
        public IList<FileInformation> FindFilesHelper(string fileName, string searchPattern)
        {
            var listTask = ListAllAsync();
            listTask.Wait();

            var currentFolder = fileName.Substring(1);
            currentFolder = currentFolder.Replace("\\", "/");

            List<FileInformation> result = new List<FileInformation>();
            IList<FileInformation> files = new List<FileInformation>();
            IList<FileInformation> folders = new List<FileInformation>();

            var folderHelper = new Helpers.FolderHelper();
            folderHelper.UpdateFolderTree(listTask.Result.Select(o => new FolderContent("/" + o.Key, o.SystemMetadata.Created, o.SystemMetadata.ContentLength)).ToList());
            var subContent = folderHelper.GetContentFor("/" + currentFolder);
            foreach (var sub in subContent)
            {
                if (sub.Key.EndsWith("/"))
                {
                    result.Add(new FileInformation
                    {
                        Attributes = System.IO.FileAttributes.Directory,
                        CreationTime = sub.CreationTime,
                        LastAccessTime = sub.CreationTime,
                        LastWriteTime = sub.CreationTime,
                        Length = 0,
                        FileName = sub.Key.Substring(currentFolder.Length + 1).TrimStart('/').TrimEnd('/')
                    });
                }
                else
                {
                    result.Add(new FileInformation
                    {
                        Attributes = System.IO.FileAttributes.Normal,
                        CreationTime = sub.CreationTime,
                        LastAccessTime = sub.CreationTime,
                        LastWriteTime = sub.CreationTime,
                        Length = sub.ContentLength,
                        FileName = sub.Key.Substring(currentFolder.Length + 1).TrimStart('/')
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Initializes a download by preparing a DownloadStream and starting it. The DownloadStream will be placed
        /// in the DokanFileInfo.Context.
        /// </summary>
        /// <param name="fileName">The filename to download</param>
        /// <param name="info">The DokanFileInfo</param>
        private void InitDownload(string fileName, IDokanFileInfo info)
        {
            //We may have downloaded that file already - if yes, take it from the cache.
            if (info.Context == null && _memoryCache[fileName] != null)
                info.Context = _memoryCache[fileName];

            if (info.Context == null)
            {
                //Get the internal name of that file
                var realFileName = GetPath(fileName);

                var getObjectTask = _objectService.GetObjectAsync(_bucket, realFileName);
                getObjectTask.Wait();

                if (getObjectTask.Result.SystemMetadata.ContentLength < 10000000)
                {
                    //Download that object using a DownloadOperation
                    DownloadOperation downloadOperation;
                    info.Context = downloadOperation = (_objectService.DownloadObjectAsync(_bucket, realFileName, new DownloadOptions(), false)).Result;
                    downloadOperation.DownloadOperationEnded += DownloadOperation_DownloadOperationEnded;
                    _ = downloadOperation.StartDownloadAsync();
                }
                else
                {
                    //Download that object using a DownloadStream
                    info.Context = new System.IO.BufferedStream(new DownloadStream(_bucket, (int)getObjectTask.Result.SystemMetadata.ContentLength, realFileName));
                }

                var cachePolicy = new CacheItemPolicy();
                cachePolicy.SlidingExpiration = new TimeSpan(0, 30, 0); //Keep it for 30 Minutes since last access in our cache.
                _memoryCache.Set(fileName, info.Context, cachePolicy);
            }
        }

        private void DownloadOperation_DownloadOperationEnded(DownloadOperation downloadOperation)
        {
            if(downloadOperation.Completed)
                Debug.WriteLine("Downloaded file " + downloadOperation.ObjectName);
            else
                Debug.WriteLine("Failed downloading file " + downloadOperation.ObjectName + " - " + downloadOperation.ErrorMessage);
        }

        /// <summary>
        /// Removes Download-Artifacts - currently not in use. Need to see if we need some disposal here...
        /// </summary>
        /// <param name="info">The DokanFileInfo</param>
        private void CleanupDownload(IDokanFileInfo info)
        {
            return;
        }

        /// <summary>
        /// Initializes a chunked upload.
        /// </summary>
        /// <param name="fileName">The filename to upload</param>
        /// <param name="info">The DokanFileInfo</param>
        private void InitChunkedUpload(string fileName, IDokanFileInfo info)
        {
            if (!_currentUploads.ContainsKey(fileName))
            {
                var realFileName = GetPath(fileName);

                var uploadTask = _objectService.UploadObjectChunkedAsync(_bucket, realFileName, new UploadOptions(), null);
                uploadTask.Wait();
                _currentUploads.Add(fileName, uploadTask.Result);
            }
        }

        /// <summary>
        /// Removes upload-Artifacts and clears the file from the cache.
        /// </summary>
        /// <param name="fileName">The filename to clear</param>
        private void CleanupChunkedUpload(string fileName)
        {
            if (_currentUploads.ContainsKey(fileName))
            {
                var commitResult = _currentUploads[fileName].Commit();
                _currentUploads.Remove(fileName);
                ClearMemoryCache(fileName);
            }
        }
        #endregion

        #region Implementation of IDokanOperations
        /// <summary>
        /// Gets the free space of the drive. The used amount is derived from the object-size - this is not the size on the network due to
        /// erasure coding!
        /// </summary>
        /// <param name="freeBytesAvailable">The free bytes available</param>
        /// <param name="totalNumberOfBytes">The total number of bytes - currently hard-coded 1PB</param>
        /// <param name="totalNumberOfFreeBytes">The total number of free bytes available</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            var listTask = ListAllAsync();
            listTask.Wait();

            totalNumberOfBytes = 1125899906842624; //1PB - why not? Storj is huge. :)
            totalNumberOfFreeBytes = totalNumberOfBytes - listTask.Result.Sum(f => f.SystemMetadata.ContentLength); ;
            freeBytesAvailable = totalNumberOfFreeBytes;
            return Trace(nameof(GetDiskFreeSpace), null, info, DokanResult.Success, "out " + freeBytesAvailable.ToString(),
                "out " + totalNumberOfBytes.ToString(), "out " + totalNumberOfFreeBytes.ToString());
        }

        /// <summary>
        /// This method searches for files if FindfilesWithPattern is not implemented. Therefore it won't get called here.
        /// </summary>
        /// <param name="fileName">The current folder</param>
        /// <param name="files">The list of files to be returned</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            // This function is not called because FindFilesWithPattern is implemented
            // Return DokanResult.NotImplemented in FindFilesWithPattern to make FindFiles called
            files = FindFilesHelper(fileName, "*");

            return Trace(nameof(FindFiles), fileName, info, DokanResult.Success);
        }

        /// <summary>
        /// This method searches for files with a pattern. The normal pattern is "*".
        /// Internally it uses FindFilesHelpfer().
        /// </summary>
        /// <param name="fileName">The current folder</param>
        /// <param name="searchPattern">The search pattern</param>
        /// <param name="files">The list of files to be returned</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = FindFilesHelper(fileName, searchPattern);

            return Trace(nameof(FindFilesWithPattern), fileName, info, DokanResult.Success);
        }

        /// <summary>
        /// Gets information about the volume - especially its name and its file-system.
        /// </summary>
        /// <param name="volumeLabel">The label of the volume</param>
        /// <param name="features">The features of the volume</param>
        /// <param name="fileSystemName">The name of the file-system</param>
        /// <param name="maximumComponentLength">The maximum component length</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the opration</returns>
        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = _mountParameters.VolumeLabel;
            fileSystemName = "NTFS";
            maximumComponentLength = 256;

            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                       FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                       FileSystemFeatures.UnicodeOnDisk;

            return Trace(nameof(GetVolumeInformation), null, info, DokanResult.Success, "out " + volumeLabel,
                "out " + features.ToString(), "out " + fileSystemName);
        }

        /// <summary>
        /// Got called if the drive got mounted
        /// </summary>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
        {
            return Trace(nameof(Mounted), null, info, DokanResult.Success);
        }

        /// <summary>
        /// Got called if the drive got unmounted
        /// </summary>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return Trace(nameof(Unmounted), null, info, DokanResult.Success);
        }

        /// <summary>
        /// Gets file security information.
        /// This is not implemented with storj.
        /// </summary>
        /// <param name="fileName">The filename</param>
        /// <param name="security">The security</param>
        /// <param name="sections">The access control sections</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            security = null;
            return DokanResult.NotImplemented;
        }

        /// <summary>
        /// Sets the file security information.
        /// This is not implemented with storj.
        /// </summary>
        /// <param name="fileName">The filename</param>
        /// <param name="security">The security</param>
        /// <param name="sections">The access control sections</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            return DokanResult.Error;
        }

        /// <summary>
        /// Creates an empty file and cleares the cache so that the file gets visible.
        /// The data of that file will be sent with WriteFile().
        /// </summary>
        /// <param name="fileName">The filename to create</param>
        /// <param name="length">The length (not used)</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            //Creates an empty file
            var file = GetPath(fileName);
            var uploadTask = _objectService.UploadObjectAsync(_bucket, file, new UploadOptions(), new byte[] { }, false);
            uploadTask.Wait();
            var result = uploadTask.Result;
            result.StartUploadAsync().Wait();
            ClearMemoryCache();
            return Trace(nameof(SetAllocationSize), fileName, info, DokanResult.Success);
        }

        /// <summary>
        /// Creates a file that is not empty - the data itself will be sent with WriteFile().
        /// </summary>
        /// <param name="fileName">The filename to create</param>
        /// <param name="length">The length (not used)</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            InitChunkedUpload(fileName, info);

            ClearMemoryCache();
            return Trace(nameof(SetEndOfFile), fileName, info, DokanResult.Success);
        }

        /// <summary>
        /// Writes data into a file by providing byte-chunks.
        /// </summary>
        /// <param name="fileName">The filename</param>
        /// <param name="buffer">The bytes to write</param>
        /// <param name="bytesWritten">The bytes written</param>
        /// <param name="offset">The offset</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            //If not yet in the current uploads (i.e. SetEndOfFile not yet called) init the chunked upload
            if (!_currentUploads.ContainsKey(fileName))
                InitChunkedUpload(fileName, info);

            var chunkedUpload = _currentUploads[fileName];
            var chunkUploaded = chunkedUpload.WriteBytes(buffer); //Write the bytes to storj
            if (chunkUploaded)
            {
                //Provide the bytes written
                bytesWritten = buffer.Length;
                return Trace(nameof(WriteFile), fileName, info, DokanResult.Success);
            }
            else
            {
                //Something went wrong - file saving not possible
                bytesWritten = 0;
                return Trace(nameof(WriteFile), fileName, info, DokanResult.Error);
            }
        }

        /// <summary>
        /// Reads data from a file. Currently the file gets downloaded completely as there is no DownloadRange-Method available (might come in the future).
        /// That means: if some bytes at the end of a file are requested, the system block here until that data got downloaded from uplink. This might
        /// be the reason for the most problems regarding the StorjMount.
        /// </summary>
        /// <param name="fileName">The filename to download</param>
        /// <param name="buffer">The buffer to write into - might have a different size as available</param>
        /// <param name="bytesRead">The number of bytes read</param>
        /// <param name="offset">The offset within the whole file</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {

            //var thread = _stuRunner.Run(() =>
            //{
            try
            {
                InitDownload(fileName, info);

                var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                int bytesReadByThread;
                if (info.Context is System.IO.BufferedStream)
                {
                    var download = info.Context as System.IO.BufferedStream;
                    download.Position = offset;
                    if (download.Length > 0 && download.Length < offset + buffer.Length)
                        bytesReadByThread = download.Read(buffer, 0, (int)(download.Length - offset));
                    else
                        bytesReadByThread = download.Read(buffer, 0, buffer.Length);
                    bytesRead = bytesReadByThread;
                }
                else if (info.Context is DownloadOperation)
                {
                    var downloadOperation = info.Context as DownloadOperation;
                    if(downloadOperation.BytesReceived > offset + buffer.Length)
                    {
                        Array.Copy(downloadOperation.DownloadedBytes, offset, buffer, 0, buffer.Length);
                        bytesRead = buffer.Length;
                    }
                    else if (downloadOperation.BytesReceived > offset)
                    {
                        bytesRead = (int)(downloadOperation.BytesReceived - offset);
                        Array.Copy(downloadOperation.DownloadedBytes,offset, buffer, 0, bytesRead);
                    }
                    else
                    {
                        //No data yet - come back later :)
                        bytesRead = 0;
                    }
                }
                else
                {
                    bytesRead = 0;
                }
            }
            catch
            {
                bytesRead = 0;
            }

            return Trace(nameof(ReadFile), fileName, info, DokanResult.Success);
        }

        /// <summary>
        /// Moves a file. Moving a file within storj is not possible! Therefore the files gets downloaded completely, uploaded to the new location
        /// and the original object gets deleted. This might be expensive for larger files!
        /// This method also gets called for directory-renames! Non-empty directories fail on purpose with an exception. Technically it would be possible
        /// by moving all objects to the new location - but depending on the amount and size this would be an expensive operation.
        /// It internally used MoveFileAsync().
        /// </summary>
        /// <param name="oldName">The old name</param>
        /// <param name="newName">The new name</param>
        /// <param name="replace">Whether or not an existing file should be replaced. If yes, the existing one gets deleted before.</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var moveFileTask = MoveFileAsync(oldName, newName, replace, info);
            moveFileTask.Wait();
            return moveFileTask.Result;
        }

        /// <summary>
        /// Moves a file async.
        /// </summary>
        // <param name="oldName">The old name</param>
        /// <param name="newName">The new name</param>
        /// <param name="replace">Whether or not an existing file should be replaced. If yes, the existing one gets deleted before.</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public async Task<NtStatus> MoveFileAsync(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var realOldName = GetPath(oldName);
            var realNewName = GetPath(newName);

            if (info.IsDirectory)
            {
                //The Directory has to be empty - otherwise we would have to copy every object with that path.
                //Furthermore we need to use the "folder.dokan"-file for the "rename".
                var files = await ListAllAsync().ConfigureAwait(false);
                if (files.Where(f => !f.IsPrefix && f.Key.StartsWith(realOldName)).Count() > 0)
                {
                    return DokanResult.DirectoryNotEmpty;
                }

                realOldName = ToInternalFolder(realOldName);
                realNewName = ToInternalFolder(realNewName);
            }

            if (replace)
                await _objectService.DeleteObjectAsync(_bucket, realNewName).ConfigureAwait(false);

            var download = await _objectService.DownloadObjectAsync(_bucket, realOldName, new DownloadOptions(), false).ConfigureAwait(false);
            await download.StartDownloadAsync().ConfigureAwait(false);

            var upload = await _objectService.UploadObjectAsync(_bucket, realNewName, new UploadOptions(), download.DownloadedBytes, false).ConfigureAwait(false);
            await upload.StartUploadAsync().ConfigureAwait(false);

            await _objectService.DeleteObjectAsync(_bucket, realOldName).ConfigureAwait(false);

            ClearMemoryCache();

            return DokanResult.Success;
        }

        /// <summary>
        /// Deletes a file by clearing the cache. The actual delete happens in Cleanup().
        /// </summary>
        /// <param name="fileName">The file to delete</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            //The real delete happens on Cleanup
            ClearMemoryCache();

            return Trace(nameof(DeleteFile), fileName, info, DokanResult.Success);
        }

        /// <summary>
        /// CreateFile does not really create a file - it creates a kind of a file-handle within Dokan. And it is also being used for directories.
        /// Depending on the FileMode and the type (file or folder) the method prepares some stuff.
        /// </summary>
        /// <param name="fileName">The filename to work on</param>
        /// <param name="access">The FileAccess</param>
        /// <param name="share">The FileShare</param>
        /// <param name="mode">The FileMode</param>
        /// <param name="options">The FileOptions</param>
        /// <param name="attributes">The FileAttributes</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus CreateFile(string fileName, FileAccess access, System.IO.FileShare share, System.IO.FileMode mode, System.IO.FileOptions options, System.IO.FileAttributes attributes, IDokanFileInfo info)
        {
            var result = DokanResult.Success;
            var filePath = GetPath(fileName);

            if (info.IsDirectory)
            {
                //It is a directory
                try
                {
                    switch (mode)
                    {
                        case System.IO.FileMode.Open:
                            //Nothing to do here
                            info.Context = new object();
                            break;

                        case System.IO.FileMode.CreateNew:
                            //Need to think about how to create a folder with a prefix
                            info.Context = new object();
                            CreateFolder(fileName);
                            break;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                        DokanResult.AccessDenied);
                }
            }
            else
            {
                //It is a file
                var pathExists = true;
                var pathIsDirectory = false;

                var readWriteAttributes = (access & DataAccess) == 0;
                var readAccess = (access & DataWriteAccess) == 0;
                var listTask = ListAllAsync();
                listTask.Wait();

                var prefix = listTask.Result.Where(l => l.Key == filePath).FirstOrDefault();
                pathExists = prefix != null ? true : false;
                pathIsDirectory = prefix != null && prefix.IsPrefix ? true : false;

                switch (mode)
                {
                    case System.IO.FileMode.Open:

                        if (pathExists || fileName == ROOT_FOLDER)
                        {
                            // check if driver only wants to read attributes, security info, or open directory
                            if (readWriteAttributes || pathIsDirectory)
                            {
                                if (pathIsDirectory && (access & FileAccess.Delete) == FileAccess.Delete
                                    && (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                                    //It is a DeleteFile request on a directory
                                    return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                        attributes, DokanResult.AccessDenied);

                                info.IsDirectory = pathIsDirectory;

                                return Trace(nameof(CreateFile), fileName, info, access, share, mode, options,
                                    attributes, DokanResult.Success);
                            }
                        }
                        else
                        {
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                                DokanResult.FileNotFound);
                        }
                        break;

                    case System.IO.FileMode.CreateNew:
                        if (pathExists)
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                                DokanResult.FileExists);
                        InitChunkedUpload(fileName, info);
                        break;

                    case System.IO.FileMode.Truncate:
                        if (!pathExists)
                            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                                DokanResult.FileNotFound);
                        break;
                }

                if (pathExists && (mode == System.IO.FileMode.OpenOrCreate || mode == System.IO.FileMode.Create))
                    result = DokanResult.AlreadyExists;
            }
            return Trace(nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                result);
        }

        /// <summary>
        /// Creates a folder with the given name. Technically it uploads a fake-file ("folder.dokan") to storj to make
        /// a "folder".
        /// </summary>
        /// <param name="folderName">The name of the new folder</param>
        private void CreateFolder(string folderName)
        {
            var file = GetPath(folderName);
            file = ToInternalFolder(file);
            var uploadTask = _objectService.UploadObjectAsync(_bucket, file, new UploadOptions(), new byte[] { }, false);
            uploadTask.Wait();
            var result = uploadTask.Result;
            result.StartUploadAsync().Wait();
            ClearMemoryCache();
        }

        /// <summary>
        /// Gets info about a file or folder
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileInfo"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            if (_currentUploads.ContainsKey(fileName))
            {
                //It is a file that is currently uploading
                fileInfo = new FileInformation
                {
                    FileName = fileName,
                    Attributes = System.IO.FileAttributes.NotContentIndexed | System.IO.FileAttributes.Archive,
                    CreationTime = DateTime.Now,
                    LastAccessTime = DateTime.Now,
                    LastWriteTime = DateTime.Now,
                    Length = 0,
                };
            }
            else if (fileName == ROOT_FOLDER || info.IsDirectory)
            {
                //It is the root or a directory
                fileInfo = new FileInformation
                {
                    FileName = fileName,
                    Attributes = System.IO.FileAttributes.Directory,
                    CreationTime = DateTime.Now,
                    LastAccessTime = DateTime.Now,
                    LastWriteTime = DateTime.Now,
                    Length = 0,
                };
            }
            else
            {
                //It is a file or not yet described in full
                var filePath = GetPath(fileName);

                //Search for it
                var listTask = ListAllAsync();
                listTask.Wait();

                var file = listTask.Result.Where(l => l.Key == filePath).FirstOrDefault();
                var fileExists = file != null ? true : false;
                if (!fileExists)
                {
                    //This file/folder does not exist
                    fileInfo = new FileInformation();
                    return Trace(nameof(GetFileInformation), fileName, info, DokanResult.FileNotFound);
                }

                if (file.IsPrefix) //"Fake"-folders ("folder.dokan") are seen as Prefixes, too, within ListAllAsync()
                {
                    //See it as a directory
                    fileInfo = new FileInformation
                    {
                        FileName = fileName,
                        Attributes = System.IO.FileAttributes.Directory,
                        CreationTime = file.SystemMetadata.Created,
                        LastAccessTime = file.SystemMetadata.Created, //Todo: use custom meta
                        LastWriteTime = file.SystemMetadata.Created, //Todo: use custom meta
                        Length = 0,
                    };
                }
                else
                {
                    //See it as a file
                    fileInfo = new FileInformation
                    {
                        FileName = fileName,
                        Attributes = System.IO.FileAttributes.NotContentIndexed | System.IO.FileAttributes.Archive,
                        CreationTime = file.SystemMetadata.Created,
                        LastAccessTime = file.SystemMetadata.Created, //Todo: use custom meta
                        LastWriteTime = file.SystemMetadata.Created, //Todo: use custom meta
                        Length = file.SystemMetadata.ContentLength,
                    };
                }
            }
            return Trace(nameof(GetFileInformation), fileName, info, DokanResult.Success);
        }

        /// <summary>
        /// Closes a "file-handle". If the parameter DeleteOnClose is true, delete that file on the network. But if it is a folder,
        /// ignore it - folders get deleted in DeleteFolder.
        /// </summary>
        /// <param name="fileName">The filename</param>
        /// <param name="info">The DokanFileInfo</param>
        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            CleanupChunkedUpload(fileName);
            CleanupDownload(info);

            if (info.DeleteOnClose &&
                !info.IsDirectory) //Directories are deleted with DeleteDirectory
            {
                var realFileName = GetPath(fileName);
                var deleteTask = _objectService.DeleteObjectAsync(_bucket, realFileName);
                deleteTask.Wait();

                ClearMemoryCache(fileName);
            }
        }

        /// <summary>
        /// Closes a "file-handle" - the difference between Cleanup and CloseFile is unclear here.
        /// So cleanup what we have in any case.
        /// </summary>
        /// <param name="fileName">The filename</param>
        /// <param name="info">The DokanFileInfo</param>
        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            CleanupChunkedUpload(fileName);
            CleanupDownload(info);
        }

        /// <summary>
        /// Unlocks a file - not supported on storj.
        /// </summary>
        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return Trace(nameof(UnlockFile), fileName, info, DokanResult.NotImplemented);
        }

        /// <summary>
        /// Locks a file - not supported on storj.
        /// </summary>
        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return Trace(nameof(LockFile), fileName, info, DokanResult.NotImplemented);
        }

        /// <summary>
        /// Sets file times - not supported on storj.
        /// </summary>
        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        /// <summary>
        /// Sets file attributes - not supported on storj.
        /// </summary>
        public NtStatus SetFileAttributes(string fileName, System.IO.FileAttributes attributes, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        /// <summary>
        /// Find stream, whatever is meant here - not supported on storj.
        /// </summary>
        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return Trace(nameof(FindStreams), fileName, info, DokanResult.NotImplemented);
        }

        /// <summary>
        /// No flushing necessary
        /// </summary>
        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            return Trace(nameof(FlushFileBuffers), fileName, info, DokanResult.NotImplemented);
        }

        /// <summary>
        /// Deletes a directory from storj. Internally calls DeleteDirectoryAsync.
        /// </summary>
        /// <param name="fileName">The folder to delete</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            var deleteDirectoryTask = DeleteDirectoryAsync(fileName, info);
            deleteDirectoryTask.Wait();
            return deleteDirectoryTask.Result;
        }

        /// <summary>
        /// Deletes a directory from the network. If it is not empty it deletes every object underneath.
        /// After that the internal cache is cleared so that the listing does not show deleted files/folders anymore.
        /// </summary>
        /// <param name="fileName">The folder to delete</param>
        /// <param name="info">The DokanFileInfo</param>
        /// <returns>The result of the operation</returns>
        public async Task<NtStatus> DeleteDirectoryAsync(string fileName, IDokanFileInfo info)
        {
            var realFileName = GetPath(fileName);

            //The Directory has to be empty - otherwise we would have to copy every object with that path.
            //Furthermore we need to use the "folder.dokan"-file for the "rename".
            var files = await ListAllAsync().ConfigureAwait(false);
            foreach (var toDelete in files.Where(f => !f.IsPrefix && f.Key.StartsWith(realFileName)))
            {
                await _objectService.DeleteObjectAsync(_bucket, toDelete.Key).ConfigureAwait(false);
            }

            realFileName = ToInternalFolder(realFileName);

            await _objectService.DeleteObjectAsync(_bucket, realFileName).ConfigureAwait(false);

            ClearMemoryCache();

            return Trace(nameof(DeleteDirectoryAsync), fileName, info, DokanResult.Success);
        }
        #endregion
    }
}
