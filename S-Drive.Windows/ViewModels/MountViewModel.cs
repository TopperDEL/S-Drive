using S_Drive.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace S_Drive.Windows.ViewModels
{
    public class MountViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MountParameterViewModel> Mounts { get; set; }

        private bool _mountsActive = true;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool MountsActive
        {
            get
            {
                return _mountsActive;
            }
            set
            {
                _mountsActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MountsActive)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StartDrivesVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StopDrivesVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MountsInactive)));
            }
        }

        public Visibility StartDrivesVisibility
        {
            get
            {
                return !_mountsActive ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility StopDrivesVisibility
        {
            get
            {
                return _mountsActive ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility DokanyIsMissingInfoVisibility { get; set; } = Visibility.Collapsed;

        public bool MountsInactive
        {
            get
            {
                return !_mountsActive;
            }
        }

        public MountViewModel()
        {
            Mounts = new ObservableCollection<MountParameterViewModel>();
        }

        public void ShowDokanyMissingInfo()
        {
            DokanyIsMissingInfoVisibility = Visibility.Visible;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DokanyIsMissingInfoVisibility)));
        }
    }
}
