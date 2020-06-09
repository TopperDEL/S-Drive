using DokanNet.Tardigrade.UWP.ViewModels;
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
        private Services.VaultService _vaultService;
        public ViewModels.MountViewModel _vm;

        public MainPage()
        {
            this.InitializeComponent();

            _vaultService = new Services.VaultService();

            this.DataContext = _vm = new ViewModels.MountViewModel();
            foreach(var mount in _vaultService.LoadMounts())
            {
                _vm.Mounts.Add(new ViewModels.MountParameterViewModel(mount));
            }

            SystemNavigationManagerPreview mgr = SystemNavigationManagerPreview.GetForCurrentView();
            mgr.CloseRequested += SystemNavigationManager_CloseRequested;

            _uwpConnectionService = new Services.UWPConnectionService();
        }

        private async void SystemNavigationManager_CloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
           
        }

        private async void MountAll_Click(object sender, RoutedEventArgs e)
        {
            var mounts = _vm.Mounts.Select(vm => vm.MountParameters).ToList();
            _vaultService.SaveMounts(mounts);

            var messageSent = await _uwpConnectionService.SendMountAllAsync(mounts);
            if (!messageSent)
            {
                MessageDialog dlg = new MessageDialog("error");
                await dlg.ShowAsync();
            }
        }

        private async void UnmountAll_Click(object sender, RoutedEventArgs e)
        {
            var messageSent = await _uwpConnectionService.SendUnmountAllAsync();
            if (!messageSent)
            {
                MessageDialog dlg = new MessageDialog("error");
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
    }
}
