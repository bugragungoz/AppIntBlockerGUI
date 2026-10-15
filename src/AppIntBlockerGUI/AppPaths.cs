using System.IO;

namespace AppIntBlockerGUI;

/// <summary>Where the app keeps its files, and the folders it refuses to block as a whole.</summary>
public static class AppPaths
{
    public static string BackupFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AppIntBlocker", "Backups");

    public static string LogFile { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppIntBlocker", "AppIntBlocker.log");

    public static string WindowsFolder { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public static IReadOnlyList<string> OtherSystemFolders { get; } =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ];

    /// <summary>A new, timestamped backup file name in <see cref="BackupFolder"/>, which it creates.</summary>
    public static string NewBackupPath()
    {
        Directory.CreateDirectory(BackupFolder);
        return Path.Combine(BackupFolder, $"FirewallBackup-{DateTime.Now:yyyyMMdd-HHmmss}.wfw");
    }
}
