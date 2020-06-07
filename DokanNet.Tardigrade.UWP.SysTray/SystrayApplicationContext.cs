using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;

namespace DokanNet.Tardigrade.UWP.SysTray
{
    class SystrayApplicationContext : ApplicationContext
    {
        private NotifyIcon _notifyIcon = null;

        public SystrayApplicationContext()
        {
            MenuItem openMenuItem = new MenuItem("Open UWP", new EventHandler(OpenApp));
            openMenuItem.DefaultItem = true;

            _notifyIcon = new NotifyIcon();
            _notifyIcon.DoubleClick += new EventHandler(OpenApp);
            _notifyIcon.Icon = DokanNet.Tardigrade.UWP.SysTray.Properties.Resources.Storj_symbol;
            _notifyIcon.ContextMenu = new ContextMenu(new MenuItem[] { openMenuItem });
            _notifyIcon.Visible = true;
        }

        private async void OpenApp(object sender, EventArgs e)
        {
            IEnumerable<AppListEntry> appListEntries = await Package.Current.GetAppListEntriesAsync();
            await appListEntries.First().LaunchAsync();
            Application.Exit(); //Todo: Unmount drives or at least handle changes correctly.
        }
    }
}
