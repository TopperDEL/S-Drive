@echo off
SET PREV_VERSION=0.2.7.0
SET VERSION=0.2.8.0

echo *** Replacing Versions all over the solution
fart "S_Drive.Package\Package.appxmanifest" "%PREV_VERSION%" "%VERSION%"
fart "S_Drive.UWP\Package.appxmanifest" "%PREV_VERSION%" "%VERSION%"
fart "S_Drive.WiXBootstrapper\Program.cs" "%PREV_VERSION%" "%VERSION%"
fart "S_Drive.WiXInstaller\Config.wxi" "%PREV_VERSION%" "%VERSION%"
fart "S_Drive.WiXInstaller\Setup.wxs" "%PREV_VERSION%" "%VERSION%"
fart "S_Drive.WiXInstaller\S_Drive.WiXInstaller.wixproj" "%PREV_VERSION%" "%VERSION%"

echo **********
echo Make sure, that msbuild from your VS-Installation is in your path-variable
echo e.g. "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin"
echo **********

echo **********
echo Build APPX-Package for x86
echo **********
msbuild.exe "S_Drive.Package\S_Drive.Package.wapproj" /nologo /verbosity:quiet /consoleloggerparameters:summary /p:configuration="Release" /p:Platform="x86" 
echo **********
echo Build APPX-Package for x64
echo **********
msbuild.exe "S_Drive.Package\S_Drive.Package.wapproj" /nologo /verbosity:quiet /consoleloggerparameters:summary /p:configuration="Release" /p:Platform="x64"

echo **********
echo Build WiX-Installer for Tardigrade-Drive
echo **********
msbuild.exe "S_Drive.WiXInstaller\S_Drive.WiXInstaller.wixproj" /nologo /verbosity:quiet /consoleloggerparameters:summary /p:configuration="Release" /p:Platform="x86"

echo **********
echo Copy the installer
echo **********
copy "S_Drive.WiXInstaller\bin\Release\Tardigrade-Drive.msi" "S_Drive.WiXBootstrapper\Tardigrade-Drive.msi" /Y

echo **********
echo Build WiX#-Bootstrapper for Tardigrade-Drive plus Dokany
echo **********
msbuild.exe "S_Drive.WiXBootstrapper\S_Drive.WiXBootstrapper.csproj" /nologo /verbosity:quiet /consoleloggerparameters:summary /p:configuration="Release" /p:Platform="x86"

echo **********
echo Finished!
echo If everything was alright you'll find the Tardigrade-Drive-Bootstrapper.exe in "S_Drive.WiXBootstrapper\"
echo **********