using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using S_Drive.Models;

namespace S_Drive.Helpers
{
    public class FolderHelper
    {
        private List<FolderContent> _folderContents;
        public void UpdateFolderTree(List<FolderContent> folderContent)
        {
            _folderContents = folderContent;
        }

        public List<FolderContent> GetContentFor(string currentDirectory)
        {
            if (_folderContents == null)
                return new List<FolderContent>();

            var ret = new List<FolderContent>();

            foreach (var folderContent in _folderContents)
            {
                if (folderContent.Key.StartsWith(currentDirectory))
                {
                    string keyToUse;
                    keyToUse = folderContent.Key;
                    var deeperPart = keyToUse.Substring(currentDirectory.Length);
                    if (deeperPart.StartsWith("/"))
                        deeperPart = deeperPart.Substring(1);
                    bool hasSubFolders = deeperPart.Where(c => c == '/').Count() >= 1;
                    deeperPart = deeperPart.Split('/')[0];
                    if (currentDirectory.EndsWith("/"))
                    {
                        keyToUse = currentDirectory + deeperPart;
                    }
                    else
                    {
                        keyToUse = currentDirectory + "/" + deeperPart;
                    }
                    if (hasSubFolders)
                    {
                        keyToUse = keyToUse + "/";
                    }

                    if (ret.Where(c => c.Key == keyToUse).Count() == 0 && !keyToUse.Contains(StorjDisk.DOKAN_FOLDER))
                        ret.Add(new FolderContent(keyToUse, folderContent.CreationTime, folderContent.ContentLength));
                }
            }

            return ret;
        }
    }
}
