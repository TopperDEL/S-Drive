# DokanNet.Tardigrade
A Dokany-Wrapper to access Storj/Tardigrade as a Windows-Drive

This is the source-code for the "Tardigrade-Drive"-App available in the Windows Store. It enables you to mount one or more storj-buckets as a virtual harddrive in you explorer. You may compile it for yourself or get the version from the store. For this app to work you must have Dokany installed (which you can get [here](https://github.com/dokan-dev/dokany/releases)).
The version in the store is a very old one as Microsoft does not allow deploying drivers with a Store-App though it would be possible to do so. Microsoft even does not allow to tell the user to install that by himself. Therefore the app is currently published with a WiX-Installer.

## Build
The documentation is not finished, yet. But if you want to build this on your own - make sure you have the following installed:

* WiX Toolkit and the WiX Visual Studio Integration (from [here](https://wixtoolset.org/))
* Windows SDK (from [here](https://developer.microsoft.com/de-de/windows/downloads/windows-10-sdk/))
* The .Net-Framework 4.8 (the project will switch to .Net 6 in the future)
* The Dokany-Driver (DokanSetup.exe from [here](https://github.com/dokan-dev/dokany/releases))
* You may need to select the windows.winmd from a path like the following: "c:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.19041.0\windows.winmd". That dependencies is necessary as a reference within the DokanNet.Tardigrade.UWP.SysTray-Project
