using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;

namespace DokanNet.Tardigrade.UWP.Services
{
    class UWPConnectionService
    {
        internal static AppServiceConnection _connection;

        public UWPConnectionService()
        {
        }

        public async Task<bool> SendMountAllAsync()
        {
            var message = new ValueSet();
            message.Add(Messages.MountAll, new List<MountParameters>());
            var result = await _connection.SendMessageAsync(message);
            return result.Status == AppServiceResponseStatus.Success;
        }

        public async Task<bool> SendUnmountAllAsync()
        {
            var message = new ValueSet();
            message.Add(Messages.UnmountAll, "inhalt");
            var result = await _connection.SendMessageAsync(message);
            return result.Status == AppServiceResponseStatus.Success;
        }
    }
}
