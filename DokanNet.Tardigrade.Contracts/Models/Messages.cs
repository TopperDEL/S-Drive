using System;
using System.Collections.Generic;
using System.Text;

namespace DokanNet.Tardigrade.Contracts.Models
{
    public abstract class Messages
    {
        public static string MountAll = "TARDIGRADE_MOUNT_ALL";
        public static string UnmountAll = "TARDIGRADE_UNMOUNT_ALL";
        public static string AreDrivesMounted = "TARDIGRADE_ARE_DRIVES_MOUNTED";
        public static string AreDrivesMountedResult = "TARDIGRADE_ARE_DRIVES_MOUNTED_RESULT";
        public static string IsDokanyInstalled = "TARDIGRADE_IS_DOKANY_INSTALLED";
        public static string IsDokanyInstalledResult = "TARDIGRADE_IS_DOKANY_INSTALLED_RESULT";
    }
}
