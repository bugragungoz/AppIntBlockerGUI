using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppIntBlockerGUI.Services;

namespace AppIntBlockerGUI.ViewModels
{
    public partial class RestorePointsViewModel : ObservableObject
    {
        private readonly SystemRestoreService _restoreService;
        private readonly IDialogService _dialogService;
        private readonly ILoggingService _loggingService;

        [ObservableProperty]
        private ObservableCollection<SystemRestoreService.RestorePoint> restorePoints = new();

        [ObservableProperty]
        private SystemRestoreService.RestorePoint? selectedRestorePoint;

        [ObservableProperty]
        private string systemRestoreStatus = "Checking...";

        [ObservableProperty]
        private int totalRestorePoints = 0;

        [ObservableProperty]
        private int appIntBlockerPoints = 0;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private DateTime lastRefreshTime = DateTime.Now;

        public RestorePointsViewModel()
        {
            _restoreService = new SystemRestoreService();
            _dialogService = new DialogService();
            _loggingService = new LoggingService();

            // Load restore points on initialization
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            StatusMessage = "Initializing restore points...";
            await CheckSystemRestoreStatus();
            await LoadRestorePointsAsync();
        }

        private async Task CheckSystemRestoreStatus()
        {
            try
            {
                var isEnabled = await _restoreService.IsSystemRestoreEnabledAsync();
                SystemRestoreStatus = isEnabled ? "System Restore is enabled" : "System Restore is disabled";
                
                if (!isEnabled)
                {
                    StatusMessage = "Warning: System Restore is disabled on this system";
                }
            }
            catch (Exception ex)
            {
                SystemRestoreStatus = "Unable to check status";
                _loggingService.LogError("Error checking system restore status", ex);
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadRestorePointsAsync();
        }

        private async Task LoadRestorePointsAsync()
        {
            try
            {
                StatusMessage = "Loading restore points...";
                
                var points = await _restoreService.GetRestorePointsAsync();
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RestorePoints.Clear();
                    foreach (var point in points.OrderByDescending(p => p.CreationTime))
                    {
                        RestorePoints.Add(point);
                    }
                    
                    TotalRestorePoints = RestorePoints.Count;
                    AppIntBlockerPoints = RestorePoints.Count(p => 
                        p.Description.Contains("AppIntBlocker", StringComparison.OrdinalIgnoreCase));
                    
                    LastRefreshTime = DateTime.Now;
                    StatusMessage = $"Loaded {TotalRestorePoints} restore points";
                });
                
                _loggingService.LogInfo($"Loaded {TotalRestorePoints} restore points, {AppIntBlockerPoints} created by AppIntBlocker");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error loading restore points", ex);
                StatusMessage = "Error loading restore points";
                _dialogService.ShowMessage("Failed to load restore points. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private async Task CreateRestorePoint()
        {
            try
            {
                var description = $"AppIntBlocker - Manual restore point {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                
                var confirmMessage = $"Create a new system restore point with description:\n\n\"{description}\"\n\n" +
                                   "This operation may take a few minutes. Continue?";
                
                if (!_dialogService.ShowConfirmation(confirmMessage, "Create Restore Point"))
                {
                    return;
                }

                StatusMessage = "Creating restore point...";
                _loggingService.LogInfo($"Creating restore point: {description}");

                var success = await Task.Run(async () => 
                {
                    return await _restoreService.CreateRestorePointAsync(description);
                });

                if (success)
                {
                    StatusMessage = "Restore point created successfully";
                    _dialogService.ShowMessage("Restore point created successfully!", "Success");
                    await LoadRestorePointsAsync();
                }
                else
                {
                    StatusMessage = "Failed to create restore point";
                    _dialogService.ShowMessage("Failed to create restore point. Make sure System Restore is enabled and you have administrator privileges.", "Error");
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error creating restore point", ex);
                StatusMessage = "Error creating restore point";
                _dialogService.ShowMessage("An error occurred while creating the restore point. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private void OpenSystemRestoreTool()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = "SystemPropertiesProtection.exe"
                };
                Process.Start(processInfo);
                _loggingService.LogInfo("Opened Windows System Restore tool.");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to open System Restore tool.", ex);
                _dialogService.ShowError("Could not open the Windows System Restore tool. You may need to open it manually from the Control Panel (System > System Protection).");
            }
        }

        [RelayCommand]
        private async Task RestoreSystem(SystemRestoreService.RestorePoint restorePoint)
        {
            if (restorePoint == null) return;

            try
            {
                var confirmMessage = $"CRITICAL WARNING!\n\n" +
                                   $"You are about to restore your system to:\n" +
                                   $"• Restore Point: {restorePoint.Description}\n" +
                                   $"• Creation Time: {restorePoint.CreationTime:yyyy-MM-dd HH:mm:ss}\n" +
                                   $"• Sequence Number: {restorePoint.SequenceNumber}\n\n" +
                                   $"This will:\n" +
                                   $"• Revert system files and registry to the restore point date\n" +
                                   $"• Restart your computer automatically\n" +
                                   $"• Potentially undo recent software installations\n\n" +
                                   $"Save all your work before continuing!\n\n" +
                                   $"Are you absolutely sure you want to proceed?";

                if (!_dialogService.ShowConfirmation(confirmMessage, "SYSTEM RESTORE WARNING"))
                {
                    return;
                }

                StatusMessage = "Initiating system restore...";
                _loggingService.LogInfo($"Initiating system restore to point {restorePoint.SequenceNumber}: {restorePoint.Description}");

                // Show final warning
                var finalConfirm = _dialogService.ShowConfirmation(
                    "FINAL CONFIRMATION\n\nThis is your last chance to cancel.\n\nThe system will restart immediately after clicking OK.\n\nProceed with system restore?",
                    "FINAL WARNING");

                if (!finalConfirm)
                {
                    StatusMessage = "System restore cancelled";
                    return;
                }

                var success = await Task.Run(async () => 
                {
                    return await _restoreService.RestoreSystemAsync(restorePoint.SequenceNumber);
                });

                if (!success)
                {
                    StatusMessage = "Failed to initiate system restore";
                    _dialogService.ShowMessage("Failed to initiate system restore. Check the log for details.", "Error");
                }
                // If successful, the system will restart and we won't reach this point
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error restoring system to point {restorePoint.SequenceNumber}", ex);
                StatusMessage = "Error during system restore";
                _dialogService.ShowMessage("An error occurred during system restore. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private void DeleteRestorePoint(SystemRestoreService.RestorePoint restorePoint)
        {
            if (restorePoint == null) return;

            try
            {
                var confirmMessage = $"Delete restore point?\n\n" +
                                   $"• Description: {restorePoint.Description}\n" +
                                   $"• Creation Time: {restorePoint.CreationTime:yyyy-MM-dd HH:mm:ss}\n" +
                                   $"• Sequence Number: {restorePoint.SequenceNumber}\n\n" +
                                   $"This action cannot be undone. Continue?";

                if (!_dialogService.ShowConfirmation(confirmMessage, "Delete Restore Point"))
                {
                    return;
                }

                StatusMessage = "Deleting restore point...";
                _loggingService.LogInfo($"Deleting restore point {restorePoint.SequenceNumber}: {restorePoint.Description}");

                // Note: Windows doesn't provide a direct API to delete specific restore points
                // This is a limitation of the Windows System Restore API
                _dialogService.ShowMessage(
                    "Windows does not provide an API to delete individual restore points.\n\n" +
                    "To manage restore points, use:\n" +
                    "• Control Panel > System > System Protection\n" +
                    "• Disk Cleanup utility\n" +
                    "• PowerShell: vssadmin delete shadows commands",
                    "Feature Limitation");

                StatusMessage = "Delete operation not available through API";
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Error attempting to delete restore point {restorePoint.SequenceNumber}", ex);
                StatusMessage = "Error during delete operation";
                _dialogService.ShowMessage("An error occurred during the delete operation. Check the log for details.", "Error");
            }
        }
    }
} 