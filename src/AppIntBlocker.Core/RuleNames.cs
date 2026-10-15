using System.Text.RegularExpressions;

namespace AppIntBlocker.Core;

public enum RuleDirection
{
    Inbound,
    Outbound,
}

/// <summary>
/// Firewall rule names. They follow AppIntBlocker.ps1 exactly, so the app and the PowerShell
/// script find and remove each other's rules:
/// "AppBlocker Rule -{application} - {path inside the folder} (Inbound|Outbound)".
/// </summary>
public static class RuleNames
{
    public const string Prefix = "AppBlocker Rule -";

    // The script cuts the part before the direction at 220 characters and appends "...".
    private const int MaxBaseLength = 220;

    // Same pattern the script groups by: the application name ends at the first " -".
    private static readonly Regex ApplicationPattern =
        new("^" + Regex.Escape(Prefix) + @"(.*?)\s+-", RegexOptions.CultureInvariant);

    public static string Build(string applicationName, string relativePath, RuleDirection direction)
    {
        var baseName = $"{Prefix}{applicationName} - {relativePath}";
        if (baseName.Length > MaxBaseLength)
        {
            baseName = baseName[..MaxBaseLength] + "...";
        }

        return $"{baseName} ({direction})";
    }

    /// <summary>The application part of a rule name, or null for a name the script did not write.</summary>
    public static string? ApplicationOf(string ruleName)
    {
        var match = ApplicationPattern.Match(ruleName);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>The file part of a rule name: what follows the application, without the direction.</summary>
    public static string FileOf(string ruleName, string applicationName)
    {
        var start = $"{Prefix}{applicationName} - ";
        var file = ruleName.StartsWith(start, StringComparison.OrdinalIgnoreCase) ? ruleName[start.Length..] : ruleName;
        foreach (var direction in Enum.GetNames<RuleDirection>())
        {
            var suffix = $" ({direction})";
            if (file.EndsWith(suffix, StringComparison.Ordinal))
            {
                return file[..^suffix.Length];
            }
        }

        return file;
    }
}
