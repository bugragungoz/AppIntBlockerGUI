⚠️ **Disclaimer:** This project is currently under active development and in a pre-release state. Features may be unstable or subject to change. Please use with caution.

# AppIntBlockerGUI v1.0

![Block Application View](assets/images/blockApplication.png)

### Additional Views

| Manage Rules | Restore Points | Windows Firewall | Settings (General) | Settings (Theme) |
| :---: | :---: | :---: | :---: | :---: |
| ![Manage Rules View](assets/images/manageRules.png) | ![Restore Points View](assets/images/restorePoints.png) | ![Windows Firewall View](assets/images/windowsFirewall.png) | ![Settings View 1](assets/images/settings1.png) | ![Settings View 2](assets/images/settings2.png) |

### Loading & Dialogs

| Loading Screen | Admin Privileges | Permission Denied | Operation Cancelled |
| :---: | :---: | :---: | :---: |
| ![Loading Screen](assets/images/loadingScreen.png) | ![Admin Privileges](assets/images/adminPrivileges.png) | ![Permission Denied](assets/images/permissionDenied.png) | ![Operation Cancelled](assets/images/operationCancelled.png) |


**Professional Windows Application Firewall Manager with Modern UI**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)

## Overview

AppIntBlockerGUI is a sophisticated Windows application that provides an intuitive interface for managing Windows Firewall rules. Built with modern WPF technology and following MVVM architectural patterns, it offers enterprise-grade firewall management capabilities with a beautiful, user-friendly interface.

Developed with the assistance of advanced AI models including Claude 4 Sonnet and Gemini 2.5 Pro to ensure modern architectural patterns and best practices.

## Features

### Core Functionality
- **Application Blocking**: Easily block applications from network access
- **Rule Management**: Create, edit, and delete firewall rules with advanced options
- **Restore Points**: Create and restore firewall configuration snapshots
- **Windows Firewall Integration**: Direct integration with Windows Firewall API
- **Real-time Monitoring**: Live status updates and rule validation
- **Administrator Privilege Management**: Automatic privilege checking and elevation

### Technical Excellence
- **MVVM Architecture**: Clean separation of concerns
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Async/Await Patterns**: Non-blocking UI operations
- **Error Handling**: Comprehensive exception management
- **Logging System**: Detailed application logging
- **Performance Optimized**: Efficient rule scanning and management

## Quick Start

### Prerequisites
- Windows 10/11
- .NET 8.0 Runtime
- Administrator privileges (the application will request them automatically)

### Installation

**1. Open a Terminal**

*Open your terminal or command prompt (e.g., PowerShell, Windows Terminal, or CMD).*


**2. Clone the Repository**
```bash
git clone https://github.com/bugragungoz/AppIntBlockerGUI.git
```

**3. Navigate to the Source Directory**
```bash
cd AppIntBlockerGUI/src
```

**4. Build the Application**
```bash
dotnet build
```

**5. Run the Application**
```bash
dotnet run
```
*The application will automatically request administrator privileges if needed.*

## Project Structure

```
AppIntBlockerGUI/
├── src/
│   ├── ViewModels/                # MVVM ViewModels
│   ├── Views/                     # WPF Views and UserControls
│   ├── Services/                  # Business logic and services
│   ├── Models/                    # Data models
│   ├── Converters/                # Value converters
│   └── Resources/                 # Themes and resources
├── docs/                          # Documentation
│   ├── api.md
│   ├── bug_fixes_applied.md
│   ├── changelog.md
│   ├── code_structure_analysis.md
│   ├── github_pages_setup_report.md
│   ├── index.md
│   ├── installation.md
│   ├── roadmap.md
│   └── security.md
├── .gitignore
├── CHANGELOG.md
├── LICENSE
└── README.md
```

## Configuration

### Theme Customization
The application supports custom themes. Edit `Resources/Themes/NulnOilGlossTheme.xaml` to customize colors and styling.

### Logging
Logs are automatically created in the `Logs/` directory. Configure logging levels in the application settings.

## Development

### Architecture
- **Pattern**: MVVM (Model-View-ViewModel)
- **Framework**: WPF (.NET 8.0)
- **UI Library**: MahApps.Metro with Extended.Wpf.Toolkit
- **Icons**: MahApps.Metro.IconPacks
- **Charts**: ScottPlot.WPF
- **DI Container**: Microsoft.Extensions.DependencyInjection

### Key Services
- `IFirewallService`: Windows Firewall API integration
- `INavigationService`: MVVM navigation management
- `IThemeService`: Dynamic theme switching
- `ILoggingService`: Application logging
- `IDialogService`: Modal dialog management

### Building from Source
```bash
# Clone repository
git clone https://github.com/bugragungoz/AppIntBlockerGUI.git
cd AppIntBlockerGUI/src

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run
```

## Performance Notes

- **Memory Usage**: Optimized for minimal memory footprint
- **Startup Time**: Fast application startup with automatic privilege checking
- **UI Responsiveness**: Non-blocking operations with progress indicators
- **Rule Processing**: Efficient handling of Windows Firewall rules

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Related Projects

- [Windows Firewall API Documentation](https://docs.microsoft.com/en-us/windows/win32/api/netfw/)
- [MahApps.Metro](https://mahapps.com/)
- [ScottPlot](https://scottplot.net/)

## AI Development Attribution

This project was developed with the assistance of advanced AI models:
- **Claude 4 Sonnet**: Architecture design, code structure, and best practices
- **Gemini 2.5 Pro**: UI/UX patterns, documentation, and optimization strategies

The use of AI tools enabled rapid prototyping, adherence to modern development patterns, and comprehensive documentation while maintaining high code quality standards.

## Security & Bug Fixes (2025)

In 2025, critical security vulnerabilities and major bugs were addressed with the help of AI-assisted code review and patching. For full details, please see `SECURITY_ANALYSIS.md` and `bug_fixes_applied.md`.

- Command-line (netsh) and PowerShell injection vulnerabilities fixed
- Path traversal and input validation significantly improved
- Error message sanitization to prevent sensitive data leaks
- Settings file is now encrypted (DPAPI)

> These security patches and fixes were proposed and implemented with the assistance of AI tools.

---

**croxz** 