namespace AppIntBlocker.Core;

/// <summary>An executable inside the application folder.</summary>
public sealed record ExeFile(string FullPath, string RelativePath);

public static class ExeScanner
{
    /// <summary>
    /// Every .exe in the folder and its subfolders, sorted by path. Only .exe files get rules:
    /// Windows Firewall matches a rule to the program of the process that opens a connection,
    /// and DLLs run inside those programs.
    /// </summary>
    public static IReadOnlyList<ExeFile> Find(string folder, CancellationToken cancellationToken = default)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            // Hidden and system files are included; links to other folders are not followed.
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        var files = new List<ExeFile>();
        foreach (var path in Directory.EnumerateFiles(folder, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(new ExeFile(path, Path.GetRelativePath(folder, path)));
            }
        }

        files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.RelativePath, b.RelativePath));
        return files;
    }
}
