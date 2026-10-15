using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppIntBlockerGUI.ViewModels;

/// <summary>What the app did, shown on the Block page and appended to a log file.</summary>
public sealed partial class ActivityLog : ObservableObject
{
    private readonly string file;

    [ObservableProperty]
    private string text = string.Empty;

    public ActivityLog(string file)
    {
        this.file = file;
    }

    public void Add(string message)
    {
        var now = DateTime.Now;
        this.Text += $"{now:HH:mm:ss}  {message}{Environment.NewLine}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.file)!);
            File.AppendAllText(this.file, $"{now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The on-screen log still has the line; a log file that cannot be written is not worth stopping for.
        }
    }
}
