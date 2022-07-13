using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace S_Drive.Helpers
{
    public class FolderHelper
    {
        private List<string> _keys;
        public void UpdateFolderTree(List<string> keys)
        {
            _keys = keys;
        }

        public List<string> GetContentFor(string currentDirectory)
        {
            if (_keys == null)
                return new List<string>();

            var ret = new List<string>();

            foreach (var key in _keys)
            {
                if (key.StartsWith(currentDirectory))
                {
                    string keyToUse;
                    keyToUse = key;
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
                    if(hasSubFolders)
                    {
                        keyToUse = keyToUse + "/";
                    }

                    if (!ret.Contains(keyToUse))
                        ret.Add(keyToUse);
                }
            }

            return ret;
        }
    }
}
