using DokanNet;
using DokanNet.Logging;
using S_Drive.Contracts.Interfaces;
using S_Drive.Contracts.Models;
using S_Drive.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using uplink.NET.Interfaces;
using uplink.NET.Models;
using uplink.NET.Services;

namespace S_Drive
{
    public class StorjMountV2 : IStorjMount, IDokanOperations
    {
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
        /// Objects with this name are (probably) empty folders. As Storj cannot create
        /// prefixes out of nowhere, we use this file to fake a folder on the network.
        /// It gets transferred into a Prefix in ListAllAsync().
        /// </summary>
        public const string DOKAN_FOLDER = "/folder.dokan";

        /// <summary>
        /// Defines the root-folder given from the OS
        /// </summary>
        const string ROOT_FOLDER = "\\";

        /// <summary>
        /// The mount parameters in use.
        /// </summary>
        private MountParameters _mountParameters;

        /// <summary>
        /// The access to the storj-network
        /// </summary>
        private Access _access;

        /// <summary>
        /// The BucketService to create/access the bucket
        /// </summary>
        private IBucketService _bucketService;

        /// <summary>
        /// The ObjectService to upload, list, download and delete objects within a bucket
        /// </summary>
        private IObjectService _objectService;

        /// <summary>
        /// The bucket used for this mount
        /// </summary>
        private Bucket _bucket;

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

        #region uplink-Access
        /// <summary>
        /// Init the services and ensure the bucket to use exists.
        /// </summary>
        /// <param name="bucketName">The bucket to connect to</param>
        private async Task InitUplinkAsync(string bucketName)
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
        }
        #endregion

        public async Task MountAsync(MountParameters mountParameters)
        {
            _mountParameters = mountParameters;
            Access.SetTempDirectory(System.IO.Path.GetTempPath());

            if (string.IsNullOrEmpty(mountParameters.AccessGrant))
                _access = new Access(mountParameters.SatelliteAddress, mountParameters.ApiKey, mountParameters.EncryptionPassphrase);
            else
                _access = new Access(mountParameters.AccessGrant);

            await InitUplinkAsync(mountParameters.Bucketname);

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

        public void Unmount()
        {
            throw new NotImplementedException();
        }

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, System.IO.FileShare share, System.IO.FileMode mode, System.IO.FileOptions options, System.IO.FileAttributes attributes, IDokanFileInfo info)
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
                    return DokanResult.AccessDenied;
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
                                    return DokanResult.AccessDenied;

                                info.IsDirectory = pathIsDirectory;

                                return DokanResult.Success;
                            }
                        }
                        else
                        {
                            return DokanResult.FileNotFound;
                        }
                        break;

                    case System.IO.FileMode.CreateNew:
                        if (pathExists)
                            return DokanResult.FileExists;
                        InitChunkedUpload(fileName, info);
                        break;

                    case System.IO.FileMode.Truncate:
                        if (!pathExists)
                            return DokanResult.FileNotFound;
                        break;
                }

                if (pathExists && (mode == System.IO.FileMode.OpenOrCreate || mode == System.IO.FileMode.Create))
                    result = DokanResult.AlreadyExists;
            }
            return result;
            //Debug.WriteLine("CreateFile - " + fileName + " - " + access.ToString() + " - " + share.ToString() + " - " + mode.ToString() + " - " + options.ToString() + " - " + attributes.ToString() + " - " + info.ToString());
            //if (IsDirectory(fileName))
            //{
            //    info.IsDirectory = true;
            //}

            //if (access == DokanNet.FileAccess.ReadAttributes)
            //{
            //    attributes = FileAttributes.Directory;
            //}

            //if (fileName.Contains("desktop.ini"))
            //{
            //    return DokanResult.FileNotFound;
            //}

            //if (info.IsDirectory)
            //{
            //    if (mode == FileMode.Open)
            //    {
            //        if (!FolderExists(fileName))
            //            return DokanResult.PathNotFound;
            //        Debug.WriteLine("OPEN");
            //    }
            //    else if (mode == FileMode.CreateNew)
            //    {
            //        CreateFolder(fileName);
            //    }
            //}

            //return NtStatus.Success;
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

        private bool IsDirectory(string fileName)
        {
            if (fileName == @"\")
                return true;
            else if (fileName.Contains("Neuer"))
                return created;

            return false;
        }

        private bool FolderExists(string fileName)
        {
            if (fileName == @"\")
                return true;
            else if (fileName.Contains("Neuer"))
                return false;
            return false;
        }
        bool created = false;
        private void CreateFolder(string folderName)
        {
            var file = GetPath(folderName);
            file = ToInternalFolder(file);
            var uploadTask = _objectService.UploadObjectAsync(_bucket, file, new UploadOptions(), Encoding.UTF8.GetBytes(folderName), false);
            uploadTask.Wait();
            var result = uploadTask.Result;
            result.StartUploadAsync().Wait();
            //ClearMemoryCache();
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
            return fileName + DOKAN_FOLDER;
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            bytesRead = 0;
            return NtStatus.NotImplemented;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            fileInfo = new FileInformation
            {
                FileName = fileName,
                Attributes = info.IsDirectory ? System.IO.FileAttributes.Directory : System.IO.FileAttributes.Normal
            };
            return NtStatus.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            // This function is not called because FindFilesWithPattern is implemented
            // Return DokanResult.NotImplemented in FindFilesWithPattern to make FindFiles called
            files = FindFilesHelper(fileName, "*");

            return DokanResult.Success;
        }

        /// <summary>
        /// Lists all objects within a bucket to provide the folder-structure
        /// </summary>
        /// <returns>The list of objects</returns>
        private async Task<List<uplink.NET.Models.Object>> ListAllAsync()
        {
            //var result = _memoryCache[LIST_CACHE] as List<uplink.NET.Models.Object>;
            //if (result == null)
            //{
                var objects = await _objectService.ListObjectsAsync(_bucket, new ListObjectsOptions() { Recursive = true, Custom = true, System = true }).ConfigureAwait(false);
                var result = new List<uplink.NET.Models.Object>();
                foreach (var obj in objects.Items)
                {
                    result.Add(obj);
                }

                //Filter double folder-names
                result = result.GroupBy(x => x.Key).Select(y => y.First()).ToList();

                //var cachePolicy = new CacheItemPolicy();
                //cachePolicy.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(1);
                //_memoryCache.Set(LIST_CACHE, result, cachePolicy);
            //}

            return result;
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

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = new List<FileInformation>();
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileAttributes(string fileName, System.IO.FileAttributes attributes, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            totalNumberOfBytes = 1125899906842624; //1PB - why not? Storj is huge. :)
            totalNumberOfFreeBytes = totalNumberOfBytes;
            freeBytesAvailable = totalNumberOfFreeBytes;

            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = _mountParameters.VolumeLabel;
            fileSystemName = "NTFS";
            maximumComponentLength = 256;

            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                       FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                       FileSystemFeatures.UnicodeOnDisk;

            return NtStatus.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            security = null;
            return NtStatus.NotImplemented; //Otherwise we could not create a new folder
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }
    }
}
