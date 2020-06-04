using DokanNet.Tardigrade.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            TardigradeMount tardigradeMount = new TardigradeMount();

            MountParameters mountParameters = new MountParameters();
            mountParameters.DriveLetter = MountParameters.DriveLetters.s;
            mountParameters.Bucketname = args[3];
            mountParameters.SatelliteAddress = args[0];
            mountParameters.ApiKey = args[1];
            mountParameters.EncryptionPassphrase = args[2];

            tardigradeMount.MountAsync(mountParameters).Wait();
        }
    }
}
