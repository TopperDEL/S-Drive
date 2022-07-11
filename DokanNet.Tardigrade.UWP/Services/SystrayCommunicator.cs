using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;

namespace S_Drive.UWP.Services
{
    class SystrayCommunicator
    {
        public static bool _fillTrustStarted = false;
        internal static async Task<bool> AssureSystrayIsLaunchedAsync()
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
