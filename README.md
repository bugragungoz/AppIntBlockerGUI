⚠️ **Disclaimer:** This project is currently under active development and in a pre-release state. Features may be unstable or subject to change. Please use with caution.

# AppIntBlockerGUI v1.2.0

**Professional Windows Application Firewall Manager**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)
[![Windows](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen.svg)](https://github.com/bugragungoz/AppIntBlockerGUI/actions)

![Block Application View](docs/assets/images/blockApplication.png)

[Download Latest Release](https://github.com/bugragungoz/AppIntBlockerGUI/releases) | [Documentation](docs/) | [Report Bug](https://github.com/bugragungoz/AppIntBlockerGUI/issues) | [Request Feature](https://github.com/bugragungoz/AppIntBlockerGUI/issues)

## Overview

AppIntBlockerGUI is a Windows application that provides an intuitive interface for managing Windows Firewall rules. Built with WPF and following MVVM architectural patterns, it offers firewall management capabilities with a modern, user-friendly interface.

## Features

### Core Functionality
- **Application Blocking**: Block applications from network access
- **Rule Management**: Create, edit, delete, and organize firewall rules
- **Network Monitor**: Real-time dashboard with per-process bandwidth monitoring
- **One-Click Blocking**: Block/unblock processes directly from network monitor
- **Restore Points**: Create and restore firewall configuration snapshots
- **Windows Firewall Integration**: Direct integration with Windows Firewall API

### Network Intelligence
- **Service Detection**: Identifies common network services (HTTP, HTTPS, SSH, FTP, etc.)
- **Traffic Analytics**: Real-time bandwidth monitoring with live graphs
- **Connection Analysis**: TCP/UDP/ICMP connection classification
- **Process Monitoring**: Per-process network usage tracking

### Technical Architecture
- **MVVM Pattern**: Clean separation of concerns
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Async Operations**: Non-blocking UI with cancellation support
- **Comprehensive Logging**: Serilog-based logging system
- **Unit Testing**: MSTest framework with Moq

## Additional Views

| Manage Rules | Restore Points | Windows Firewall | Settings |
| :---: | :---: | :---: | :---: |
| ![Manage Rules View](docs/assets/images/manageRules.png) | ![Restore Points View](docs/assets/images/restorePoints.png) | ![Windows Firewall View](docs/assets/images/windowsFirewall.png) | ![Settings View 1](docs/assets/images/settings1.png) |

## Quick Start

### Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| Windows | 10/11 | Administrator privileges required |
| .NET Desktop Runtime | 8.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Npcap Driver | Latest | [Download](https://npcap.com/) - Install in WinPcap mode |

### Installation

1. **Download** the latest release from [GitHub Releases](https://github.com/bugragungoz/AppIntBlockerGUI/releases/latest)
2. **Install** Npcap in WinPcap API-Compatible Mode for network monitoring
3. **Run** the installer as administrator
4. **Launch** the application (will request administrator privileges automatically)

### Building from Source

```bash
# Clone repository
git clone https://github.com/bugragungoz/AppIntBlockerGUI.git
cd AppIntBlockerGUI

# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release

# Run
cd src && dotnet run
```

## Architecture & Technology

### Framework & Libraries
- **.NET 8.0 WPF** - Desktop application framework
- **MahApps.Metro** - Modern UI components
- **LiveChartsCore.SkiaSharp** - Real-time data visualization
- **Serilog** - Structured logging
- **MSTest + Moq** - Unit testing framework
- **SharpPcap + PacketDotNet** - Network packet capture

### Key Services
- `IFirewallService` - Windows Firewall API integration
- `INetworkMonitorService` - Real-time network monitoring
- `ILoggingService` - Application logging
- `IDialogService` - Modal dialog management
- `INavigationService` - MVVM navigation

## Project Structure

```
AppIntBlockerGUI/
├── src/
│   ├── ViewModels/           # MVVM ViewModels
│   ├── Views/               # WPF Views and UserControls
│   ├── Services/            # Business logic and services
│   ├── Models/              # Data models
│   ├── Converters/          # Value converters
│   ├── Core/                # Core interfaces and utilities
│   └── Resources/           # Themes, styles, and assets
├── Tests/                   # Unit tests
├── docs/                    # Documentation
├── .github/workflows/       # CI/CD pipeline
├── CHANGELOG.md            # Version history
├── LICENSE                 # MIT license
└── README.md               # This file
```

## Security

AppIntBlockerGUI implements security best practices:

- **Input Validation**: Comprehensive sanitization of user inputs
- **Command Injection Protection**: Parameterized commands and argument escaping
- **Path Traversal Prevention**: Path validation and normalization
- **Privilege Management**: Secure administrator elevation handling
- **System Protection**: Critical system directory and file protection

For detailed security information, see [Security Policy](docs/security.md)

## Testing & Quality

- **Unit Tests**: Comprehensive service layer testing with MSTest
- **Mocking**: Isolated testing with Moq framework
- **CI/CD**: Automated testing on every commit
- **Code Quality**: Static analysis and security scanning

## Recent Updates

### v1.2.0 - Enhanced Network Monitoring (January 2025)
- Real-time process bandwidth monitoring
- Interactive 60-second bandwidth graphs
- One-click blocking from network monitor
- Performance counters integration
- Enhanced UI responsiveness

### v1.1.0 - Reliability & Testing (July 2024)
- Cancellation support for long-running operations
- Comprehensive unit test suite
- CI/CD pipeline implementation
- Architectural improvements for testability

See [CHANGELOG.md](CHANGELOG.md) for complete version history.

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on:
- Development setup
- Code style guidelines
- Testing requirements
- Pull request process

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

### Development Partnership
This project was developed with assistance from AI models:
- **Claude 4 Sonnet**: Architecture design and security implementation
- **Gemini 2.5 Pro**: UI/UX optimization and documentation

### Open Source Inspiration
Network monitoring capabilities inspired by [Sniffnet](https://github.com/GyulyVGC/sniffnet) by Giuliano Bellini.

### Technology Stack
- [MahApps.Metro](https://mahapps.com/) - Modern UI framework
- [LiveCharts](https://livecharts.dev/) - Data visualization
- [Serilog](https://serilog.net/) - Structured logging

---

[Report Bug](https://github.com/bugragungoz/AppIntBlockerGUI/issues) • [Request Feature](https://github.com/bugragungoz/AppIntBlockerGUI/issues) • [Join Discussion](https://github.com/bugragungoz/AppIntBlockerGUI/discussions)
