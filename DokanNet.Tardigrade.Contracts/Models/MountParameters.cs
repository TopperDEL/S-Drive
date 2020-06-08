using System;
using System.Collections.Generic;
using System.Text;

namespace DokanNet.Tardigrade.Contracts.Models
{
    public class MountParameters
    {   
        public string Bucketname { get; set; }
        public DriveLetters DriveLetter { get; set; }
        public string SatelliteAddress { get; set; }
        public string ApiKey { get; set; }
        public string EncryptionPassphrase { get; set; }
        public string AccessGrant { get; set; }
        public string VolumeLabel { get; set; }

        public MountParameters()
        {
            DriveLetter = DriveLetters.s;
            VolumeLabel = "Tardigrade";
        }
    }
}
