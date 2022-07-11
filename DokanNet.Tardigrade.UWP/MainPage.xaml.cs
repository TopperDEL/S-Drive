using S_Drive.UWP.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
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

namespace S_Drive.UWP
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ResourceLoader _resourceLoader;
        private Services.UWPConnectionService _uwpConnectionService;
        private Services.VaultService _vaultService;
        public ViewModels.MountViewModel _vm;

        public MainPage()
        {
            this.InitializeComponent();

            _resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

            _vaultService = new Services.VaultService();

            this.DataContext = _vm = new ViewModels.MountViewModel();
            foreach (var mount in _vaultService.LoadMounts())
            {
                _vm.Mounts.Add(new ViewModels.MountParameterViewModel(mount));
            }

            SystemNavigationManagerPreview mgr = SystemNavigationManagerPreview.GetForCurrentView();
            mgr.CloseRequested += SystemNavigationManager_CloseRequested;

            _uwpConnectionService = new Services.UWPConnectionService();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await GetMountStatusAsync();
        }

        private async Task<bool> IsSystrayAvailable()
        {
            await Services.SystrayCommunicator.AssureSystrayIsLaunchedAsync();

            int counter = 0;
            while (Services.UWPConnectionService._connection == null && counter < 10)
            {
                await Task.Delay(100);
                counter++;
            }

            var isAvailable = await _uwpConnectionService.GetIsTrayAliveAsync();

            if(!isAvailable)
            {
                //Restart systray
                Services.UWPConnectionService._connection = null;
                Services.SystrayCommunicator._fillTrustStarted = false;
                await Services.SystrayCommunicator.AssureSystrayIsLaunchedAsync();
                counter = 0;
                while (Services.UWPConnectionService._connection == null && counter < 10)
                {
                    await Task.Delay(100);
                    counter++;
                }
            }

            isAvailable = await _uwpConnectionService.GetIsTrayAliveAsync();

            if (!isAvailable)
            {
                MessageDialog dlg = new MessageDialog(_resourceLoader.GetString("Error_SystrayConnect"));
                await dlg.ShowAsync();
            }

            return isAvailable;
        }

        private async Task GetMountStatusAsync()
        {
            if (!await IsSystrayAvailable())
                return;

            var isDokanyInstalled = await _uwpConnectionService.GetIsDokanyInstalledAsync();
            if(!isDokanyInstalled)
            {
                _vm.ShowDokanyMissingInfo();
            }
            _vm.MountsActive = await _uwpConnectionService.GetAreDrivesMountedAsync();
        }

        private async void SystemNavigationManager_CloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
        }

        private async void MountAll_Click(object sender, RoutedEventArgs e)
        {
            if (!await IsSystrayAvailable())
                return;

            _vm.MountsActive = true;
            var mounts = _vm.Mounts.Select(vm => vm.MountParameters).ToList();
            _vaultService.SaveMounts(mounts);

            var messageSent = await _uwpConnectionService.SendMountAllAsync(mounts);
            if (!messageSent)
            {
                MessageDialog dlg = new MessageDialog(_resourceLoader.GetString("Error_Mount"));
                await dlg.ShowAsync();
            }
        }

        private async void UnmountAll_Click(object sender, RoutedEventArgs e)
        {
            if (!await IsSystrayAvailable())
                return;

            _vm.MountsActive = false;
            var messageSent = await _uwpConnectionService.SendUnmountAllAsync();
            if (!messageSent)
            {
                MessageDialog dlg = new MessageDialog(_resourceLoader.GetString("Error_Unmount"));
                await dlg.ShowAsync();
            }
        }

        private void AddMount_Click(object sender, RoutedEventArgs e)
        {
            _vm.Mounts.Add(new ViewModels.MountParameterViewModel(new Contracts.Models.MountParameters()));
        }

        private async void EditCredentials_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            Views.EditCredentialsDialog editCredentialsDlg = new Views.EditCredentialsDialog(button.Tag as MountParameterViewModel);
            await editCredentialsDlg.ShowAsync();
        }

        private void DeleteMount_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            _vm.Mounts.Remove(button.Tag as MountParameterViewModel);
        }

        private async void InstallDokany_Click(object sender, RoutedEventArgs e)
        {
            if (Environment.Is64BitProcess)
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dokan-dev/dokany/releases/download/v1.4.0.1000/Dokan_x64.msi"));
            else
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dokan-dev/dokany/releases/download/v1.4.0.1000/Dokan_x86.msi"));
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            Views.AboutDialog aboutDlg = new Views.AboutDialog();
            await aboutDlg.ShowAsync();
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            Views.SettingsDialog settingsDlg = new Views.SettingsDialog();
            await settingsDlg.ShowAsync();
        }
    }
}
