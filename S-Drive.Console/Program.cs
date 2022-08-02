using NC.DokanFS;
using S_Drive.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S_Drive.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var storjDisk = new StorjDisk(new uplink.NET.Models.Access(""), "s-drive");
            var dokan = new DokanFrontend(storjDisk, "Storj");

            //MountParameters mountParameters = new MountParameters();
            //mountParameters.DriveLetter = DriveLetters.s;

            ////Via Access-Grant:
            //mountParameters.Bucketname = "s-drive";

            //Via Satellite, API key and Passphrase:
            //mountParameters.SatelliteAddress = args[0];
            //mountParameters.ApiKey = args[1];
            //mountParameters.EncryptionPassphrase = args[2];
            //mountParameters.Bucketname = args[3];
            MountParameters mountParameters = new MountParameters();
            mountParameters.DriveLetter = DriveLetters.s;
            mountParameters.VolumeLabel = "S-Drive";
            storjDisk.MountAsync(mountParameters, dokan).Wait();
        }
    }
}
