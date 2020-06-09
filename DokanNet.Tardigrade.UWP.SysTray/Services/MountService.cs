using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DokanNet.Tardigrade.UWP.SysTray.Services
{
    class MountService
    {
        private Dictionary<MountParameters, Task> _activeMounts;

        public MountService()
        {
            _activeMounts = new Dictionary<MountParameters, Task>();
        }

        public void MountAll(List<MountParameters> mounts)
        {
            foreach (var mount in mounts)
            {
                Task mountTask = Task.Run(() => StartMount(mount));
                mountTask.Start();
                _activeMounts.Add(mount, mountTask);
            }
        }

        public void UnmountAll()
        {
            foreach (var entry in _activeMounts)
            {
                //Todo
            }
        }

        private void StartMount(MountParameters mountParameters)
        {
            try
            {
                DokanNet.Tardigrade.TardigradeMount mount = new TardigradeMount();
                mount.MountAsync(mountParameters).Wait();
                MessageBox.Show("Unmounted!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Mount-Error: " + ex.Message);
            }
        }
    }
}
