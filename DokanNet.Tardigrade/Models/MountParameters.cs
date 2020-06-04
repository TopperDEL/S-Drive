using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.Models
{
    public class MountParameters
    {
        public enum DriveLetters
        {
            a,
            b,
            c,
            d,
            e,
            f,
            g,
            h,
            i,
            j,
            k,
            l,
            m,
            n,
            o,
            p,
            q,
            r,
            s,
            t,
            u,
            v,
            w,
            x,
            y,
            z
        }
        public string Bucketname { get; set; }
        public DriveLetters DriveLetter { get; set; }
        public string SatelliteAddress { get; set; }
        public string ApiKey { get; set; }
        public string EncryptionPassphrase { get; set; }
        public string AccessGrant { get; set; }

        public MountParameters()
        {
            DriveLetter = DriveLetters.s;
        }
    }
}
