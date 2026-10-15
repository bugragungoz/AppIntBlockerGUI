namespace AppIntBlocker.Core;

/// <summary>An application with block rules, as the rule names describe it.</summary>
public sealed record BlockedApp(string Name, IReadOnlyList<string> RuleNames, IReadOnlyList<string> Files)
{
    public const string UnknownName = "Unknown application";

    /// <summary>Groups rule names by application, the way AppIntBlocker.ps1 lists them.</summary>
    public static IReadOnlyList<BlockedApp> Group(IEnumerable<string> ruleNames)
    {
        return ruleNames
            .GroupBy(name => Core.RuleNames.ApplicationOf(name) ?? UnknownName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var names = group.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
                var files = names
                    .Select(name => Core.RuleNames.FileOf(name, group.Key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new BlockedApp(group.Key, names, files);
            })
            .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
