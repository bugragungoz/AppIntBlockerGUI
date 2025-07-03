using System.Windows;
using AppIntBlockerGUI.Views;
using WinForms = System.Windows.Forms;

namespace AppIntBlockerGUI.Services
{
    public class DialogService : IDialogService
    {
        public string? OpenFolderDialog()
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            var result = dialog.ShowDialog();
            return result == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
        }

        public string? OpenFileDialog(string title = "Select File", string filter = "All Files|*.*")
        {
            using var dialog = new WinForms.OpenFileDialog
            {
                Title = title,
                Filter = filter,
                Multiselect = false
            };
            var result = dialog.ShowDialog();
            return result == WinForms.DialogResult.OK ? dialog.FileName : null;
        }

        public void ShowMessage(string message, string title = "Information")
        {
            ShowInfo(message, title);
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