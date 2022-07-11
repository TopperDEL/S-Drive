using S_Drive.UWP.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Die Elementvorlage "Inhaltsdialogfeld" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace S_Drive.UWP.Views
{
   
    public sealed partial class EditCredentialsDialog : ContentDialog
    {
        public MountParameterViewModel MountParametersVM { get; set; }

        public EditCredentialsDialog(MountParameterViewModel mountParametersVM)
        {
            this.InitializeComponent();

            MountParametersVM = mountParametersVM;
        }

        private void OKClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            //Nothing to do here
        }
    }
}
