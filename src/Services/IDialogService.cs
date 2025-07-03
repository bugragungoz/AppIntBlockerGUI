using System.Threading.Tasks;

namespace AppIntBlockerGUI.Services
{
    public interface IDialogService
    {
        string? OpenFolderDialog();
        string? OpenFileDialog(string title = "Select File", string filter = "All Files|*.*");
        void ShowMessage(string message, string title = "Information");
        void ShowInfo(string message, string title = "Information");
        void ShowWarning(string message, string title = "Warning");
        void ShowError(string message, string title = "Error");
        bool ShowConfirmation(string message, string title = "Confirm");
    }
} 