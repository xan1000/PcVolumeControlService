# PcVolumeControlService

This project is based on:

https://github.com/PcVolumeControl

The server has been updated to run on .NET Core 3.1 and can be installed as a service.

The following packages have been updated:

| Package                          | Updated Version | Previous Version |
|----------------------------------|-----------------|------------------|
| AudioSwitcher.AudioApi           | 4.0.0-alpha5    | 3.0.0            |
| AudioSwitcher.AudioApi.CoreAudio | 4.0.0-alpha5    | 3.0.0.1          |

This helps reduce the continuous background CPU usage used by the current stable version of these packages.

# Build Service Commands

```
dotnet build -c Release
dotnet publish -c Release -r win-x64
```

**OR**

```
dotnet build -c Release && dotnet publish -c Release -r win-x64 --no-self-contained
```

# Create Service Commands

```
sc create PcVolumeControlService BinPath="<path-to-repository>\PcVolumeControlService\bin\Release\net6.0\win-x64\PcVolumeControlService.exe"
```

# Start, Stop & Delete Service Commands

```
sc start PcVolumeControlService
sc stop PcVolumeControlService
sc delete PcVolumeControlService
```
