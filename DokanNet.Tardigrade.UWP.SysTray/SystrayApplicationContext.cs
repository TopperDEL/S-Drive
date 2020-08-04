using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;

namespace DokanNet.Tardigrade.UWP.SysTray
{
    class SystrayApplicationContext : ApplicationContext
    {
        private NotifyIcon _notifyIcon = null;
        private Services.UWPConnectionService _uwpConnectionService;
        private Services.MountService _mountService;
        public static MenuItem openMenuItem;

        public SystrayApplicationContext()
        {
            openMenuItem = new MenuItem("Open UWP", new EventHandler(OpenApp));
            openMenuItem.DefaultItem = true;

            _notifyIcon = new NotifyIcon();
            _notifyIcon.DoubleClick += new EventHandler(OpenApp);
            _notifyIcon.Icon = DokanNet.Tardigrade.UWP.SysTray.Properties.Resources.Storj_symbol;
            _notifyIcon.ContextMenu = new ContextMenu(new MenuItem[] { openMenuItem });
            _notifyIcon.Visible = true;

            _uwpConnectionService = new Services.UWPConnectionService();
            _uwpConnectionService.MountAll += _uwpConnectionService_MountAll;
            _uwpConnectionService.UnmountAll += _uwpConnectionService_UnmountAll;
            _uwpConnectionService.DrivesMounted += _uwpConnectionService_DrivesMounted;

            _mountService = new Services.MountService();
        }

        private bool _uwpConnectionService_DrivesMounted()
        {
            return _mountService.GetDrivesMounted();
        }

        private void _uwpConnectionService_MountAll(List<Contracts.Models.MountParameters> mountList)
        {
            _mountService.UnmountAll();
            _mountService.MountAll(mountList);
        }

        private void _uwpConnectionService_UnmountAll()
        {
            _mountService.UnmountAll();
        }

        private async void OpenApp(object sender, EventArgs e)
        {
            IEnumerable<AppListEntry> appListEntries = await Package.Current.GetAppListEntriesAsync();
            await appListEntries.First().LaunchAsync();
            //Application.Exit(); //Todo: Unmount drives or at least handle changes correctly.
        }
    }
}
