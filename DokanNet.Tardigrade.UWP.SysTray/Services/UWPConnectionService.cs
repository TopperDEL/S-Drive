using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Windows.ApplicationModel.AppService;

namespace DokanNet.Tardigrade.UWP.SysTray.Services
{
    public delegate void MountAll(List<MountParameters> mountList);
    public delegate void UnmountAll();
    class UWPConnectionService
    {
        static AppServiceConnection connection = null;

        public event MountAll MountAll;
        public event UnmountAll UnmountAll;

        public UWPConnectionService()
        {
            Thread appServiceThread = new Thread(new ThreadStart(InitAsync));
            appServiceThread.Start();
        }

        private async void InitAsync()
        {
            connection = new AppServiceConnection();
            connection.AppServiceName = "SystrayExtensionService";
            connection.PackageFamilyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            connection.RequestReceived += Connection_RequestReceived;

            AppServiceConnectionStatus status = await connection.OpenAsync();
        }

        public void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.ContainsKey(Messages.MountAll))
            {
                MountAll?.Invoke(Newtonsoft.Json.JsonConvert.DeserializeObject<List<MountParameters>>(args.Request.Message[Messages.MountAll].ToString()));
            }
            else if (args.Request.Message.ContainsKey(Messages.UnmountAll.ToString()))
            {
                UnmountAll?.Invoke();
            }
        }
    }
}
