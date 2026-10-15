using System.Text.RegularExpressions;

namespace AppIntBlocker.Core;

/// <summary>Checks on what the user types. Each check returns an error message, or null when the input is fine.</summary>
public static class InputChecks
{
    // Rule names separate the application from the file with " - ", so a name holding a space
    // followed by a hyphen would be read back as a shorter name (AppIntBlocker.ps1 refuses it too).
    private static readonly Regex SpaceHyphen = new(@"\s-", RegexOptions.CultureInvariant);

    /// <summary>Drops surrounding spaces and the quotes Explorer's "Copy as path" adds.</summary>
    public static string NormalizePath(string text) => text.Trim().Trim('"').Trim();

    public static string? CheckApplicationName(string name)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return "Enter a name for the application.";
        }

        if (SpaceHyphen.IsMatch(name))
        {
            return "The name cannot contain a space followed by a hyphen.";
        }

        return null;
    }

    /// <summary>A valid application name made from the folder's name.</summary>
    public static string SuggestName(string folder)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        return Regex.Replace(name, @"\s+-\s*", "-").Trim();
    }

    /// <summary>
    /// Refuses a missing folder, a whole drive, the Windows folder with everything inside it, and
    /// the other system folders themselves (Program Files, the user profile). Blocking every .exe
    /// in one of them would cut off Windows or every installed program at once.
    /// </summary>
    public static string? CheckFolder(string folder, string windowsFolder, IReadOnlyList<string> otherSystemFolders)
    {
        if (folder.Length == 0)
        {
            return "Choose the application's folder.";
        }

        if (!Directory.Exists(folder))
        {
            return "That folder does not exist.";
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var root = Path.GetPathRoot(full);
        if (root is not null && string.Equals(Path.TrimEndingDirectorySeparator(root), full, StringComparison.OrdinalIgnoreCase))
        {
            return "Choose the application's own folder, not a whole drive.";
        }

        var windows = Normalize(windowsFolder);
        var insideWindows = windows is not null &&
            (string.Equals(full, windows, StringComparison.OrdinalIgnoreCase) ||
             full.StartsWith(windows + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        var isSystemFolder = otherSystemFolders
            .Select(Normalize)
            .Any(system => string.Equals(full, system, StringComparison.OrdinalIgnoreCase));
        if (insideWindows || isSystemFolder)
        {
            return "Choose the application's own folder, not a system folder.";
        }

        return null;
    }

    private static string? Normalize(string folder) =>
        folder.Length == 0 ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
}
