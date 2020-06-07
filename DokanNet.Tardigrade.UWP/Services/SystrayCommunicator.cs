using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;

namespace DokanNet.Tardigrade.UWP.Services
{
    class SystrayCommunicator
    {
        internal static async Task<bool> LaunchSystray()
        {
            if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            {
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                return true;
            }
            else
                return false;
        }
    }
}
