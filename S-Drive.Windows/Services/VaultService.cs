using S_Drive.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Credentials;

namespace S_Drive.WinUI.Services
{
    class VaultService
    {
        private PasswordVault _vault = new PasswordVault();

        public List<MountParameters> LoadMounts()
        {
            List<MountParameters> ret = new List<MountParameters>();
            try
            {
                var saved = _vault.Retrieve("mounts", "s_drive");
                if (saved != null)
                {
                    ret = Newtonsoft.Json.JsonConvert.DeserializeObject<List<MountParameters>>(saved.Password);
                }
            }
            catch { }
            return ret;
        }

        public void SaveMounts(List<MountParameters> mounts)
        {
            _vault.Add(new PasswordCredential("mounts", "s_drive", Newtonsoft.Json.JsonConvert.SerializeObject(mounts)));
        }
    }
}
