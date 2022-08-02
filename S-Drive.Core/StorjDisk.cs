using DokanNet;
using DokanNet.Logging;
using NC.DokanFS;
using S_Drive.Contracts.Models;
using S_Drive.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using uplink.NET.Interfaces;
using uplink.NET.Models;
using uplink.NET.Services;

namespace S_Drive.Core
{
    public class StorjDisk : MountableBase, IDokanDisk
    {
        /// <summary>
        /// Objects with this name are (probably) empty folders. As Storj cannot create
        /// prefixes out of nowhere, we use this file to fake a folder on the network.
        /// It gets transferred into a Prefix in ListAllAsync().
        /// </summary>
        public const string DOKAN_FOLDER = "/folder.dokan";

        /// <summary>
        /// The MemoryCache-Entry-Name for the result of ListAllAsync().
        /// </summary>
        private const string LIST_CACHE = "LIST";

        private readonly string _bucketName;
        /// <summary>
        /// The access to the storj-network
        /// </summary>
        private readonly Access _access;

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
        /// The MemoryCache holds mainly the result of ListAllAsync plus some accessed files.
        /// It reduces the amount of data retrieved from the network lowering costs, saving bandwith and getting overall better performance.
        /// </summary>
        private ObjectCache _memoryCache = MemoryCache.Default;

        public StorjDisk(Access access, string bucketName)
        {
            _access = access;
            _bucketName = bucketName;
        }

        protected override async Task InitBeforeMountingAsync()
        {
            await InitUplinkAsync(_bucketName);
        }

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

        public string Id => "S-Drive";

        public string VolumeLabel => "S-Drive";

        public string FileSystemName => "NTFS";

        public FileSystemFeatures FileSystemFeatures => FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                                                        FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                                                        FileSystemFeatures.UnicodeOnDisk;

        public uint MaximumComponentLength => 256;

        public void CreateDirectory(string path)
        {
            var folderFile = ToInternalFolder(path);
            var uploadTask = _objectService.UploadObjectAsync(_bucket, folderFile, new UploadOptions(), Encoding.UTF8.GetBytes(path), false);
            uploadTask.Wait();
            var result = uploadTask.Result;
            result.StartUploadAsync().Wait();
            ClearMemoryCache();
            Dokan.Notify.Create(_dokanInstance, GetPathInverse(path), true);
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

            //Barrel.Current.Empty(_bucket.Name + "_" + fileName);
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


        public IDokanFileContext CreateFileContext(string path, FileMode mode, System.IO.FileAccess access, FileShare share = FileShare.None, FileOptions options = FileOptions.None)
        {
            var fileExist = FileExists(path);

            var context = StorjDriveContext.GetOrCreateContext(this, path, _objectService, _bucket, fileExist);
            context.FileChanged += (sender, args) =>
            {
                ClearMemoryCache();
                Dokan.Notify.Update(_dokanInstance, GetPathInverse(path));
                Dokan.Notify.XAttrUpdate(_dokanInstance, GetPathInverse(path));
            };

            return context;
        }

        public void DeleteDirectory(string path)
        {
            var subObject = GetFolderSubElementsAsync(path).Result;
            foreach (var sub in subObject)
            {
                DeleteFile(sub);
            }
            ClearMemoryCache(path);
            Dokan.Notify.Delete(_dokanInstance, GetPathInverse(path), true);
        }

        public void DeleteFile(string path)
        {
            try
            {
                if (_objectService.GetObjectAsync(_bucket, path).Result != null)
                {
                    _objectService.DeleteObjectAsync(_bucket, path).Wait();
                }
            }
            catch { }
            ClearMemoryCache(path);
            Dokan.Notify.Delete(_dokanInstance, GetPathInverse(path), false);
        }

        public bool DirectoryCanBeDeleted(string path)
        {
            throw new NotImplementedException();
        }

        public bool DirectoryExists(string path)
        {
            if (path == "/" || path.Length == 0)
                return true;

            var exists = ListAllAsync().Result.Where(o => o.Key == path + DOKAN_FOLDER).Count() >= 1;
            return exists;
        }

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

        private async Task<List<string>> GetFolderSubElementsAsync(string path)
        {
            var result = new List<string>();
            var objects = await _objectService.ListObjectsAsync(_bucket, new ListObjectsOptions() { Recursive = true, Custom = true, System = true, Prefix = path + "/" }).ConfigureAwait(false);
            foreach (var obj in objects.Items)
            {
                result.Add(obj.Key);
            }

            return result;
        }

        public bool FileExists(string path)
        {
            var exists = ListAllAsync().Result.Where(o => o.Key == path).Count() >= 1;
            var isOpen = StorjDriveContext.GetFilesWithContext().Where(o=>o.Key == path).Any();

            return (exists || isOpen);
        }

        public IList<FileInformation> FindFiles(string directory, string searchPattern)
        {
            var listTask = ListAllAsync();
            listTask.Wait();

            var currentFolder = directory.Substring(1);
            currentFolder = currentFolder.Replace(@"\\", "/");

            List<FileInformation> result = new List<FileInformation>();
            IList<FileInformation> files = new List<FileInformation>();
            IList<FileInformation> folders = new List<FileInformation>();

            var folderHelper = new Helpers.FolderHelper();
            var objectList = listTask.Result.Select(o => new FolderContent("/" + o.Key, o.SystemMetadata.Created, o.SystemMetadata.ContentLength)).ToList();

            //Add files that are currently in use/created
            objectList.AddRange(StorjDriveContext.GetFilesWithContext());

            folderHelper.UpdateFolderTree(objectList);
            var subContent = folderHelper.GetContentFor("/" + currentFolder);
            foreach (var sub in subContent)
            {
                if (sub.Key.EndsWith("/"))
                {
                    var folderName = sub.Key.Substring(currentFolder.Length + 1).TrimStart('/').TrimEnd('/');
                    if (DokanHelper.DokanIsNameInExpression(searchPattern, folderName, false))
                    {
                        result.Add(new FileInformation
                        {
                            Attributes = System.IO.FileAttributes.Directory,
                            CreationTime = sub.CreationTime,
                            LastAccessTime = sub.CreationTime,
                            LastWriteTime = sub.CreationTime,
                            Length = 0,
                            FileName = folderName
                        });
                    }
                }
                else
                {
                    var fileName = sub.Key.Substring(currentFolder.Length + 1).TrimStart('/');
                    if (DokanHelper.DokanIsNameInExpression(searchPattern, fileName, false))
                    {
                        result.Add(new FileInformation
                        {
                            Attributes = System.IO.FileAttributes.Normal,
                            CreationTime = sub.CreationTime,
                            LastAccessTime = sub.CreationTime,
                            LastWriteTime = sub.CreationTime,
                            Length = sub.ContentLength,
                            FileName = fileName
                        });
                    }
                }
            }

            return result;
        }

        public void GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes)
        {
            var listTask = ListAllAsync();
            listTask.Wait();

            totalNumberOfBytes = 1125899906842624; //1PB - why not? Storj is huge. :)
            totalNumberOfFreeBytes = totalNumberOfBytes - listTask.Result.Sum(f => f.SystemMetadata.ContentLength);
            freeBytesAvailable = totalNumberOfFreeBytes;
        }

        public bool GetFileInfo(string path, out FileInformation fi)
        {
            var listTask = ListAllAsync();
            listTask.Wait();
            var currentFile = listTask.Result.Where(s => s.Key == path).FirstOrDefault();
            fi = new FileInformation
            {
                FileName = path
            };
            if (currentFile != null)
            {
                fi.Length = currentFile.SystemMetadata.ContentLength;
                fi.CreationTime = currentFile.SystemMetadata.Created;
                fi.LastWriteTime = currentFile.SystemMetadata.Created;
                fi.Attributes = FileAttributes.Normal;
            }
            else
            {
                fi.Attributes = FileAttributes.Directory;
            }
            
            return true;
        }

        public string GetPath(string osPath)
        {
            return osPath.Replace(@"\\", "").Replace(@"\", @"/").TrimStart('/');
        }

        public string GetPathInverse(string osPath)
        {
            return @"\\" + osPath.Replace(@"/", @"\");
        }

        public bool IsDirectory(string path)
        {
            if (path.EndsWith("/") || path.Length == 0)
            {
                return true;
            }
            else
            {
                return DirectoryExists(path);
            }
        }

        public bool IsDirectoryEmpty(string path)
        {
            var realPath = GetPath(path);

            var objectsWithPrefixExist = ListAllAsync().Result.Where(s => s.Key.StartsWith(realPath)).Count() >= 1;
            return !objectsWithPrefixExist;
        }

        public void MoveDirectory(string oldPath, string newPath)
        {
            var subObject = GetFolderSubElementsAsync(oldPath).Result;
            foreach (var sub in subObject)
            {
                MoveFile(sub, newPath + sub.Substring(oldPath.Length));
            }
            ClearMemoryCache(oldPath);
            Dokan.Notify.Rename(_dokanInstance, GetPathInverse(oldPath), GetPathInverse(newPath), true, true);
        }

        public void MoveFile(string oldPath, string newPath)
        {
            _objectService.MoveObjectAsync(_bucket, oldPath, _bucket, newPath).Wait();
            ClearMemoryCache(oldPath);
            Dokan.Notify.Rename(_dokanInstance, GetPathInverse(oldPath), GetPathInverse(newPath), false, false);
        }

        public void SetFileAttribute(string path, FileAttributes attr)
        {
            //ToDo
            Debug.WriteLine("NOT IMPLEMENTED - SetFileAttribute");
        }

        public void SetFileTime(string path, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime)
        {
            //ToDo
            Debug.WriteLine("NOT IMPLEMENTED - SetFiletime");
        }

        public IDokanFile Touch(string path, FileAttributes attributes)
        {
            ClearMemoryCache(path);
            return null; // Not used
        }

        public void UpdateDirectoryInformation(IDokanDirectory dir)
        {
            throw new NotImplementedException();
        }

        public void UpdateFileInformation(IDokanFile file)
        {
            throw new NotImplementedException();
        }
    }
}
