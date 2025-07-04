// <copyright file="IDialogService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Services
{
    using System.Threading.Tasks;

    public interface IDialogService
    {
        string? OpenFolderDialog();

        string? OpenFileDialog(string title = "Select File", string filter = "All Files|*.*");

        string? SaveFileDialog(string title = "Save File", string filter = "All Files|*.*", string defaultExt = "", string fileName = "");

        void ShowMessage(string message, string title = "Information");

        void ShowInfo(string message, string title = "Information");

        void ShowWarning(string message, string title = "Warning");

        void ShowError(string message, string title = "Error");

        bool ShowConfirmation(string message, string title = "Confirm");
    }
}
