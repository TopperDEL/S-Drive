using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.UWP.ViewModels
{
    public class MountViewModel
    {
        public ObservableCollection<MountParameterViewModel> Mounts { get; set; }

        public MountViewModel()
        {
            Mounts = new ObservableCollection<MountParameterViewModel>();
        }
    }
}
