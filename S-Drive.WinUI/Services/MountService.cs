using S_Drive.Contracts.Models;
using S_Drive.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace S_Drive.WinUI.Services
{
    class MountService
    {
        private Dictionary<MountParameters, StorjDisk> _activeMounts;

        public MountService()
        {
            _activeMounts = new Dictionary<MountParameters, StorjDisk>();
        }

        public void MountAll(List<MountParameters> mountParameters)
        {
            foreach (var mountParameter in mountParameters)
            {
                StorjDisk storjMount = new StorjDisk(new uplink.NET.Models.Access(mountParameter.AccessGrant), mountParameter.Bucketname);

                Task mountTask = Task.Run(() => StartMount(storjMount, mountParameter));
                _activeMounts.Add(mountParameter, storjMount);
            }
        }

        public void UnmountAll()
        {
            foreach (var entry in _activeMounts.ToList())
            {
                entry.Value.Unmount();
                _activeMounts.Remove(entry.Key);
            }
        }

        public bool GetDrivesMounted()
        {
            return _activeMounts.Count() > 0;
        }

        private async Task StartMount(StorjDisk mount, MountParameters mountParameters)
        {
            try
            {
                var dokan = new NC.DokanFS.DokanFrontend(mount, "Storj");
                var driveLetter = (NC.DokanFS.DriveLetters)Enum.Parse(typeof(NC.DokanFS.DriveLetters), mountParameters.DriveLetter.ToString());
                await mount.MountAsync(new NC.DokanFS.MountParameters { DriveLetter = driveLetter, VolumeLabel = mountParameters.VolumeLabel }, dokan);
            }
            catch (Exception ex)
            {
                //TODO:
                //var error = Properties.Resources.MountError.Replace("$error$", ex.Message).Replace("$stack$", ex.StackTrace);

                //MessageBox.Show(error);
            }
        }
    }
}
