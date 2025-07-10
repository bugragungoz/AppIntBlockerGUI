# 📋 Changelog

All notable changes to **AppIntBlockerGUI** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### 🔄 **Planned**
- Enhanced security analysis dashboard
- Automated vulnerability scanning
- Extended CI/CD pipeline with security checks
- Performance optimization metrics
- Multi-language support

### 🔒 **Security Improvements**
- Comprehensive input validation audit
- Enhanced logging sanitization
- Privilege escalation protection
- Rate limiting for network operations

---

## [1.2.0] - 2024-07-08

### 🆕 **Added**
- **📊 Network Monitor Dashboard**: Real-time view displaying upload/download rates per running process
- **📈 Live Bandwidth Graphs**: Interactive 60-second bandwidth visualization powered by LiveCharts2
- **⚡ Internet Block/Unblock**: One-click toggle to create or remove firewall rules directly from the dashboard
- **📊 Performance Counters**: Windows `PerformanceCounter` API integration for accurate per-process byte tracking
- **📈 Aggregate Statistics**: Header displays total current throughput and cumulative data transferred
- **🚨 Alert Thresholds**: Configurable throughput warnings (default: 5 Mbps)
- **🔍 Process Analysis**: Enhanced process categorization and security identification

### 🔄 **Changed**
- **📊 Charting Library Upgrade**: Migrated from `ScottPlot` to `LiveChartsCore.SkiaSharpView.WPF` for superior performance and animations
- **🏗️ Service Registration**: Added `INetworkMonitorService` and `NetworkMonitorViewModel` to DI container
- **🎨 UI Navigation**: Enhanced sidebar with "Network Monitor" navigation option
- **⚡ Performance**: Optimized real-time data processing and chart rendering

### 🐛 **Fixed**
- **📊 Data Binding**: Resolved formatting issues with `TotalSentMB` and `TotalReceivedMB` display
- **🔄 Memory Management**: Improved garbage collection for real-time monitoring
- **🎯 Chart Performance**: Reduced CPU usage during continuous data updates

### 🔒 **Security**
- **✅ Process Validation**: Enhanced process path validation and sanitization
- **🛡️ Privilege Checks**: Improved administrator privilege verification
- **🔐 Data Protection**: Secured network monitoring data collection

---

## [1.1.0] - 2025-07-04

### 🆕 **Added**
- **❌ Cancellation Support**: User-controllable cancellation for long-running operations (firewall rule refresh, etc.)
- **🧪 Comprehensive Unit Test Suite**: Complete testing framework for `FirewallService` using MSTest and Moq
- **⚙️ CI/CD Pipeline**: GitHub Actions workflow for automated building and testing on every push/PR
- **📊 Dynamic Status Indicators**: Real-time status text updates (e.g., "Refreshing rules...")
- **🔄 Progress Tracking**: Enhanced user feedback during background operations

### 🔄 **Changed**
- **🏗️ Architectural Refactoring**: `FirewallService` now depends on `IPowerShellWrapper` interface for improved testability
- **🎯 Dependency Decoupling**: Removed direct dependency on `System.Management.Automation.PowerShell`
- **🎨 UI Responsiveness**: Improved loading indicators in "Manage Rules" view
- **📈 Error Handling**: Enhanced exception management and user feedback

### 🐛 **Fixed**
- **⏱️ Long-running Tasks**: Improved UI feedback during extended background operations
- **🔄 Cancellation Handling**: Proper cleanup and state management for cancelled operations
- **📊 Status Updates**: More accurate and timely status message updates

### 🔒 **Security**
- **✅ Input Validation**: Enhanced parameter validation in service methods
- **🛡️ Error Message Sanitization**: Improved error handling to prevent information disclosure
- **🔐 Interface Abstraction**: Better security through abstracted dependencies

---

## [1.0.0] - 2025-01-07

### 🎉 **Initial Release - Complete Architecture Implementation**

### 🆕 **Added**
- **🏗️ Modern MVVM Architecture**: Complete implementation with dependency injection
- **🎨 Professional UI/UX**: Dark, modern theme with polished user experience
- **🔧 Service-based Architecture**: Clean interfaces and proper separation of concerns
- **📊 Real-time Status Dashboard**: Live uptime tracking and system monitoring
- **🚫 Application Blocking**: Core functionality to block applications from network access
- **⚙️ Rule Management**: Comprehensive firewall rule creation, editing, and deletion
- **💾 Restore Points**: System state backup and restoration capabilities
- **🎛️ Settings Management**: Configurable application preferences and options

### 🔒 **Security Features**
- **✅ Administrator Privilege Management**: Automatic elevation and verification
- **🛡️ Input Validation**: Comprehensive user input sanitization
- **🔐 Path Security**: Protection against path traversal attacks
- **🚨 System Protection**: Critical system directory access prevention

### 🛠️ **Technical Implementation**
- **⚡ Async/Await Patterns**: Full asynchronous implementation throughout
- **📊 Dependency Injection**: Microsoft.Extensions.DependencyInjection integration
- **📝 Comprehensive Logging**: Serilog-based structured logging system
- **🎯 Error Handling**: Robust exception management and recovery
- **🔄 Performance Optimization**: Efficient resource usage and memory management

---

## 🛡️ **Security Changelog**

### **Vulnerability Fixes Applied**

#### **🔴 Critical Fixes**
- **Command Injection Protection**: Implemented parameterized command execution
- **Path Traversal Prevention**: Added comprehensive path validation and normalization
- **Input Sanitization**: Enhanced validation for all user inputs
- **Privilege Escalation Protection**: Secured administrator elevation handling

#### **🟠 High Priority Fixes**
- **Error Message Sanitization**: Prevented sensitive information disclosure
- **Memory Leak Prevention**: Proper resource disposal and cleanup
- **Thread Safety**: Secured multi-threaded operations
- **Dependency Injection Security**: Secured service instantiation and lifecycle

#### **🟡 Medium Priority Fixes**
- **Settings Encryption**: DPAPI encryption for user settings
- **Log Security**: Sanitized logging to prevent information leaks
- **UI Security**: Prevented UI-based attack vectors
- **File System Security**: Enhanced file operation security

---

## 🤝 **Contributing**

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on:
- 🔧 Development setup
- 📝 Code style guidelines
- 🧪 Testing requirements
- 📋 Pull request process

## 📞 **Support**

- 🐛 **Bug Reports**: [GitHub Issues](https://github.com/bugragungoz/AppIntBlockerGUI/issues)
- 💡 **Feature Requests**: [GitHub Issues](https://github.com/bugragungoz/AppIntBlockerGUI/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/bugragungoz/AppIntBlockerGUI/discussions)
- 📧 **Security Issues**: Please see our [Security Policy](SECURITY.md)

---

**📝 For detailed technical changes, see individual commit messages and pull requests.** 