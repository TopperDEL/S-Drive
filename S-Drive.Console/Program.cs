using S_Drive.Contracts.Models;
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
            StorjMount storjMount = new StorjMount();

            MountParameters mountParameters = new MountParameters();
            mountParameters.DriveLetter = DriveLetters.s;

            //Via Access-Grant:
            
            //Via Satellite, API key and Passphrase:
            //mountParameters.SatelliteAddress = args[0];
            //mountParameters.ApiKey = args[1];
            //mountParameters.EncryptionPassphrase = args[2];
            //mountParameters.Bucketname = args[3];

            storjMount.MountAsync(mountParameters).Wait();
        }
    }
}
