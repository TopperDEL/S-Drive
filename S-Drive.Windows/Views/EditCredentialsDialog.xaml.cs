using S_Drive.Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// Die Elementvorlage "Inhaltsdialogfeld" wird unter https://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace S_Drive.Windows.Views
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
