using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Data.Json;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;

namespace DokanNet.Tardigrade.UWP.Services
{
    class UWPConnectionService
    {
        internal static AppServiceConnection _connection;
        internal static BackgroundTaskDeferral _deferral = null;

        public async Task<bool> SendMountAllAsync(List<MountParameters> mounts)
        {
            try
            {
                var message = new ValueSet();
                message.Add(Messages.MountAll, Newtonsoft.Json.JsonConvert.SerializeObject(mounts));
                var result = await _connection.SendMessageAsync(message);
                return result.Status == AppServiceResponseStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SendUnmountAllAsync()
        {
            try
            {
                var message = new ValueSet();
                message.Add(Messages.UnmountAll, "");
                var result = await _connection.SendMessageAsync(message);
                return result.Status == AppServiceResponseStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> GetAreDrivesMounted()
        {
            try
            {
                var message = new ValueSet();
                message.Add(Messages.AreDrivesMounted, "");
                var result = await _connection.SendMessageAsync(message);
                if (result.Status == AppServiceResponseStatus.Success)
                {
                    var response = result.Message.FirstOrDefault();
                    if (response.Key == Messages.AreDrivesMountedResult)
                    {
                        return (bool)response.Value;
                    }
                }
            }
            catch { }
            return false;
        }

        public async Task<bool> GetIsDokanyInstalled()
        {
            try
            {
                var message = new ValueSet();
                message.Add(Messages.IsDokanyInstalled, "");
                var result = await _connection.SendMessageAsync(message);
                if (result.Status == AppServiceResponseStatus.Success)
                {
                    var response = result.Message.FirstOrDefault();
                    if (response.Key == Messages.IsDokanyInstalledResult)
                    {
                        return (bool)response.Value;
                    }
                }
            }
            catch { }
            return false;
        }

        public async Task<bool> GetIsTrayAlive()
        {
            try
            {
                var message = new ValueSet();
                message.Add(Messages.Ping, "");
                var result = await _connection.SendMessageAsync(message);
                if (result.Status == AppServiceResponseStatus.Success)
                {
                    var response = result.Message.FirstOrDefault();
                    if (response.Key == Messages.Pong)
                    {
                        return (bool)response.Value;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
