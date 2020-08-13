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

echo **********
echo Make sure, that msbuild from your VS-Installation is in your path-variable
echo e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin"
echo **********

echo **********
echo Build APPX-Package for x86
echo **********
msbuild.exe "DokanNet.Tardigrade.Package\DokanNet.Tardigrade.Package.wapproj" /nologo /verbosity:quiet /consoleloggerparameters:summary /p:configuration="Release" /p:Platform="x86" 
echo **********
echo Build APPX-Package for x64
echo **********
msbuild.exe "DokanNet.Tardigrade.Package\DokanNet.Tardigrade.Package.wapproj" /nologo /verbosity:quiet /consoleloggerparameters:summary /p:configuration="Release" /p:Platform="x64"

echo **********
echo Build WiX-Installer for Tardigrade-Drive
echo **********
msbuild.exe "DokanNet.Tardigrade.WiXInstaller\DokanNet.Tardigrade.WiXInstaller.wixproj" /nologo /verbosity:quiet /consoleloggerparameters:summary /p:configuration="Release" /p:Platform="x86"

echo **********
echo Copy the installer
echo **********
copy "DokanNet.Tardigrade.WiXInstaller\bin\Release\Tardigrade-Drive.msi" "DokanNet.Tardigrade.WiXBootstrapper\Tardigrade-Drive.msi" /Y

echo **********
echo Build WiX#-Bootstrapper for Tardigrade-Drive plus Dokany
echo **********
msbuild.exe "DokanNet.Tardigrade.WiXBootstrapper\DokanNet.Tardigrade.WiXBootstrapper.csproj" /nologo /verbosity:quiet /consoleloggerparameters:summary /p:configuration="Release" /p:Platform="x86"

echo **********
echo Finished!
echo If everything was alright you'll find the Tardigrade-Drive-Bootstrapper.exe in "DokanNet.Tardigrade.WiXBootstrapper\"
echo **********