using DokanNet.Tardigrade.Contracts.Models;
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
            mountParameters.DriveLetter = DriveLetters.s;

            //Via Access-Grant:
            mountParameters.Bucketname = args[0];
            mountParameters.AccessGrant = args[1];

            //Via Satellite, API key and Passphrase:
            //mountParameters.SatelliteAddress = args[0];
            //mountParameters.ApiKey = args[1];
            //mountParameters.EncryptionPassphrase = args[2];
            //mountParameters.Bucketname = args[3];

            tardigradeMount.MountAsync(mountParameters).Wait();
        }
    }
}
