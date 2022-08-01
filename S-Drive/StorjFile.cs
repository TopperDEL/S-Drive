using DokanNet;
using NC.DokanFS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uplink.NET.Models;

namespace S_Drive
{
    public class StorjFile : IDokanFile
    {
        public FileInformation FileInformation { get; set; }

        public string Id { get; set; }

        public ChunkedUploadOperation ChunkedUpload { get; set; }
    }
}
