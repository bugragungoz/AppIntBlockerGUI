---
layout: default
title: Home
permalink: /
---

# AppIntBlockerGUI v1.0

![Block Application View](assets/images/blockApplication.png)

**Professional Windows Application Firewall Manager with Modern UI**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)
[![GitHub](https://img.shields.io/badge/GitHub-Repository-black.svg)](https://github.com/bugragungoz/AppIntBlockerGUI)

---

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- .NET 8.0 Runtime
- Administrator privileges

### Installation
```bash
git clone https://github.com/bugragungoz/AppIntBlockerGUI.git
cd AppIntBlockerGUI/src
dotnet build
dotnet run
```

---

## ✨ Key Features

### 🛡️ **Firewall Management**
- **Application Blocking**: Intuitive interface for blocking network access
- **Rule Management**: Create, edit, and delete firewall rules
- **Real-time Monitoring**: Live status updates and validation
- **Bulk Operations**: Manage multiple rules simultaneously

### 🔧 **System Integration**
- **Windows Firewall API**: Direct integration with native Windows Firewall
- **Restore Points**: Create and restore firewall configuration snapshots
- **Administrator Privileges**: Automatic privilege checking and elevation
- **PowerShell Integration**: Advanced rule management capabilities

### 🎨 **Modern UI/UX**
- **MVVM Architecture**: Clean separation of concerns
- **Dynamic Themes**: Dark/Light mode support
- **Material Design**: Professional icons and styling
- **Responsive Interface**: Non-blocking operations with progress indicators

---

## 📱 Application Views

| Main Interface | Rule Management |
| :---: | :---: |
| ![Block Application](assets/images/blockApplication.png) | ![Manage Rules](assets/images/manageRules.png) |

| System Monitoring | Configuration |
| :---: | :---: |
| ![Windows Firewall](assets/images/windowsFirewall.png) | ![Settings](assets/images/settings1.png) |

---

## 🏗️ Architecture Overview

**AppIntBlockerGUI** follows modern software engineering principles:

- **MVVM Pattern**: Model-View-ViewModel architecture
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Async/Await**: Non-blocking UI operations
- **Service Layer**: Clean business logic separation
- **Error Handling**: Comprehensive exception management

### Core Technologies
- **.NET 8.0** with C# 12.0
- **WPF** with MahApps.Metro
- **Windows Firewall API** integration
- **PowerShell** automation
- **System Restore** integration

---

## 📚 Documentation

### 📖 **User Guides**
- [Installation Guide](installation/) - Step-by-step setup instructions
- [User Manual](user-guide/) - Complete usage documentation
- [Troubleshooting](troubleshooting/) - Common issues and solutions

### 🔧 **Developer Resources**
- [API Documentation](api/) - Comprehensive service interfaces
- [Architecture Guide](architecture/) - System design and patterns
- [Contributing](contributing/) - Development guidelines

### 🔒 **Security & Compliance**
- [Security Analysis](security/) - Vulnerability assessment
- [Best Practices](security/#best-practices) - Secure usage guidelines
- [Audit Trail](security/#audit-logging) - Logging and monitoring

---

## 🗺️ Project Roadmap

### Version 1.1.0 (Q2 2025)
- 📊 Advanced Analytics Dashboard
- 🔍 Smart Rule Management
- 📁 Import/Export Functionality
- 🔎 Enhanced Search & Filtering

### Version 1.2.0 (Q4 2025)
- 👥 Multi-User Support
- 🏢 Group Policy Integration
- 📅 Scheduled Operations
- 🚨 Real-Time Notifications

[View Complete Roadmap](roadmap/)

---

## 📊 Code Quality Metrics

| Metric | Status | Details |
|--------|--------|---------|
| **Architecture** | ✅ Excellent | MVVM with DI |
| **Code Coverage** | ⚠️ Pending | Unit tests needed |
| **Security** | ⚠️ Review Required | [Security Analysis](security/) |
| **Documentation** | ✅ Comprehensive | API, guides, roadmap |
| **Performance** | ✅ Optimized | <2s rule loading |

---

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](contributing/) for details.

### Development Setup
1. Fork the repository
2. Create a feature branch
3. Install .NET 8.0 SDK
4. Run `dotnet restore` in `/src`
5. Make your changes
6. Submit a pull request

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

---

## 🙏 Acknowledgments

### AI Development Attribution
This project was developed with the assistance of advanced AI models:
- **Claude 4 Sonnet**: Architecture design, code structure, and best practices
- **Gemini 2.5 Pro**: UI/UX patterns, documentation, and optimization strategies

### Third-Party Libraries
- [MahApps.Metro](https://mahapps.com/) - Modern WPF UI framework
- [CommunityToolkit.Mvvm](https://docs.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) - MVVM helpers
- [ScottPlot](https://scottplot.net/) - Charting and visualization

---

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/bugragungoz/AppIntBlockerGUI/issues)
- **Discussions**: [GitHub Discussions](https://github.com/bugragungoz/AppIntBlockerGUI/discussions)
- **Documentation**: [Project Wiki](https://github.com/bugragungoz/AppIntBlockerGUI/wiki)

---

*Last updated: January 2025*