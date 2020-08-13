@echo off
SET PREV_VERSION=0.2.6.0
SET VERSION=0.2.7.0

echo *** Replacing Versions all over the solution
fart "DokanNet.Tardigrade.Package\Package.appxmanifest" "%PREV_VERSION%" "%VERSION%"
fart "DokanNet.Tardigrade.UWP\Package.appxmanifest" "%PREV_VERSION%" "%VERSION%"
fart "DokanNet.Tardigrade.WiXBootstrapper\Program.cs" "%PREV_VERSION%" "%VERSION%"
fart "DokanNet.Tardigrade.WiXInstaller\Config.wxi" "%PREV_VERSION%" "%VERSION%"
fart "DokanNet.Tardigrade.WiXInstaller\Setup.wxs" "%PREV_VERSION%" "%VERSION%"
fart "DokanNet.Tardigrade.WiXInstaller\DokanNet.Tardigrade.WiXInstaller.wixproj" "%PREV_VERSION%" "%VERSION%"
