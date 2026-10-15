using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AppIntBlocker.Core;

/// <summary>A rule to add: block <see cref="Program"/> in one direction.</summary>
public sealed record PlannedRule(string Name, string Program, RuleDirection Direction)
{
    /// <summary>Two rules, inbound and outbound, for each file, named like AppIntBlocker.ps1 names them.</summary>
    public static IReadOnlyList<PlannedRule> For(string applicationName, IEnumerable<ExeFile> files) =>
        files
            .SelectMany(file => new[] { RuleDirection.Inbound, RuleDirection.Outbound }
                .Select(direction => new PlannedRule(RuleNames.Build(applicationName, file.RelativePath, direction), file.FullPath, direction)))
            .ToList();
}

/// <summary>A rule the firewall refused, with its reason.</summary>
public sealed record FailedRule(string Name, string Error);

/// <summary>What a block or remove request changed.</summary>
public sealed record FirewallChange(int Created, int Skipped, int Removed, IReadOnlyList<FailedRule> Failed, string? BackupPath);

/// <summary>The firewall refused a whole request, or PowerShell could not run it.</summary>
public sealed class FirewallException(string message) : Exception(message);

/// <summary>Reads and changes the script's Windows Firewall rules through firewall.ps1.</summary>
public sealed class FirewallService
{
    private static readonly Lazy<string> Script = new(() =>
    {
        using var stream = typeof(FirewallService).Assembly.GetManifestResourceStream("AppIntBlocker.Core.firewall.ps1")
            ?? throw new InvalidOperationException("firewall.ps1 is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    private readonly IScriptRunner runner;

    public FirewallService(IScriptRunner runner)
    {
        this.runner = runner;
    }

    public async Task<IReadOnlyList<string>> GetRuleNamesAsync(CancellationToken cancellationToken = default)
    {
        var result = await this.RunAsync(new { operation = "list" }, cancellationToken);
        return Strings(result, "names");
    }

    public async Task<string> BackupAsync(string path, CancellationToken cancellationToken = default)
    {
        await this.RunAsync(new { operation = "backup", backupPath = path }, cancellationToken);
        return path;
    }

    public async Task<FirewallChange> RemoveAsync(IReadOnlyCollection<string> ruleNames, CancellationToken cancellationToken = default)
    {
        var result = await this.RunAsync(new { operation = "remove", names = ruleNames }, cancellationToken);
        return Change(result);
    }

    /// <summary>
    /// Adds <paramref name="rules"/>, skipping names that already exist. First backs up the firewall to
    /// <paramref name="backupPath"/> when one is given, then removes <paramref name="replaceRuleNames"/>.
    /// </summary>
    public async Task<FirewallChange> BlockAsync(
        IReadOnlyList<PlannedRule> rules,
        IReadOnlyCollection<string> replaceRuleNames,
        string? backupPath,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            operation = "block",
            backupPath,
            removeNames = replaceRuleNames,
            rules = rules.Select(rule => new { name = rule.Name, program = rule.Program, direction = rule.Direction.ToString() }),
        };
        var result = await this.RunAsync(request, cancellationToken);
        return Change(result);
    }

    private async Task<JsonElement> RunAsync(object request, CancellationToken cancellationToken)
    {
        var input = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)));
        var run = await this.runner.RunAsync(Script.Value, input, cancellationToken);

        var line = run.Output
            .Split('\n')
            .Select(text => text.Trim())
            .LastOrDefault(text => text.StartsWith("RESULT:", StringComparison.Ordinal));
        if (line is null)
        {
            var details = string.IsNullOrWhiteSpace(run.Errors) ? $"exit code {run.ExitCode}" : run.Errors.Trim();
            throw new FirewallException($"PowerShell returned no result ({details}).");
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(line["RESULT:".Length..]));
        var result = JsonDocument.Parse(json).RootElement.Clone();
        if (!result.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
        {
            var error = result.TryGetProperty("error", out var message) ? message.GetString() : null;
            throw new FirewallException(error ?? "PowerShell reported an error without a message.");
        }

        return result;
    }

    private static FirewallChange Change(JsonElement result)
    {
        var failed = Items(result, "failed")
            .Select(item => new FailedRule(Text(item, "name") ?? string.Empty, Text(item, "error") ?? string.Empty))
            .ToList();
        return new FirewallChange(Number(result, "created"), Number(result, "skipped"), Number(result, "removed"), failed, Text(result, "backupPath"));
    }

    // Windows PowerShell's ConvertTo-Json can write a one-item list as the item itself; read both.
    private static IEnumerable<JsonElement> Items(JsonElement result, string name)
    {
        if (!result.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value];
    }

    private static IReadOnlyList<string> Strings(JsonElement result, string name) =>
        Items(result, name).Select(item => item.GetString() ?? string.Empty).ToList();

    private static int Number(JsonElement result, string name) =>
        result.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

    private static string? Text(JsonElement result, string name) =>
        result.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
