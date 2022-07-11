using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using S_Drive.Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace S_Drive.Windows
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public ViewModels.MountViewModel _vm;

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void MountAll_Click(object sender, RoutedEventArgs e)
        {
            //if (!await IsSystrayAvailable())
            //    return;

            //_vm.MountsActive = true;
            //var mounts = _vm.Mounts.Select(vm => vm.MountParameters).ToList();
            //_vaultService.SaveMounts(mounts);

            //var messageSent = await _uwpConnectionService.SendMountAllAsync(mounts);
            //if (!messageSent)
            //{
            //    MessageDialog dlg = new MessageDialog(_resourceLoader.GetString("Error_Mount"));
            //    await dlg.ShowAsync();
            //}
        }

        private async void UnmountAll_Click(object sender, RoutedEventArgs e)
        {
            //if (!await IsSystrayAvailable())
            //    return;

            //_vm.MountsActive = false;
            //var messageSent = await _uwpConnectionService.SendUnmountAllAsync();
            //if (!messageSent)
            //{
            //    MessageDialog dlg = new MessageDialog(_resourceLoader.GetString("Error_Unmount"));
            //    await dlg.ShowAsync();
            //}
        }

        private void AddMount_Click(object sender, RoutedEventArgs e)
        {
            _vm.Mounts.Add(new ViewModels.MountParameterViewModel(new Contracts.Models.MountParameters()));
        }

        private async void EditCredentials_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            //Views.EditCredentialsDialog editCredentialsDlg = new Views.EditCredentialsDialog(button.Tag as MountParameterViewModel);
            //await editCredentialsDlg.ShowAsync();
        }

        private void DeleteMount_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            _vm.Mounts.Remove(button.Tag as MountParameterViewModel);
        }

        private async void InstallDokany_Click(object sender, RoutedEventArgs e)
        {
            //if (Environment.Is64BitProcess)
            //    await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dokan-dev/dokany/releases/download/v1.4.0.1000/Dokan_x64.msi"));
            //else
            //    await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/dokan-dev/dokany/releases/download/v1.4.0.1000/Dokan_x86.msi"));
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            //Views.AboutDialog aboutDlg = new Views.AboutDialog();
            //await aboutDlg.ShowAsync();
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            //Views.SettingsDialog settingsDlg = new Views.SettingsDialog();
            //await settingsDlg.ShowAsync();
        }
    }
}
