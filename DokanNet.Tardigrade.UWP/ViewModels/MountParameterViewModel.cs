using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.UWP.ViewModels
{
    public class MountParameterViewModel:INotifyPropertyChanged
    {
        public MountParameters MountParameters { get; set; }

        public List<string> DriveLetterList { get; set; }
        public string SelectedDriveLetter
        {
            get
            {
                return MountParameters.DriveLetter.ToString();
            }

            set
            {
                MountParameters.DriveLetter = (DriveLetters)Enum.Parse(typeof(DriveLetters), value);
            }
        }

        public bool UseAuthMethod_AccessGrant
        {
            get
            {
                return MountParameters.AuthMethod == Contracts.Models.AuthMethods.AccessGrant;
            }
            set
            {
                if (value)
                    MountParameters.AuthMethod = Contracts.Models.AuthMethods.AccessGrant;
                else
                    MountParameters.AuthMethod = Contracts.Models.AuthMethods.APIkey;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseAuthMethod_AccessGrant)));
            }
        }

        public bool UseAuthMethod_APIkey
        {
            get
            {
                return MountParameters.AuthMethod == Contracts.Models.AuthMethods.APIkey;
            }
            set
            {
                if (value)
                    MountParameters.AuthMethod = Contracts.Models.AuthMethods.APIkey;
                else
                    MountParameters.AuthMethod = Contracts.Models.AuthMethods.AccessGrant;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseAuthMethod_APIkey)));
            }
        }

        public MountParameterViewModel(MountParameters mountParameters)
        {
            MountParameters = mountParameters;

            DriveLetterList = new List<string>();
            foreach (var value in Enum.GetValues(typeof(DriveLetters)))
                DriveLetterList.Add(value.ToString());
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
