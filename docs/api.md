---
layout: default
title: API Documentation
permalink: /api/
---

# API Documentation

This document provides detailed information about the services, interfaces, and architecture of AppIntBlockerGUI v1.0.

## Architecture Overview

AppIntBlocker follows a clean MVVM architecture with dependency injection, providing a clear separation of concerns and testable code structure.

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│     Views       │    │   ViewModels    │    │    Services     │
│                 │    │                 │    │                 │
│ - XAML Files    │◄──►│ - Business Logic│◄──►│ - Data Access   │
│ - User Controls │    │ - Commands      │    │ - External APIs │
│ - Windows       │    │ - Properties    │    │ - Utilities     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                        │                        │
         │                        │                        │
         ▼                        ▼                        ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│     Models      │    │   Converters    │    │   Resources     │
│                 │    │                 │    │                 │
│ - Data Models   │    │ - Value         │    │ - Themes        │
│ - Entities      │    │   Converters    │    │ - Styles        │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Core Services

### IFirewallService
**Location**: `Services/IFirewallService.cs`

The primary service for Windows Firewall integration.

```csharp
public interface IFirewallService
{
    Task<List<FirewallRuleModel>> GetExistingRulesAsync(ILoggingService logger);
    Task<bool> CreateRuleAsync(string ruleName, string applicationPath, 
                              NET_FW_RULE_DIRECTION direction, ILoggingService logger);
    Task<bool> DeleteRuleAsync(string ruleName, ILoggingService logger);
    Task<bool> ToggleRuleAsync(string ruleName, bool enabled, ILoggingService logger);
    Task<bool> RuleExistsAsync(string ruleName, ILoggingService logger);
}
```

**Key Methods:**
- `GetExistingRulesAsync()`: Retrieves all Windows Firewall rules
- `CreateRuleAsync()`: Creates a new firewall rule
- `DeleteRuleAsync()`: Removes an existing rule
- `ToggleRuleAsync()`: Enables/disables a rule
- `RuleExistsAsync()`: Checks if a rule exists

### INavigationService
**Location**: `Services/INavigationService.cs`

Manages view navigation in the MVVM pattern.

```csharp
public interface INavigationService
{
    ObservableObject CurrentViewModel { get; }
    event Action<ObservableObject>? NavigationChanged;
    void NavigateTo(Type viewModelType);
}
```

**Key Features:**
- Type-safe navigation to ViewModels
- Event-driven navigation updates
- Automatic view resolution through DataTemplates

### IDialogService
**Location**: `Services/IDialogService.cs`

Manages modal dialogs and user interactions.

```csharp
public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task ShowInfoAsync(string title, string message);
    Task<string?> ShowInputAsync(string title, string prompt, string defaultValue = "");
}
```

## Data Models

### FirewallRuleModel
**Location**: `Models/FirewallRuleModel.cs`

Represents a Windows Firewall rule.

```csharp
public class FirewallRuleModel
{
    public string Name { get; set; }
    public string ApplicationName { get; set; }
    public bool Enabled { get; set; }
    public NET_FW_RULE_DIRECTION Direction { get; set; }
    public string Protocol { get; set; }
    public string LocalPorts { get; set; }
    public string RemotePorts { get; set; }
    public string Description { get; set; }
}
```

### AppSettings
**Location**: `Models/AppSettings.cs`

Application configuration and user preferences.

```csharp
public class AppSettings
{
    public Theme CurrentTheme { get; set; }
    public bool StartMinimized { get; set; }
    public string LogLevel { get; set; }
    public bool AutoRefreshRules { get; set; }
    public int RefreshInterval { get; set; }
}
```

## ViewModels

### MainWindowViewModel
**Responsibilities:**
- Application-wide state management
- Navigation coordination
- System status monitoring
- Theme management

### BlockApplicationViewModel
**Responsibilities:**
- Application selection and blocking
- File browser integration
- Rule creation workflow

### ManageRulesViewModel
**Responsibilities:**
- Rule listing and filtering
- Rule modification and deletion
- Bulk operations

## Event Flow

### Application Startup
1. `App.xaml.cs` configures dependency injection
2. Services are registered in DI container
3. `MainWindow` is created with injected `MainWindowViewModel`
4. `NavigationService` initializes with default view
5. Theme service applies saved theme

### Navigation Flow
1. User interaction triggers command in ViewModel
2. Command calls `NavigationService.NavigateTo(typeof(TargetViewModel))`
3. NavigationService resolves ViewModel from DI container
4. NavigationChanged event fires
5. MainWindow updates CurrentViewModel property
6. WPF DataTemplate system resolves and displays appropriate View

### Rule Management Flow
1. User initiates rule operation
2. ViewModel validates input
3. `IFirewallService` executes Windows Firewall API calls
4. Operation result logged via `ILoggingService`
5. UI updates reflect changes
6. Status dashboard refreshes

## Extension Points

### Adding New Views
1. Create new ViewModel inheriting from `ObservableObject`
2. Create corresponding View (UserControl)
3. Add DataTemplate to `MainWindow.xaml` resources
4. Register ViewModel in DI container
5. Add navigation command to appropriate parent ViewModel

### Custom Services
1. Define interface in `Services/` folder
2. Implement concrete class
3. Register in `App.xaml.cs` DI configuration
4. Inject into ViewModels as needed

## Performance Considerations

### Rule Loading Optimization
- Async loading with progress indicators
- Lazy loading for large rule sets
- Background threading for heavy operations

### Memory Management
- Proper disposal of ViewModels implementing `IDisposable`
- Event subscription cleanup in Dispose methods
- Weak event patterns where appropriate

---

**For implementation examples and detailed code samples, see the source code in the [GitHub repository](https://github.com/bugragungoz/AppIntBlockerGUI).**