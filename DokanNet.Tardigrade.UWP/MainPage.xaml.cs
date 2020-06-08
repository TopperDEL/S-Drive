using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core.Preview;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x407 dokumentiert.

namespace DokanNet.Tardigrade.UWP
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Services.UWPConnectionService _uwpConnectionService;

        public MainPage()
        {
            this.InitializeComponent();

            SystemNavigationManagerPreview mgr = SystemNavigationManagerPreview.GetForCurrentView();
            mgr.CloseRequested += SystemNavigationManager_CloseRequested;

            _uwpConnectionService = new Services.UWPConnectionService();
        }

        private async void SystemNavigationManager_CloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
           
        }

        private async void MountAll_Click(object sender, RoutedEventArgs e)
        {
            var messageSent = await _uwpConnectionService.SendMountAllAsync();
            if (!messageSent)
            {
                MessageDialog dlg = new MessageDialog("error");
            }
        }

        private async void UnmountAll_Click(object sender, RoutedEventArgs e)
        {
            var messageSent = await _uwpConnectionService.SendUnmountAllAsync();
            if (!messageSent)
            {
                MessageDialog dlg = new MessageDialog("error");
            }
        }
    }
}
