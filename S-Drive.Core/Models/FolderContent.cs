using System;
using System.Collections.Generic;
using System.Text;

namespace S_Drive.Core.Models
{
    public class FolderContent
    {
        public string Key { get; set; }
        public DateTime CreationTime{ get; set; }
        public long ContentLength { get; set; }

        public FolderContent(string key, DateTime creationTime, long contentLength)
        {
            Key = key;
            CreationTime = creationTime;
            ContentLength = contentLength;
        }
    }
}
