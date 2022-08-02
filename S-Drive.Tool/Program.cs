using CommandLine;
using NC.DokanFS;
using S_Drive;
using S_Drive.Tool;
using System.Reflection;

Parser.Default.ParseArguments<CommandLineOptions>(args)
    .WithParsed(o =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version;
        Console.WriteLine($"Welcome to S-Drive v{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}!");
        Console.WriteLine("Checking for Dokany 2...");

        if (!System.IO.File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dokan2.dll")))
        {
            Console.WriteLine("Dokany is not installed - please install Dokany 2 from here:");
            if (Environment.Is64BitProcess)
                Console.WriteLine("https://github.com/dokan-dev/dokany/releases/download/v2.0.5.1000/Dokan_x64.msi");
            else
                Console.WriteLine("https://github.com/dokan-dev/dokany/releases/download/v2.0.5.1000/Dokan_x86.msi");

            return;
        }
        else
        {
            Console.WriteLine("Dokany is installed!");
        }

        var storjDisk = new StorjDisk(new uplink.NET.Models.Access(o.AccessGrant), o.BucketName);
        var dokan = new DokanFrontend(storjDisk, "Storj");

        var driveLetter = (DriveLetters)Enum.Parse(typeof(DriveLetters), o.DriveLetter.ToLower());
        MountParameters mountParameters = new MountParameters();
        mountParameters.DriveLetter = driveLetter;
        mountParameters.VolumeLabel = o.DriveLabel;
        Console.WriteLine($"Starting drive '{o.DriveLetter}' with label '{o.DriveLabel}'...");
        storjDisk.MountAsync(mountParameters, dokan).Wait();
    });
