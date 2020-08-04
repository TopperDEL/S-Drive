using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace DokanNet.Tardigrade.UWP.SysTray.Services
{
    public delegate void MountAll(List<MountParameters> mountList);
    public delegate void UnmountAll();
    public delegate bool DrivesMounted();
    class UWPConnectionService
    {
        static AppServiceConnection connection = null;
        private DokanyCheckService _dokanyCheckService;

        public event MountAll MountAll;
        public event UnmountAll UnmountAll;
        public event DrivesMounted DrivesMounted;

        public UWPConnectionService()
        {
            _dokanyCheckService = new DokanyCheckService();

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

        public async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.ContainsKey(Messages.MountAll))
            {
                MountAll?.Invoke(Newtonsoft.Json.JsonConvert.DeserializeObject<List<MountParameters>>(args.Request.Message[Messages.MountAll].ToString()));
            }
            else if (args.Request.Message.ContainsKey(Messages.UnmountAll.ToString()))
            {
                UnmountAll?.Invoke();
            }
            else if (args.Request.Message.ContainsKey(Messages.AreDrivesMounted.ToString()))
            {
                var drivesMounted = DrivesMounted?.Invoke();
                ValueSet response = new ValueSet();
                var def = args.GetDeferral();
                response.Add(Messages.AreDrivesMountedResult.ToString(), drivesMounted);
                await args.Request.SendResponseAsync(response);
                def.Complete();
            }
            else if (args.Request.Message.ContainsKey(Messages.IsDokanyInstalled.ToString()))
            {
                ValueSet response = new ValueSet();
                var def = args.GetDeferral();
                response.Add(Messages.IsDokanyInstalledResult.ToString(), _dokanyCheckService.IsDokanyInstalled());
                await args.Request.SendResponseAsync(response);
                def.Complete();
            }
            else if (args.Request.Message.ContainsKey(Messages.Ping.ToString()))
            {
                ValueSet response = new ValueSet();
                var def = args.GetDeferral();
                response.Add(Messages.Pong.ToString(), true);
                await args.Request.SendResponseAsync(response);
                def.Complete();
            }
        }
    }
}
