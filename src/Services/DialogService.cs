// <copyright file="DialogService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System.Windows;
    using AppIntBlockerGUI.Views;
    using Microsoft.Win32;

    public class DialogService : IDialogService
    {
        public string? OpenFolderDialog()
        {
            // FIXED: Use WPF dialog instead of Windows Forms
            var dialog = new OpenFileDialog
            {
                Title = "Select Folder",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Folder Selection",
                Filter = "Folders|*.",
                ValidateNames = false
            };

            if (dialog.ShowDialog() == true)
            {
                return System.IO.Path.GetDirectoryName(dialog.FileName);
            }

            return null;
        }

        public string? OpenFileDialog(string title = "Select File", string filter = "All Files|*.*")
        {
            // FIXED: Use WPF dialog instead of Windows Forms
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        public string? SaveFileDialog(string title = "Save File", string filter = "All Files|*.*", string defaultExt = "", string fileName = "")
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                DefaultExt = defaultExt,
                FileName = fileName
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return null;
        }

        public void ShowMessage(string message, string title = "Information")
        {
            this.ShowInfo(message, title);
        }

        public void ShowInfo(string message, string title = "Information")
        {
            var dialog = new CustomDialogWindow(title, message, "ℹ️");
            dialog.ShowDialog();
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            var dialog = new CustomDialogWindow(title, message, "⚠️");
            dialog.ShowDialog();
        }

        public void ShowError(string message, string title = "Error")
        {
            var dialog = new CustomDialogWindow(title, message, "❌");
            dialog.ShowDialog();
        }

        public bool ShowConfirmation(string message, string title = "Confirm")
        {
            var dialog = new CustomDialogWindow(title, message, "❓", showCancelButton: true);
            return dialog.ShowDialog() == true;
        }
    }
}
