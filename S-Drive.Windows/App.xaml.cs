using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace S_Drive.Windows
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static TaskbarIcon? TrayIcon { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            InitializeTrayIcon();

            m_window = new MainWindow();
            m_window.Closed += (sender, args) => { args.Handled = true; m_window.Hide(); };
            m_window.Activate();
        }

        private Window m_window;

        private void InitializeTrayIcon()
        {
            TrayIcon = (TaskbarIcon)Resources["TrayIcon"];

            var showHideWindowCommand = (XamlUICommand)Resources["ShowHideWindowCommand"];
            showHideWindowCommand.ExecuteRequested += (sender, e) =>
            {
                if (m_window.Visible)
                {
                    m_window.Hide();
                }
                else
                {
                    m_window.Show();
                }
            };

            var exitApplicationCommand = (XamlUICommand)Resources["ExitApplicationCommand"];
            exitApplicationCommand.ExecuteRequested += (sender, e) =>
            {
                TrayIcon?.Dispose();
                m_window?.Close();
            };

            TrayIcon.ForceCreate();
        }
    }
}
