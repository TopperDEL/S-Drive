using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S_Drive.Tool
{
    public class CommandLineOptions
    {
        [Option('g',"access-grant", Required = true, HelpText = "The access grant on Storj DCS, that should be used.")]
        public string AccessGrant { get; set; }

        [Option('b', "bucket-name", Required = true, HelpText = "The name of the bucket that should be used.")]
        public string BucketName { get; set; }

        [Option('l', "drive-label", Required = true, HelpText = "The name of the drive to create - will appear as name in the Windows Explorer.")]
        public string DriveLabel { get; set; }

        [Option('d', "drive-letter", Required = true, HelpText = "The drive letter of the hard drive to use - must not be in use already.")]
        public string DriveLetter { get; set; }
    }
}
