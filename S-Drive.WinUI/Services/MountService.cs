using S_Drive.Contracts.Models;
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
        private Dictionary<MountParameters, StorjMount> _activeMounts;

        public MountService()
        {
            _activeMounts = new Dictionary<MountParameters, StorjMount>();
        }

        public void MountAll(List<MountParameters> mountParameters)
        {
            foreach (var mountParameter in mountParameters)
            {
                StorjMount storjMount = new StorjMount();

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

        private void StartMount(StorjMount mount, MountParameters mountParameters)
        {
            try
            {
                mount.Mount(mountParameters);
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
