# Build Instructions

## Building the Application

##### 1. Build the main application:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o "Installer\Payload"
```

##### 2. Build the installer:
```powershell
cd .\Installer\
dotnet publish Installer.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
cd ..
```

##### 3. The installer will be at:
```
Installer\bin\Release\net8.0-windows\win-x64\publish\SimpleGSXIntegrator-Installer.exe
```

##### 4. Build the Release (.zip found in /release) 
```powershell
./BuildRelease.ps1
```

## Project Structure

- **Main Application**: Console app that integrates with MSFS and GSX
- **Installer**: Windows Forms wizard that handles installation/uninstallation
- **Payload Folder**: Contains the published application files embedded in the installer

## Distribution

Distribute only the single `SimpleGSXIntegrator-Installer.exe` file. It contains everything needed for installation.
