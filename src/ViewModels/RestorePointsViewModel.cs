// <copyright file="RestorePointsViewModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using AppIntBlockerGUI.Services;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;

    public partial class RestorePointsViewModel : ObservableObject
    {
        private readonly SystemRestoreService restoreService;
        private readonly IDialogService dialogService;
        private readonly ILoggingService loggingService;

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
            this.restoreService = new SystemRestoreService();
            this.dialogService = new DialogService();
            this.loggingService = new LoggingService();

            // Load restore points on initialization
            _ = this.InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            this.StatusMessage = "Initializing restore points...";
            await this.CheckSystemRestoreStatus();
            await this.LoadRestorePointsAsync();
        }

        private async Task CheckSystemRestoreStatus()
        {
            try
            {
                var isEnabled = await this.restoreService.IsSystemRestoreEnabledAsync();
                this.SystemRestoreStatus = isEnabled ? "System Restore is enabled" : "System Restore is disabled";

                if (!isEnabled)
                {
                    this.StatusMessage = "Warning: System Restore is disabled on this system";
                }
            }
            catch (Exception ex)
            {
                this.SystemRestoreStatus = "Unable to check status";
                this.loggingService.LogError("Error checking system restore status", ex);
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await this.LoadRestorePointsAsync();
        }

        private async Task LoadRestorePointsAsync()
        {
            try
            {
                this.StatusMessage = "Loading restore points...";

                var points = await this.restoreService.GetRestorePointsAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    this.RestorePoints.Clear();
                    foreach (var point in points.OrderByDescending(p => p.CreationTime))
                    {
                        this.RestorePoints.Add(point);
                    }

                    this.TotalRestorePoints = this.RestorePoints.Count;
                    this.AppIntBlockerPoints = this.RestorePoints.Count(p =>
                        p.Description.Contains("AppIntBlocker", StringComparison.OrdinalIgnoreCase));

                    this.LastRefreshTime = DateTime.Now;
                    this.StatusMessage = $"Loaded {this.TotalRestorePoints} restore points";
                });

                this.loggingService.LogInfo($"Loaded {this.TotalRestorePoints} restore points, {this.AppIntBlockerPoints} created by AppIntBlocker");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error loading restore points", ex);
                this.StatusMessage = "Error loading restore points";
                this.dialogService.ShowMessage("Failed to load restore points. Check the log for details.", "Error");
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

                if (!this.dialogService.ShowConfirmation(confirmMessage, "Create Restore Point"))
                {
                    return;
                }

                this.StatusMessage = "Creating restore point...";
                this.loggingService.LogInfo($"Creating restore point: {description}");

                var success = await Task.Run(async () =>
                {
                    return await this.restoreService.CreateRestorePointAsync(description);
                });

                if (success)
                {
                    this.StatusMessage = "Restore point created successfully";
                    this.dialogService.ShowMessage("Restore point created successfully!", "Success");
                    await this.LoadRestorePointsAsync();
                }
                else
                {
                    this.StatusMessage = "Failed to create restore point";
                    this.dialogService.ShowMessage("Failed to create restore point. Make sure System Restore is enabled and you have administrator privileges.", "Error");
                }
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Error creating restore point", ex);
                this.StatusMessage = "Error creating restore point";
                this.dialogService.ShowMessage("An error occurred while creating the restore point. Check the log for details.", "Error");
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
                this.loggingService.LogInfo("Opened Windows System Restore tool.");
            }
            catch (Exception ex)
            {
                this.loggingService.LogError("Failed to open System Restore tool.", ex);
                this.dialogService.ShowError("Could not open the Windows System Restore tool. You may need to open it manually from the Control Panel (System > System Protection).");
            }
        }

        [RelayCommand]
        private async Task RestoreSystem(SystemRestoreService.RestorePoint restorePoint)
        {
            if (restorePoint == null)
            {
                return;
            }

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

                if (!this.dialogService.ShowConfirmation(confirmMessage, "SYSTEM RESTORE WARNING"))
                {
                    return;
                }

                this.StatusMessage = "Initiating system restore...";
                this.loggingService.LogInfo($"Initiating system restore to point {restorePoint.SequenceNumber}: {restorePoint.Description}");

                // Show final warning
                var finalConfirm = this.dialogService.ShowConfirmation(
                    "FINAL CONFIRMATION\n\nThis is your last chance to cancel.\n\nThe system will restart immediately after clicking OK.\n\nProceed with system restore?",
                    "FINAL WARNING");

                if (!finalConfirm)
                {
                    this.StatusMessage = "System restore cancelled";
                    return;
                }

                var success = await Task.Run(async () =>
                {
                    return await this.restoreService.RestoreSystemAsync(restorePoint.SequenceNumber);
                });

                if (!success)
                {
                    this.StatusMessage = "Failed to initiate system restore";
                    this.dialogService.ShowMessage("Failed to initiate system restore. Check the log for details.", "Error");
                }

                // If successful, the system will restart and we won't reach this point
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Error restoring system to point {restorePoint.SequenceNumber}", ex);
                this.StatusMessage = "Error during system restore";
                this.dialogService.ShowMessage("An error occurred during system restore. Check the log for details.", "Error");
            }
        }

        [RelayCommand]
        private void DeleteRestorePoint(SystemRestoreService.RestorePoint restorePoint)
        {
            if (restorePoint == null)
            {
                return;
            }

            try
            {
                var confirmMessage = $"Delete restore point?\n\n" +
                                   $"• Description: {restorePoint.Description}\n" +
                                   $"• Creation Time: {restorePoint.CreationTime:yyyy-MM-dd HH:mm:ss}\n" +
                                   $"• Sequence Number: {restorePoint.SequenceNumber}\n\n" +
                                   $"This action cannot be undone. Continue?";

                if (!this.dialogService.ShowConfirmation(confirmMessage, "Delete Restore Point"))
                {
                    return;
                }

                this.StatusMessage = "Deleting restore point...";
                this.loggingService.LogInfo($"Deleting restore point {restorePoint.SequenceNumber}: {restorePoint.Description}");

                // Note: Windows doesn't provide a direct API to delete specific restore points
                // This is a limitation of the Windows System Restore API
                this.dialogService.ShowMessage(
                    "Windows does not provide an API to delete individual restore points.\n\n" +
                    "To manage restore points, use:\n" +
                    "• Control Panel > System > System Protection\n" +
                    "• Disk Cleanup utility\n" +
                    "• PowerShell: vssadmin delete shadows commands",
                    "Feature Limitation");

                this.StatusMessage = "Delete operation not available through API";
            }
            catch (Exception ex)
            {
                this.loggingService.LogError($"Error attempting to delete restore point {restorePoint.SequenceNumber}", ex);
                this.StatusMessage = "Error during delete operation";
                this.dialogService.ShowMessage("An error occurred during the delete operation. Check the log for details.", "Error");
            }
        }
    }
}
