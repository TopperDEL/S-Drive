using DokanNet;
using DokanNet.Logging;
using S_Drive.Contracts.Interfaces;
using S_Drive.Contracts.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using uplink.NET.Interfaces;
using uplink.NET.Models;
using uplink.NET.Services;

namespace S_Drive
{
    abstract class MemFsEntry
    {
        protected FileInformation fileinfo;

        public MemFsEntry(string name)
        {
            fileinfo = new FileInformation();
            fileinfo.CreationTime = fileinfo.LastAccessTime = fileinfo.LastWriteTime = DateTime.Now;
            fileinfo.Attributes = 0; // Child must fill this in!
            fileinfo.FileName = name;
            fileinfo.Length = 0;
        }

        public FileInformation GetFileInfo()
        {
            FileInformation r = new FileInformation();
            r.Attributes = fileinfo.Attributes;
            r.CreationTime = fileinfo.CreationTime;
            r.LastAccessTime = fileinfo.LastAccessTime;
            r.LastWriteTime = fileinfo.LastWriteTime;
            r.FileName = fileinfo.FileName;
            r.Length = fileinfo.Length;
            return r;
        }

        public bool IsValidName(string name)
        {
            if (name.Equals("")) { return false; }
            if (name.Equals(".")) { return false; }
            if (name.Equals("..")) { return false; }
            if (name.Contains("/")) { return false; }
            if (name.Contains("\\")) { return false; }
            return true;
        }

        public bool SetName(string newname)
        {
            if (!this.IsValidName(newname)) { return false; }
            fileinfo.FileName = newname;
            return true;
        }

        public abstract bool SetFileAttributes(FileAttributes attr);

        public void SetFileTime(DateTime ctime, DateTime atime, DateTime mtime)
        {
            fileinfo.LastWriteTime = mtime;
            fileinfo.LastAccessTime = atime;
            fileinfo.CreationTime = ctime;
        }
    }

    class MemFSDirectory : MemFsEntry
    {
        Hashtable contents;
        public MemFSDirectory(string name) : base(name)
        {
            contents = new Hashtable();
            fileinfo.Attributes = FileAttributes.Directory;
        }

        public bool AddFile(string name)
        {
            if (!this.IsValidName(name)) { return false; }

            if (contents.ContainsKey(name)) { return false; }
            contents.Add(name, new MemFSFile(name));
            return true;
        }

        public bool AddDirectory(string name)
        {
            if (!this.IsValidName(name)) { return false; }

            if (contents.ContainsKey(name)) { return false; }
            contents.Add(name, new MemFSDirectory(name));
            return true;
        }

        public bool AddEntry(string name, MemFsEntry entry)
        {
            if (!this.IsValidName(name)) { return false; }

            if (contents.ContainsKey(name)) { return false; }
            contents.Add(name, entry);
            return true;
        }

        public bool RemoveEntry(string name)
        {
            if (!this.IsValidName(name)) { return false; }

            if (!contents.ContainsKey(name)) { return false; }
            contents.Remove(name);
            return true;
        }

        public MemFsEntry GetEntry(string name)
        {
            if (contents.ContainsKey(name))
            {
                return (MemFsEntry)contents[name];
            }
            return null;
        }

        public MemFsEntry[] GetEntries()
        {
            MemFsEntry[] results = new MemFsEntry[contents.Count];
            int x = 0;

            foreach (object o in contents.Values)
            {
                results[x++] = (MemFsEntry)o;
            }
            return results;
        }

        public override bool SetFileAttributes(FileAttributes attr)
        {
            if ((attr & FileAttributes.Compressed) == FileAttributes.Compressed) { return false; }
            if ((attr & FileAttributes.Encrypted) == FileAttributes.Encrypted) { return false; }
            if ((attr & FileAttributes.Normal) == FileAttributes.Normal) { return false; }
            if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) { return false; }
            if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) { return false; }
            if ((attr & FileAttributes.Hidden) == FileAttributes.Hidden) { return false; }
            if ((attr & FileAttributes.Directory) != FileAttributes.Directory) { return false; }

            fileinfo.Attributes = attr;
            return true;
        }
    }

    class MemFSFile : MemFsEntry
    {

        IntPtr mydata;
        public uint refcount;
        public bool deleteonclose;
        public MemFSFile(string name) : base(name)
        {
            fileinfo.Attributes = FileAttributes.Normal;
            mydata = Marshal.AllocHGlobal(0);
            refcount = 0;
            deleteonclose = false;
        }

        ~MemFSFile()
        {
            Marshal.FreeHGlobal(mydata);
        }

        public bool Resize(long newsize)
        {
            if (newsize < 0) { return false; }
            if (newsize == fileinfo.Length) { return true; }
            try
            {
                mydata = Marshal.ReAllocHGlobal(mydata, (IntPtr)newsize);
                fileinfo.Length = newsize;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Read(byte[] buffer, out int readBytes, long offset)
        {
            if ((buffer.Length > Int32.MaxValue) || (offset > fileinfo.Length))
            {
                readBytes = 0;
                return false;
            }
            if ((buffer.Length == 0) || (offset == fileinfo.Length))
            {
                readBytes = 0;
                return true;
            }

            readBytes = (int)buffer.Length; // Cast is safe because whe check for overflow above.
            if (readBytes > (fileinfo.Length - offset))
            {
                readBytes = (int)(fileinfo.Length - offset); // Safe because we check above against an overflow.
            }
            try
            {
                Marshal.Copy((IntPtr)(mydata.ToInt64() + offset), buffer, 0, (int)readBytes);
            }
            catch
            {
                readBytes = 0;
                return false;
            }
            return true;
        }

        public bool Write(Byte[] buffer, out int writtenBytes, long offset)
        {
            try
            {
                if (buffer.LongLength > Int32.MaxValue)
                {
                    writtenBytes = 0;
                    return false;
                }
                if (buffer.Length + offset > fileinfo.Length)
                {
                    mydata = Marshal.ReAllocHGlobal(mydata, (IntPtr)(buffer.Length + offset));
                    fileinfo.Length = buffer.Length + offset;
                }
                Marshal.Copy(buffer, 0, (IntPtr)(mydata.ToInt64() + offset), buffer.Length);
                writtenBytes = buffer.Length;
            }
            catch
            {
                writtenBytes = 0;
                return false;
            }
            return true;
        }

        public override bool SetFileAttributes(FileAttributes attr)
        {
            // "touch" from cygwin seems to want to set the file attributes to
            // 0. As far as I can tell, 0 and Normal are equivelent, so
            // lets act like they requested Normal.
            if ((attr & FileAttributes.Normal) != FileAttributes.Normal) { attr |= FileAttributes.Normal; }

            if ((attr & FileAttributes.Compressed) == FileAttributes.Compressed) { return false; }
            if ((attr & FileAttributes.Encrypted) == FileAttributes.Encrypted) { return false; }
            if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) { return false; }
            if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) { return false; }
            if ((attr & FileAttributes.Hidden) == FileAttributes.Hidden) { return false; }
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory) { return false; }

            fileinfo.Attributes = attr;
            return true;
        }
    }

    public class MemFSje : IStorjMount, IDokanOperations
    {
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
        /// Dokan itself
        /// </summary>
        private Dokan _dokan;

        /// <summary>
        /// A helper to unmount the drive
        /// </summary>
        private System.Threading.ManualResetEvent _mre;
        private MemFSDirectory root;
        public MemFSje()
        {
            root = new MemFSDirectory("");
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

        [StructLayout(LayoutKind.Sequential)] //, Pack = 4)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public long ullTotalPhys;
            public long ullAvailPhys;
            public long ullTotalPageFile;
            public long ullAvailPageFile;
            public long ullTotalVirtual;
            public long ullAvailVirtual;
            public long ullAvailExtendedVirtual;
        }

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

        public void Unmount()
        {
            throw new NotImplementedException();
        }

        private MemFSDirectory GetDirectoryForPath(string path)
        {
            string[] elements = path.Split(new Char[] { '/', '\\' });

            MemFSDirectory cwd = root;
            MemFsEntry e;
            for (int x = 1; x < elements.Length - 1; x++)
            {
                e = cwd.GetEntry(elements[x]);
                if ((e == null) || (!(e is MemFSDirectory)))
                {
                    return null;
                }
                cwd = (MemFSDirectory)e;
            }

            return cwd;
        }

        private MemFsEntry GetEntryForPath(string path)
        {
            // Seems like a hack, but it's a consequence of the way we store
            // directories. When opening the root, the driver asks for "\\" (
            // note that is one backslash escaped). If we break this up into
            // components we get a directory of "" and a file of "". Opening a
            // directory of "" works, because GetDirectoryForPath returns the
            // root when there are no backslashes to split on. But inside the
            // root there is no file named "". I don't see a better way of
            // handling this, unless we create a "virtual" entry named "" in
            // each directory that points to self? That seems worse, since
            // we don't handle "." or ".." references either.
            if (path.Equals("\\")) { return root; }
            MemFSDirectory dir = GetDirectoryForPath(path);
            if (dir == null) { return null; }
            return dir.GetEntry(GetFilenameFromPath(path));
        }

        private string GetFilenameFromPath(string path)
        {
            string[] elements = path.Split(new Char[] { '/', '\\' });
            return elements[elements.Length - 1];
        }

        // Public methods follow...

        public NtStatus CreateFile(string filename, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            Debug.WriteLine("CreateFile - " + filename + " - " + access.ToString() + " - " + share.ToString() + " - " + mode.ToString() + " - " + options.ToString() + " - " + attributes.ToString() + " - " + info.ToString());
            MemFsEntry entry = GetEntryForPath(filename);

            // Not a lot to do for directories.
            if (entry != null && entry is MemFSDirectory)
            {
                info.IsDirectory = true;
                return DokanResult.Success;
            }

            // File exists and caller requests we overwrite.
            if ((entry != null) && (mode == FileMode.Create))
            {
                GetDirectoryForPath(filename).RemoveEntry(GetFilenameFromPath(filename));
                entry = null;
            }

            // File doesn't exist, do we create?
            if (entry == null)
            {
                if (mode == FileMode.Create || mode == FileMode.CreateNew || mode == FileMode.OpenOrCreate)
                {
                    if (GetDirectoryForPath(filename).AddFile(GetFilenameFromPath(filename)))
                    {
                        entry = GetEntryForPath(filename);
                        ((MemFSFile)entry).refcount = 1;
                        if ((options & FileOptions.DeleteOnClose) == FileOptions.DeleteOnClose)
                        {
                            ((MemFSFile)entry).deleteonclose = true;
                        }

                        return DokanResult.Success;
                    }
                    else { return DokanResult.Error; }
                }
                else { return DokanResult.FileNotFound; }
            }

            // File exists, do we open?
            if (mode == FileMode.CreateNew) { return DokanResult.AlreadyExists; }

            // Okay, open it.

            if (!(entry is MemFSFile))
            {
                // We don't support anything other than files or directories
                // (which are opened above and created in another method).
                return DokanResult.Error;
            }

            if ((options & FileOptions.DeleteOnClose) == FileOptions.DeleteOnClose)
            {
                // If they want delete-on-close, it must be opened that way
                // already.
                if ((((MemFSFile)entry).refcount > 0)
                    && (!((MemFSFile)entry).deleteonclose)
                )
                {
                    return DokanResult.Error;
                }
            }

            if (mode == FileMode.Truncate)
            {
                ((MemFSFile)entry).Resize(0);
            }

            ((MemFSFile)entry).refcount++;
            return DokanResult.Success;

        }

        public NtStatus OpenDirectory(String filename, DokanFileInfo info)
        {
            object o = GetEntryForPath(filename);
            if (o is MemFSDirectory) { return DokanResult.Success; }
            return DokanResult.Error;
        }

        public NtStatus CreateDirectory(String filename, DokanFileInfo info)
        {
            MemFsEntry entry = GetDirectoryForPath(filename);
            if (entry == null) { return DokanResult.FileNotFound; }
            if (GetEntryForPath(filename) != null) { return DokanResult.AlreadyExists; }
            if (((MemFSDirectory)entry).AddDirectory(GetFilenameFromPath(filename)))
            {
                return DokanResult.Success;
            }
            return DokanResult.Error;
        }

        public void Cleanup(string filename, IDokanFileInfo info)
        {
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

        public void CloseFile(string filename, IDokanFileInfo info)
        {
            MemFsEntry entry = GetEntryForPath(filename);
            if (entry is MemFSFile)
            {
                ((MemFSFile)entry).refcount--;
                if ((((MemFSFile)entry).deleteonclose) && (((MemFSFile)entry).refcount == 0))
                {
                    this.DeleteFile(filename, info);
                }
            }
        }

        public NtStatus ReadFile(string filename, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            MemFsEntry entry = GetEntryForPath(filename);
            if (!(entry is MemFSFile))
            {
                bytesRead = 0;
                return DokanResult.FileNotFound;
            }

            if (((MemFSFile)entry).Read(buffer, out bytesRead, offset))
            {
                return DokanResult.Success;
            }
            return DokanResult.Error;
        }

        public NtStatus WriteFile(string filename, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            MemFsEntry entry = GetEntryForPath(filename);
            if (!(entry is MemFSFile))
            {
                bytesWritten = 0;
                return DokanResult.FileNotFound;
            }

            if (((MemFSFile)entry).Write(buffer, out bytesWritten, offset))
            {
                return DokanResult.Success;
            }
            return DokanResult.Error;
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus GetFileInformation(string filename, out FileInformation fileinfo, IDokanFileInfo info)
        {
            fileinfo = new FileInformation();
            MemFsEntry entry = GetEntryForPath(filename);
            if (entry == null) { 
                return DokanResult.FileNotFound; }

            FileInformation f = entry.GetFileInfo();

            fileinfo.Attributes = f.Attributes;
            fileinfo.CreationTime = f.CreationTime;
            fileinfo.LastAccessTime = f.LastAccessTime;
            fileinfo.LastWriteTime = f.LastWriteTime;
            fileinfo.Length = f.Length;
            fileinfo.FileName = f.FileName;

            return DokanResult.Success;
        }

        public NtStatus FindFiles(string filename, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = new List<FileInformation>();
            MemFsEntry entry = GetEntryForPath(filename);
            if (!(entry is MemFSDirectory))
            {
                
                return DokanResult.FileNotFound;
            }

            FileInformation dots = entry.GetFileInfo();
            dots.FileName = ".";
            files.Add(dots);

            if (entry == root)
            {
                dots = entry.GetFileInfo();
                dots.FileName = "..";
                files.Add(dots);
            }
            else
            {
                dots = GetDirectoryForPath(filename).GetFileInfo();
                dots.FileName = "..";
                files.Add(dots);
            }

            foreach (MemFsEntry e in ((MemFSDirectory)entry).GetEntries())
            {
                files.Add(e.GetFileInfo());
            }

            return DokanResult.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = new List<FileInformation>();
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileAttributes(string filename, FileAttributes attributes, IDokanFileInfo info)
        {
            MemFsEntry entry = GetEntryForPath(filename);
            if (entry == null) { return DokanResult.FileNotFound; }
            if (entry.SetFileAttributes(attributes))
            {
                return DokanResult.Success;
            }
            return DokanResult.Error;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            // Disabled - Throws errors about improper times for files?
            //MemFsEntry entry = GetEntryForPath(filename);
            //if (entry == null) { return DokanResult.FileNotFound; }
            //entry.SetFileTime(ctime, atime, mtime);
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string filename, IDokanFileInfo info)
        {
            MemFsEntry entry = GetEntryForPath(filename);
            if (entry == null) { return DokanResult.FileNotFound; }
            if (!(entry is MemFSFile)) { return DokanResult.Error; }
            entry = GetDirectoryForPath(filename);
            if (((MemFSDirectory)entry).RemoveEntry(GetFilenameFromPath(filename)))
            {
                return DokanResult.Success;
            }
            return DokanResult.Error;
        }

        public NtStatus DeleteDirectory(string filename, IDokanFileInfo info)
        {
            MemFsEntry entry = GetEntryForPath(filename);
            if (entry == null) { return DokanResult.FileNotFound; }
            if (!(entry is MemFSDirectory)) { return DokanResult.Error; }
            entry = GetDirectoryForPath(filename);
            if (((MemFSDirectory)entry).RemoveEntry(GetFilenameFromPath(filename)))
            {
                return DokanResult.Success;
            }
            return DokanResult.Error;
        }

        public NtStatus MoveFile(string filename, string newname, bool replace, IDokanFileInfo info)
        {
            MemFsEntry entry = GetEntryForPath(filename);
            if (entry == null) { return DokanResult.FileNotFound; }

            MemFsEntry destentry = GetEntryForPath(newname);
            string newfilename = GetFilenameFromPath(newname);

            if (destentry != null)
            {
                if (replace)
                {
                    if (!GetDirectoryForPath(newname).RemoveEntry(newfilename))
                    {
                        return DokanResult.AlreadyExists;
                    }
                }
                else
                {
                    return DokanResult.AlreadyExists;
                }
            }

            if (!GetDirectoryForPath(newname).AddEntry(newfilename, entry)) { return DokanResult.Error; }
            if (!GetDirectoryForPath(filename).RemoveEntry(GetFilenameFromPath(filename))) { return DokanResult.Error; }
            entry.SetName(newfilename);

            return DokanResult.Success;
        }

        public NtStatus SetEndOfFile(string filename, long length, IDokanFileInfo info)
        {
            MemFsEntry entry = GetEntryForPath(filename);
            if (entry == null) { return DokanResult.FileNotFound; }
            if (entry is MemFSFile)
            {
                if (((MemFSFile)entry).Resize(length))
                {
                    return DokanResult.Success;
                }
                // else fall through...
            }
            return DokanResult.Error;
        }

        public NtStatus SetAllocationSize(string filename, long length, IDokanFileInfo info)
        {
            return this.SetEndOfFile(filename, length, info);
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            MemoryStatusEx ms = new MemoryStatusEx();
            ms.dwLength = (uint)Marshal.SizeOf(ms);
            GlobalMemoryStatusEx(ref ms);
            totalNumberOfBytes = ms.ullTotalPageFile;
            freeBytesAvailable = ms.ullAvailPageFile;
            totalNumberOfFreeBytes = ms.ullAvailPageFile;

            return DokanResult.Success;
        }

        public NtStatus Unmount(DokanFileInfo info)
        {
            return DokanResult.Success;
        }
    }

    public class StorjMountV3 : IStorjMount, IDokanOperations
    {
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

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            Debug.WriteLine("CreateFile - " + fileName + " - " + access.ToString() + " - " + share.ToString() + " - " + mode.ToString() + " - " + options.ToString() + " - " + attributes.ToString() + " - " + info.ToString());
            if (IsDirectory(fileName))
            {
                info.IsDirectory = true;
            }

            if (access == DokanNet.FileAccess.ReadAttributes)
            {
                attributes = FileAttributes.Directory;
            }

            if (fileName.Contains("desktop.ini"))
            {
                return DokanResult.FileNotFound;
            }

            if (info.IsDirectory)
            {
                if (mode == FileMode.Open)
                {
                    if (!FolderExists(fileName))
                        return DokanResult.PathNotFound;
                    Debug.WriteLine("OPEN");
                }
                else if (mode == FileMode.CreateNew)
                {
                    CreateFolder(fileName);
                }
            }

            return NtStatus.Success;
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
        private void CreateFolder(string fileName)
        {
            created = true;
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
                Attributes = info.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal
            };
            return NtStatus.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = new List<FileInformation>();
            files.Add(new FileInformation
            {
                FileName = "Test1.txt",
                Attributes = FileAttributes.Normal,
                Length = 100
            });

            files.Add(new FileInformation
            {
                FileName = "Folder1",
                Attributes = FileAttributes.Directory
            });

            if (created)
            {
                files.Add(new FileInformation
                {
                    FileName = "Neuer Ordner",
                    Attributes = FileAttributes.Directory
                });
            }
            return NtStatus.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = new List<FileInformation>();
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
