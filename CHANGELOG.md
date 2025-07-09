# Changelog

All notable changes to AppIntBlockerGUI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2024-07-08
### Added
- **Network Monitor Dashboard**: A new view that displays real-time upload/download rates per running process and a live 60-second bandwidth graph powered by LiveCharts2.
- **Internet Block / Unblock**: One-click toggle to create or remove firewall rules for the selected process directly from the dashboard.
- **Performance-based Counters**: Uses Windows `PerformanceCounter` API to obtain accurate per-process byte counters.
- **Aggregate Statistics**: Header now shows total current throughput and cumulative data transferred since monitoring started.
- **Alert Thresholds**: Logs a warning if combined throughput exceeds a configurable threshold (default: 5 Mbps).

### Changed
- **Upgraded Charting Library**: Replaced `ScottPlot` with `LiveChartsCore.SkiaSharpView.WPF` for better performance and animations.
- Registered `INetworkMonitorService` and `NetworkMonitorViewModel` in the DI container.
- Sidebar now contains a "Network Monitor" navigation option.

### Fixed
- Corrected a data binding issue where `TotalSentMB` and `TotalReceivedMB` were not formatted correctly in the UI.

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
- **Modern MVVM Architecture** with dependency injection
- **Professional UI/UX** with a dark, modern theme
- **Service-based Architecture** with proper interfaces
- **Real-time Status Dashboard** with uptime tracking
- **Features**: Application Blocking, Rule Management, Restore Points, Settings.

---
**For detailed technical changes, see individual commit messages and pull requests.** 