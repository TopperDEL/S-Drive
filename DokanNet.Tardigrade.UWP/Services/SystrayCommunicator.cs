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
        private static bool _fillTrustStarted = false;
        internal static async Task<bool> LaunchSystrayAsync()
        {
            if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            {
                if(!_fillTrustStarted)
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                _fillTrustStarted = true;
                return true;
            }
            else
                return false;
        }
    }
}
