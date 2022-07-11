using System;
using WixSharp;
using WixSharp.Bootstrapper;

namespace S_Drive.WiXBootstrapper
{
    class Program
    {
        static void Main()
        {
            var bootstrapper =
              new Bundle("Tardigrade-Drive",
                  new MsiPackage("Dokan_x64.msi") { InstallCondition = "VersionNT64" },
                  new MsiPackage("Dokan_x86.msi") { InstallCondition = "Not VersionNT64" },
                  new MsiPackage("Tardigrade-Drive.msi") { DisplayInternalUI = true });

            bootstrapper.Version = new Version("0.2.8.0");
            bootstrapper.IconFile = "Storj-symbol_32x32.ico";
            bootstrapper.Application.LogoFile = "Storj-symbol.png";
            bootstrapper.UpgradeCode = new Guid("6f330b47-2577-43ad-9095-1861bb25844b");
            // bootstrapper.Application = new SilentBootstrapperApplication();
            // bootstrapper.PreserveTempFiles = true;

            bootstrapper.Build("Tardigrade-Drive-Bootstrapper.exe");
        }
    }
}