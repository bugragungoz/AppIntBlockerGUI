⚠️ **Disclaimer:** This project is currently under active development and in a pre-release state. Features may be unstable or subject to change. Please use with caution.

# AppIntBlockerGUI v1.0

![AppIntBlockerGUI Showcase](src/images/mainView.png)

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
- Administrator privileges (automatically requested)

### Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/bugragungoz/AppIntBlockerGUI.git
   cd AppIntBlockerGUI
   ```

2. Build the application:
   ```bash
   cd src/AppIntBlockerGUI
   dotnet build --configuration Release
   ```

3. Run the application:
   ```bash
   dotnet run
   ```
   *The application will automatically request administrator privileges if needed.*

## Project Structure

```
AppIntBlockerGUI/
├── src/
│   └── AppIntBlockerGUI/          # Main WPF application
│       ├── ViewModels/            # MVVM ViewModels
│       ├── Views/                 # WPF Views and UserControls
│       ├── Services/              # Business logic and services
│       ├── Models/                # Data models
│       ├── Converters/            # Value converters
│       └── Resources/             # Themes and resources
├── docs/                          # Documentation
├── CHANGELOG.md                   # Version history
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
cd AppIntBlockerGUI/src/AppIntBlockerGUI

# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release

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

---

**croxz** 