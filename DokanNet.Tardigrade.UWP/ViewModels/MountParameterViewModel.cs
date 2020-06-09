using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.UWP.ViewModels
{
    public class MountParameterViewModel
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

        public MountParameterViewModel(MountParameters mountParameters)
        {
            MountParameters = mountParameters;

            DriveLetterList = new List<string>();
            foreach (var value in Enum.GetValues(typeof(DriveLetters)))
                DriveLetterList.Add(value.ToString());
        }
    }
}
