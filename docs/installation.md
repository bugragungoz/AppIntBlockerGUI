---
layout: default
title: Installation Guide
permalink: /installation/
---

# Installation Guide

This guide provides step-by-step instructions for installing and setting up AppIntBlockerGUI on your Windows system.

---

## 📋 System Requirements

### Minimum Requirements
- **Operating System**: Windows 10 (Build 1809) or Windows 11
- **Framework**: .NET 8.0 Runtime
- **Memory**: 4 GB RAM
- **Storage**: 500 MB available space
- **Permissions**: Administrator privileges

### Recommended Requirements
- **Operating System**: Windows 11 (latest version)
- **Framework**: .NET 8.0 SDK (for development)
- **Memory**: 8 GB RAM
- **Storage**: 1 GB available space
- **Display**: 1920x1080 resolution

---

## 🚀 Quick Installation

### Option 1: Binary Download (Recommended)
```bash
# 1. Download the latest release
https://github.com/bugragungoz/AppIntBlockerGUI/releases/latest

# 2. Extract the archive
# 3. Run AppIntBlockerGUI.exe as Administrator
```

### Option 2: Build from Source
```bash
# 1. Clone the repository
git clone https://github.com/bugragungoz/AppIntBlockerGUI.git

# 2. Navigate to source directory
cd AppIntBlockerGUI/src

# 3. Restore dependencies
dotnet restore

# 4. Build the application
dotnet build --configuration Release

# 5. Run the application
dotnet run
```

---

## 🔧 Detailed Installation Steps

### Step 1: Install Prerequisites

#### .NET 8.0 Runtime
1. Visit the [.NET Download Page](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Download the **Windows x64 Desktop Runtime**
3. Run the installer with administrator privileges
4. Verify installation:
   ```cmd
   dotnet --version
   ```

#### Visual C++ Redistributable (if needed)
1. Download from [Microsoft](https://docs.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)
2. Install the x64 version

### Step 2: Download AppIntBlockerGUI

#### Using Git
```bash
git clone https://github.com/bugragungoz/AppIntBlockerGUI.git
cd AppIntBlockerGUI
```

#### Using ZIP Download
1. Go to [GitHub Repository](https://github.com/bugragungoz/AppIntBlockerGUI)
2. Click **Code > Download ZIP**
3. Extract to your preferred location

### Step 3: Build the Application

#### For Users (Runtime Only)
```bash
cd src
dotnet publish -c Release -r win-x64 --self-contained false
```

#### For Developers (Full SDK)
```bash
cd src
dotnet restore
dotnet build -c Release
```

### Step 4: Run the Application

#### First Launch
1. Navigate to the output directory
2. Right-click `AppIntBlockerGUI.exe`
3. Select **"Run as administrator"**
4. Accept the UAC prompt

#### Subsequent Launches
The application will automatically request administrator privileges when needed.

---

## ⚙️ Configuration

### Initial Setup
1. **Theme Selection**: Choose your preferred theme (Dark/Light)
2. **Auto-Refresh**: Configure automatic rule refresh intervals
3. **Logging Level**: Set logging verbosity (Info/Debug/Warning/Error)
4. **Backup Settings**: Configure automatic backup creation

### Advanced Configuration
Edit the `appsettings.json` file to customize:

```json
{
  "Theme": "NulnOilGloss",
  "AutoRefreshInterval": 30,
  "LogLevel": "Info",
  "CreateBackupOnStartup": true,
  "MaxBackupFiles": 10
}
```

---

## 🔒 Security Considerations

### Administrator Privileges
AppIntBlockerGUI requires administrator privileges to:
- Access Windows Firewall API
- Create and modify firewall rules
- Create system restore points
- Access system directories

### Windows Defender
Add the application to Windows Defender exclusions if needed:
1. Open **Windows Security**
2. Go to **Virus & threat protection**
3. Manage settings under **Virus & threat protection settings**
4. Add exclusion for the application folder

### Firewall Configuration
Ensure Windows Firewall is enabled and properly configured:
```powershell
# Check firewall status
Get-NetFirewallProfile | Select-Object Name, Enabled

# Enable firewall if disabled
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True
```

---

## 🚨 Troubleshooting

### Common Issues

#### "Access Denied" Error
**Problem**: Application cannot access firewall settings
**Solution**:
1. Ensure you're running as administrator
2. Check UAC settings (should be enabled)
3. Verify Windows Firewall service is running

#### ".NET Runtime Not Found"
**Problem**: Missing .NET 8.0 runtime
**Solution**:
1. Download from [Microsoft .NET](https://dotnet.microsoft.com/download)
2. Install the Desktop Runtime (not just ASP.NET Core)
3. Restart the application

#### "PowerShell Execution Policy" Error
**Problem**: PowerShell scripts blocked by execution policy
**Solution**:
```powershell
# Check current policy
Get-ExecutionPolicy

# Set policy (as Administrator)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope LocalMachine
```

#### Theme Not Loading
**Problem**: UI appears with default styling
**Solution**:
1. Check `Resources/Themes/` folder exists
2. Verify theme files are not corrupted
3. Reset to default theme in settings

### Performance Issues

#### Slow Rule Loading
- **Cause**: Large number of existing firewall rules
- **Solution**: Use filtering and search features

#### High Memory Usage
- **Cause**: Memory leaks in long-running sessions
- **Solution**: Restart the application periodically

#### UI Freezing
- **Cause**: Synchronous operations on UI thread
- **Solution**: Report as bug - operations should be async

---

## 🔄 Updating

### Automatic Updates (Future Feature)
AppIntBlockerGUI will support automatic updates in future versions.

### Manual Updates
1. Download the latest release
2. Close the current application
3. Replace the executable files
4. Restart as administrator

### Preserving Settings
Settings are stored in:
```
%APPDATA%\AppIntBlockerGUI\settings.json
```

Backup this file before updating to preserve your configuration.

---

## 🗑️ Uninstallation

### Standard Uninstall
1. Close the application
2. Delete the application folder
3. Remove settings folder:
   ```
   %APPDATA%\AppIntBlockerGUI\
   ```

### Complete Cleanup
1. Remove any created firewall rules (optional)
2. Delete log files:
   ```
   %TEMP%\AppIntBlockerGUI\Logs\
   ```
3. Clear Windows Event Logs (optional)

### Restore Original Firewall Rules
If you want to remove all rules created by AppIntBlockerGUI:
1. Open the application
2. Go to **Manage Rules**
3. Filter by "Created by AppIntBlockerGUI"
4. Select all and delete

---

## 📞 Getting Help

### Documentation
- [API Documentation](../api/) - Technical reference
- [User Guide](../user-guide/) - Feature documentation
- [FAQ](../faq/) - Frequently asked questions

### Support Channels
- **GitHub Issues**: [Report Bugs](https://github.com/bugragungoz/AppIntBlockerGUI/issues)
- **Discussions**: [Community Forum](https://github.com/bugragungoz/AppIntBlockerGUI/discussions)
- **Wiki**: [Knowledge Base](https://github.com/bugragungoz/AppIntBlockerGUI/wiki)

### System Information
When reporting issues, include:
```cmd
# System information
systeminfo | findstr /B /C:"OS Name" /C:"OS Version" /C:"System Type"

# .NET version
dotnet --info

# PowerShell version
$PSVersionTable.PSVersion
```

---

## 🧪 Development Setup

### Additional Requirements for Development
- **Visual Studio 2022** or **Visual Studio Code**
- **.NET 8.0 SDK** (not just runtime)
- **Git** for version control

### Development Installation
```bash
# Clone repository
git clone https://github.com/bugragungoz/AppIntBlockerGUI.git
cd AppIntBlockerGUI

# Restore packages
dotnet restore

# Build in debug mode
dotnet build

# Run with debugger
dotnet run --project src/AppIntBlockerGUI.csproj
```

### IDE Configuration
#### Visual Studio
1. Open `AppIntBlockerGUI.sln`
2. Set startup project to `AppIntBlockerGUI`
3. Ensure "Run as Administrator" is configured

#### Visual Studio Code
1. Install C# extension
2. Open the `src` folder
3. Configure launch.json for admin privileges

---

*For additional help, please visit our [GitHub repository](https://github.com/bugragungoz/AppIntBlockerGUI) or create an issue.*