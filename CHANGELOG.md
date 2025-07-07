# Changelog

All notable changes to AppIntBlockerGUI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2025-07-04
### Added
- **Cancellation Support**: Long-running operations like refreshing firewall rules can now be cancelled by the user.
- **Unit Test Suite**: Created a robust test suite for `FirewallService` using MSTest and Moq to ensure reliability.
- **CI/CD Pipeline**: Implemented a GitHub Actions workflow to automatically build and test the project on every push and pull request.

### Changed
- **Architectural Refactoring**: Refactored `FirewallService` to depend on an `IPowerShellWrapper` interface, decoupling it from `System.Management.Automation.PowerShell` and dramatically improving testability.
- **UI Responsiveness**: The loading indicator in the "Manage Rules" view now shows dynamic status text (e.g., "Refreshing rules...").

### Fixed
- Improved UI feedback during long-running background tasks.

## [1.0.0] - 2025-01-07

### Initial Release - Complete Architecture Implementation

### Added
- **Modern MVVM Architecture** with dependency injection
- **Professional UI/UX** with Nuln Oil Gloss theme
- **Dynamic Theme Switching** (Dark/Light modes)
- **Navigation Service** for clean view management
- **Service-based Architecture** with proper interfaces
- **Real-time Status Dashboard** with uptime tracking
- **Comprehensive Error Handling** and logging
- **Material Design Icons** throughout the application
- **MahApps.Metro Integration** for modern Windows styling
- **Async/Await Patterns** for non-blocking operations

### Features
- **Application Blocking Interface** with intuitive UX
- **Rule Management System** with advanced filtering
- **Restore Points Feature** with backup visualization
- **Windows Firewall Integration** with direct API access
- **Settings Management** with persistent configuration
- **Performance Optimization** for efficient rule processing

### Technical Implementation
- Built on **.NET 8.0** framework
- Implemented **Microsoft.Extensions.DependencyInjection**
- Added **CommunityToolkit.Mvvm** for modern MVVM patterns
- Integrated **MahApps.Metro** for consistent Windows styling
- Added comprehensive **Value Converters** for data binding
- Implemented **ObservableObject** pattern throughout ViewModels

### Architecture Highlights
- Resolved startup deadlock issues in MainWindow constructor
- Fixed DataContext binding problems
- Corrected theme resource loading and switching
- Improved memory management and disposal patterns
- Enhanced exception handling across all services

## Development Notes

### AI-Assisted Development
This version was developed with the assistance of:
- **Claude 4 Sonnet**: Architecture design and code structure
- **Gemini 2.5 Pro**: UI/UX patterns and optimization strategies

### Migration Information
- **Framework**: .NET 8.0 with modern C# features
- **Dependencies**: MahApps.Metro, CommunityToolkit.Mvvm
- **Configuration**: Theme settings managed through ThemeService
- **Permissions**: Administrator access required for firewall operations

### Backup Recommendations
- Always backup existing firewall configurations before making changes
- Test in a non-production environment first
- Review administrator permission requirements

## Upcoming Features

### v1.2.0 (Planned)
- [ ] Rule import/export functionality
- [ ] Advanced rule filtering and search
- [ ] Batch operations for multiple rules
- [ ] Improved error reporting and diagnostics
- [ ] PowerShell module integration
- [ ] Group Policy support
- [x] Network monitoring dashboard
- [ ] Scheduled rule management

### v2.0.0 (Future)
- [ ] Web-based management interface
- [ ] Multi-machine management
- [ ] Advanced reporting and analytics

## [2025-SECURITY-PATCH] - 2025-xx-xx
### Added
- AI-assisted security patches: command injection, PowerShell injection, path traversal, input validation, error message sanitization
- Settings file encryption (DPAPI)
- Build and compatibility issues resolved
- Refactoring completed while preserving GUI and core functionality

## [1.2.0] - 2025-07-07
### Added
- **Network Monitor Dashboard**: A new view that displays real-time upload/download rates per running process and a live 60-second bandwidth graph powered by LiveCharts2.
- **Internet Block / Unblock**: One-click toggle to create or remove firewall rules for the selected process directly from the dashboard.
- **Performance-based Counters**: Uses Windows `PerformanceCounter` API to obtain accurate per-process byte counters.
- **Aggregate Statistics**: Header now shows total current throughput and cumulative data transferred since monitoring started.
- **Alert Thresholds**: Logs a warning if combined throughput exceeds 5 Mbps (configurable).

### Changed
- Registered `INetworkMonitorService` and `NetworkMonitorViewModel` in the DI container.
- Sidebar now contains a "Network Monitor" navigation option.
- Added LiveCharts2 dependency (`LiveChartsCore.SkiaSharpView.WPF`).

### Removed
- The "Network monitoring dashboard" item from the upcoming features list – it is now implemented.

---

**For detailed technical changes, see individual commit messages and pull requests.** 