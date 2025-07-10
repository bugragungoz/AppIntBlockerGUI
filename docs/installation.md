---
layout: default
title: Installation Guide
permalink: /installation/
---

<div align="center">

# 📦 **AppIntBlockerGUI Installation Guide**

**Professional Setup Guide for Windows Firewall Management**

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)
![License](https://img.shields.io/badge/License-MIT-green.svg)

</div>

---

## 🎯 **Quick Start (5 Minutes)**

### ⚡ **Express Installation**

1. **📥 Download** → [Latest Release](https://github.com/bugragungoz/AppIntBlockerGUI/releases/latest)
2. **📦 Install** → Run `.msi` installer as administrator
3. **🔧 Configure** → Install Npcap in WinPcap mode
4. **🚀 Launch** → Application auto-elevates privileges

> ✅ **Ready to use in under 5 minutes!**

---

## 📋 **System Requirements**

<table>
<tr>
<th>🏠 <strong>Home Users</strong></th>
<th>🏢 <strong>Enterprise Users</strong></th>
</tr>
<tr>
<td>

| Component | Requirement |
|-----------|-------------|
| **OS** | Windows 10 (1909+) |
| **RAM** | 4 GB |
| **Storage** | 500 MB |
| **Framework** | .NET 8.0 Runtime |
| **Privileges** | Administrator |

</td>
<td>

| Component | Requirement |
|-----------|-------------|
| **OS** | Windows 11 Pro/Enterprise |
| **RAM** | 8 GB+ |
| **Storage** | 2 GB+ |
| **Framework** | .NET 8.0 SDK |
| **Network** | Domain Controller Access |

</td>
</tr>
</table>

---

## 🚀 **Installation Methods**

### 📦 **Method 1: Installer Package (Recommended)**

#### **Step 1: Download**
```bash
# Option A: Direct Download
https://github.com/bugragungoz/AppIntBlockerGUI/releases/latest

# Option B: Using PowerShell
Invoke-WebRequest -Uri "https://api.github.com/repos/bugragungoz/AppIntBlockerGUI/releases/latest" | ConvertFrom-Json | Select-Object -ExpandProperty assets | Where-Object {$_.name -like "*.msi"} | Select-Object -ExpandProperty browser_download_url | Invoke-WebRequest -OutFile "AppIntBlockerGUI.msi"
```

#### **Step 2: Install**
```powershell
# Right-click installer → "Run as administrator"
# OR use command line:
msiexec /i AppIntBlockerGUI.msi /qn
```

#### **Step 3: Verify Installation**
```powershell
# Check if installed successfully
Get-WmiObject -Class Win32_Product | Where-Object {$_.Name -like "*AppIntBlocker*"}
```

### 🛠️ **Method 2: Build from Source**

#### **Developer Setup**
```bash
# Prerequisites check
dotnet --version  # Should be 8.0+
git --version     # Should be 2.30+

# Clone and build
git clone https://github.com/bugragungoz/AppIntBlockerGUI.git
cd AppIntBlockerGUI

# Restore packages
dotnet restore

# Build release version
dotnet build --configuration Release --output ./build

# Optional: Create portable package
dotnet publish src/AppIntBlockerGUI.csproj -c Release -r win-x64 --self-contained false -o ./publish
```

### 🐳 **Method 3: Containerized Development**

#### **Docker Development Environment**
```dockerfile
# Dockerfile.dev
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet build --configuration Release
```

```bash
# Build development container
docker build -f Dockerfile.dev -t appintblocker-dev .

# Run development environment
docker run -it --rm -v ${PWD}:/app appintblocker-dev
```

---

## ⚙️ **Prerequisites Setup**

### 🔧 **Required Components**

#### **.NET 8.0 Desktop Runtime**
```powershell
# Check current version
dotnet --list-runtimes

# Download and install if missing
# https://dotnet.microsoft.com/download/dotnet/8.0
winget install Microsoft.DotNet.DesktopRuntime.8
```

#### **Npcap Network Driver**
```powershell
# Download from https://npcap.com/
# Install with WinPcap API compatibility mode
.\npcap-installer.exe /winpcap_mode=yes /loopback_support=yes
```

#### **Visual C++ Redistributables**
```powershell
# Install latest redistributables
winget install Microsoft.VCRedist.2022.x64
```

### 🛡️ **Security Prerequisites**

#### **Windows Firewall Configuration**
```powershell
# Ensure Windows Firewall is enabled
Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True

# Verify firewall service is running
Get-Service -Name "MpsSvc" | Start-Service

# Check firewall status
Get-NetFirewallProfile | Select-Object Name, Enabled
```

#### **PowerShell Execution Policy**
```powershell
# Check current policy
Get-ExecutionPolicy

# Set appropriate policy (if needed)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

---

## 🎛️ **Configuration & Setup**

### 🔧 **Initial Configuration**

#### **First Launch Setup**
1. **Administrator Elevation**: Application will request UAC elevation
2. **Theme Selection**: Choose preferred interface theme
3. **Network Adapter**: Select primary network interface for monitoring
4. **Logging Level**: Configure verbosity (Info/Debug/Warning/Error)
5. **Auto-Updates**: Enable/disable automatic update checking

#### **Configuration File**
```json
{
  "AppSettings": {
    "Theme": "NulnOilGloss",
    "AutoRefreshInterval": 30,
    "LogLevel": "Info",
    "EnableNetworkMonitoring": true,
    "CreateSystemRestorePoint": true,
    "BackupSettings": {
      "AutoBackup": true,
      "BackupInterval": "Daily",
      "MaxBackupFiles": 10,
      "BackupLocation": "%AppData%\\AppIntBlocker\\Backups"
    }
  },
  "Security": {
    "RequireAdminPrivileges": true,
    "ValidatePathSafety": true,
    "LogSecurityEvents": true
  },
  "Performance": {
    "NetworkScanInterval": 50,
    "MaxProcessesToMonitor": 1000,
    "UseAdaptiveRateLimit": true
  }
}
```

### 🌐 **Network Configuration**

#### **Firewall Rules for AppIntBlockerGUI**
```powershell
# Allow AppIntBlockerGUI through Windows Firewall
New-NetFirewallRule -DisplayName "AppIntBlockerGUI" -Direction Inbound -Program "C:\Program Files\AppIntBlockerGUI\AppIntBlockerGUI.exe" -Action Allow
New-NetFirewallRule -DisplayName "AppIntBlockerGUI" -Direction Outbound -Program "C:\Program Files\AppIntBlockerGUI\AppIntBlockerGUI.exe" -Action Allow
```

#### **Network Adapter Selection**
```powershell
# List available network adapters
Get-NetAdapter | Select-Object Name, InterfaceDescription, Status

# Configure monitoring adapter in app settings
# GUI: Settings → Network → Primary Adapter
```

---

## 🔧 **Advanced Installation Options**

### 🏢 **Enterprise Deployment**

#### **Group Policy Deployment**
```powershell
# Create MSI deployment package
msiexec /a AppIntBlockerGUI.msi /qb TARGETDIR=C:\DeploymentShare

# Deploy via Group Policy
# Computer Configuration → Software Settings → Software Installation
```

#### **Silent Installation with Custom Configuration**
```powershell
# Silent install with custom settings
msiexec /i AppIntBlockerGUI.msi /qn CONFIGFILE="C:\Config\enterprise-config.json"

# Verify deployment
Get-Service -Name "AppIntBlockerGUI*" -ErrorAction SilentlyContinue
```

### 🔄 **Automated Updates**

#### **Update Configuration**
```json
{
  "UpdateSettings": {
    "AutoCheckForUpdates": true,
    "UpdateChannel": "Stable",
    "NotifyBeforeUpdate": true,
    "AutoDownload": true,
    "AutoInstall": false
  }
}
```

#### **Manual Update Process**
```powershell
# Check for updates
Invoke-RestMethod -Uri "https://api.github.com/repos/bugragungoz/AppIntBlockerGUI/releases/latest"

# Download and install update
# Application → Help → Check for Updates
```

---

## 🚨 **Troubleshooting Guide**

### ❌ **Common Installation Issues**

<details>
<summary><strong>🔴 "Access Denied" Error</strong></summary>

**Symptoms**: Cannot install or run application
**Solution**:
```powershell
# 1. Run PowerShell as Administrator
Start-Process PowerShell -Verb RunAs

# 2. Check UAC settings
Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" -Name "EnableLUA"

# 3. Temporarily disable antivirus (if needed)
# 4. Install from safe location (C:\Temp)
```
</details>

<details>
<summary><strong>🟠 ".NET Runtime Missing"</strong></summary>

**Symptoms**: Application won't start, missing framework error
**Solution**:
```powershell
# Install .NET 8.0 Desktop Runtime
winget install Microsoft.DotNet.DesktopRuntime.8

# Alternative: Direct download
Invoke-WebRequest -Uri "https://download.visualstudio.microsoft.com/download/pr/..." -OutFile "dotnet-runtime.exe"
```
</details>

<details>
<summary><strong>🟡 "Npcap Installation Failed"</strong></summary>

**Symptoms**: Network monitoring not working
**Solution**:
```powershell
# 1. Uninstall existing WinPcap/Npcap
wmic product where name="Npcap" call uninstall

# 2. Download latest Npcap
# 3. Install with proper flags
.\npcap-installer.exe /winpcap_mode=yes /admin_only=no

# 4. Restart system
```
</details>

### 🔍 **Diagnostic Tools**

#### **System Information Collection**
```powershell
# Create diagnostic report
$DiagInfo = @{
    OS = Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, Architecture
    DotNet = dotnet --list-runtimes
    Firewall = Get-NetFirewallProfile | Select-Object Name, Enabled
    Services = Get-Service | Where-Object {$_.Name -like "*firewall*" -or $_.Name -like "*npcap*"}
    Adapters = Get-NetAdapter | Select-Object Name, Status, LinkSpeed
}

$DiagInfo | ConvertTo-Json -Depth 3 | Out-File "AppIntBlocker-Diagnostic.json"
```

#### **Log Analysis**
```powershell
# View application logs
Get-Content "$env:LOCALAPPDATA\AppIntBlockerGUI\Logs\*.log" | Select-Object -Last 50

# Filter error messages
Select-String -Path "$env:LOCALAPPDATA\AppIntBlockerGUI\Logs\*.log" -Pattern "ERROR|FATAL"
```

---

## 📞 **Support & Resources**

### 🆘 **Getting Help**

| **Issue Type** | **Contact Method** | **Response Time** |
|----------------|-------------------|-------------------|
| 🐛 **Bugs** | [GitHub Issues](https://github.com/bugragungoz/AppIntBlockerGUI/issues) | 24-48 hours |
| 💡 **Features** | [GitHub Discussions](https://github.com/bugragungoz/AppIntBlockerGUI/discussions) | 1-3 days |
| 🔒 **Security** | [Security Policy](../SECURITY.md) | 24 hours |
| 📚 **Documentation** | [Wiki](https://github.com/bugragungoz/AppIntBlockerGUI/wiki) | Self-service |

### 📖 **Additional Resources**

- **📋 [User Manual](user-manual.md)** - Complete usage guide
- **🔧 [Configuration Reference](configuration.md)** - All settings explained
- **🛡️ [Security Best Practices](security.md)** - Hardening guide
- **🚀 [Performance Tuning](performance.md)** - Optimization tips

---

<div align="center">

**🎉 Installation Complete!**

**Ready to secure your system with professional firewall management.**

[🚀 Get Started](user-manual.md) | [⚙️ Configuration](configuration.md) | [🆘 Support](https://github.com/bugragungoz/AppIntBlockerGUI/issues)

</div>