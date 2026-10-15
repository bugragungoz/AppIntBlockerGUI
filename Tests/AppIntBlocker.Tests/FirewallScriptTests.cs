using System.Text;
using AppIntBlocker.Core;

namespace AppIntBlocker.Tests;

/// <summary>
/// Runs the real firewall.ps1 through the real PowerShellRunner, with stand-ins for the
/// NetSecurity cmdlets and netsh, so no firewall is touched. Runs once with PowerShell 7 (pwsh)
/// and once with Windows PowerShell 5.1, which is what the app uses; a missing one is skipped.
/// </summary>
[TestClass]
public class FirewallScriptTests
{
    // Defined before the script runs; functions take precedence over cmdlets and programs of the
    // same name. Each call is reported on standard output as base64, so non-ASCII text survives
    // any console code page.
    private const string StandIns = """
        $global:Rules = [System.Collections.Generic.List[object]]::new()
        foreach ($name in $SeedNames) { $global:Rules.Add([pscustomobject]@{ DisplayName = $name }) }
        function Write-Call([string]$Text) {
            [Console]::Out.WriteLine("CALL:" + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Text)))
        }
        function Get-NetFirewallRule { param([string]$DisplayName, $ErrorAction)
            @($global:Rules | Where-Object { $_.DisplayName -like $DisplayName })
        }
        function New-NetFirewallRule { param($DisplayName, $Direction, $Program, $Action, $Profile, $Enabled, $ErrorAction)
            if ($Program -like '*denied*') { throw "Access is denied." }
            $global:Rules.Add([pscustomobject]@{ DisplayName = $DisplayName })
            Write-Call "new|$DisplayName|$Program|$Direction|$Action|$Profile|$Enabled"
        }
        function Remove-NetFirewallRule { param([Parameter(ValueFromPipeline = $true)] $InputObject)
            process { [void]$global:Rules.Remove($InputObject); Write-Call "remove|$($InputObject.DisplayName)" }
        }
        function netsh.exe {
            Write-Call ("netsh|" + ($args -join '|'))
            if ($args[2] -like '*denied*') { "The requested operation requires elevation."; $global:LASTEXITCODE = 1 }
            else { Set-Content -LiteralPath $args[2] -Value 'policy'; "Ok."; $global:LASTEXITCODE = 0 }
        }

        """;

    private string folder = string.Empty;

    [TestInitialize]
    public void CreateFolder()
    {
        this.folder = Path.Combine(Path.GetTempPath(), "AppIntBlockerScript-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.folder);
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(this.folder, recursive: true);

    [DataTestMethod]
    [DataRow("pwsh")]
    [DataRow("powershell")]
    public async Task ListsOnlyTheScriptsRules(string shell)
    {
        var runner = Runner(shell, "AppBlocker Rule -Demo - a.exe (Inbound)", "Some other rule", "AppBlocker Rule -Şahin - ığ.exe (Outbound)");

        var names = await new FirewallService(runner).GetRuleNamesAsync();

        CollectionAssert.AreEquivalent(new[] { "AppBlocker Rule -Demo - a.exe (Inbound)", "AppBlocker Rule -Şahin - ığ.exe (Outbound)" }, names.ToArray());
    }

    [DataTestMethod]
    [DataRow("pwsh")]
    [DataRow("powershell")]
    public async Task BlocksEveryFileInBothDirections(string shell)
    {
        var runner = Runner(shell, "AppBlocker Rule -Şahin - ığ.exe (Inbound)");
        var rules = PlannedRule.For("Şahin", [new ExeFile(@"C:\Oyunlar\Şahin\ığ.exe", "ığ.exe"), new ExeFile(@"C:\Oyunlar\Şahin\x64\up.exe", @"x64\up.exe")]);

        var change = await new FirewallService(runner).BlockAsync(rules, [], null);

        Assert.AreEqual(3, change.Created);
        Assert.AreEqual(1, change.Skipped, "the rule that already existed");
        Assert.AreEqual(0, change.Failed.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                @"new|AppBlocker Rule -Şahin - ığ.exe (Outbound)|C:\Oyunlar\Şahin\ığ.exe|Outbound|Block|Any|True",
                @"new|AppBlocker Rule -Şahin - x64\up.exe (Inbound)|C:\Oyunlar\Şahin\x64\up.exe|Inbound|Block|Any|True",
                @"new|AppBlocker Rule -Şahin - x64\up.exe (Outbound)|C:\Oyunlar\Şahin\x64\up.exe|Outbound|Block|Any|True",
            },
            runner.Calls.ToArray());
    }

    [DataTestMethod]
    [DataRow("pwsh")]
    [DataRow("powershell")]
    public async Task BacksUpThenReplacesThenAdds(string shell)
    {
        var runner = Runner(shell, "AppBlocker Rule -Demo - old.exe (Inbound)", "AppBlocker Rule -Other - b.exe (Inbound)");
        var backup = Path.Combine(this.folder, "My Backups", "FirewallBackup.wfw");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        var rules = PlannedRule.For("Demo", [new ExeFile(@"C:\Apps\Demo\new.exe", "new.exe")]);

        var change = await new FirewallService(runner).BlockAsync(rules, ["AppBlocker Rule -Demo - old.exe (Inbound)", "AppBlocker Rule -Demo - gone.exe (Inbound)"], backup);

        Assert.AreEqual((2, 0, 1, 0, backup), (change.Created, change.Skipped, change.Removed, change.Failed.Count, change.BackupPath));
        Assert.IsTrue(File.Exists(backup));
        CollectionAssert.AreEqual(
            new[]
            {
                $"netsh|advfirewall|export|{backup}",
                "remove|AppBlocker Rule -Demo - old.exe (Inbound)",
                @"new|AppBlocker Rule -Demo - new.exe (Inbound)|C:\Apps\Demo\new.exe|Inbound|Block|Any|True",
                @"new|AppBlocker Rule -Demo - new.exe (Outbound)|C:\Apps\Demo\new.exe|Outbound|Block|Any|True",
            },
            runner.Calls.ToArray());
    }

    [DataTestMethod]
    [DataRow("pwsh")]
    [DataRow("powershell")]
    public async Task ChangesNothingWhenTheBackupFails(string shell)
    {
        var runner = Runner(shell, "AppBlocker Rule -Demo - old.exe (Inbound)");
        var rules = PlannedRule.For("Demo", [new ExeFile(@"C:\Apps\Demo\new.exe", "new.exe")]);

        var error = await Assert.ThrowsExceptionAsync<FirewallException>(() =>
            new FirewallService(runner).BlockAsync(rules, ["AppBlocker Rule -Demo - old.exe (Inbound)"], Path.Combine(this.folder, "denied.wfw")));

        StringAssert.Contains(error.Message, "requires elevation");
        Assert.AreEqual(1, runner.Calls.Count, string.Join(Environment.NewLine, runner.Calls));
        StringAssert.StartsWith(runner.Calls[0], "netsh|");
    }

    [DataTestMethod]
    [DataRow("pwsh")]
    [DataRow("powershell")]
    public async Task KeepsGoingWhenOneRuleIsRefused(string shell)
    {
        var runner = Runner(shell);
        var rules = PlannedRule.For("Demo", [new ExeFile(@"C:\denied\a.exe", "a.exe"), new ExeFile(@"C:\Apps\b.exe", "b.exe")]);

        var change = await new FirewallService(runner).BlockAsync(rules, [], null);

        Assert.AreEqual(2, change.Created);
        Assert.AreEqual(2, change.Failed.Count);
        Assert.AreEqual(new FailedRule("AppBlocker Rule -Demo - a.exe (Inbound)", "Access is denied."), change.Failed[0]);
    }

    [DataTestMethod]
    [DataRow("pwsh")]
    [DataRow("powershell")]
    public async Task RemovesOnlyTheNamedRulesOfTheScript(string shell)
    {
        var runner = Runner(shell, "AppBlocker Rule -Demo - a.exe (Inbound)", "AppBlocker Rule -Demo - a.exe (Outbound)", "Other rule");

        var change = await new FirewallService(runner).RemoveAsync(["AppBlocker Rule -Demo - a.exe (Inbound)", "Other rule"]);

        Assert.AreEqual(1, change.Removed);
        CollectionAssert.AreEqual(new[] { "remove|AppBlocker Rule -Demo - a.exe (Inbound)" }, runner.Calls.ToArray());
    }

    private static StandInRunner Runner(string shell, params string[] seedNames)
    {
        var executable = Find(shell);
        if (executable is null)
        {
            Assert.Inconclusive($"{shell} is not installed here.");
        }

        var seed = "$SeedNames = @(" + string.Join(", ", seedNames.Select(name => "'" + name.Replace("'", "''") + "'")) + ")\n";
        return new StandInRunner(new PowerShellRunner(executable), seed + StandIns);
    }

    private static string? Find(string shell)
    {
        if (shell == "powershell")
        {
            var path = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            return OperatingSystem.IsWindows() && File.Exists(path) ? path : null;
        }

        var configured = Environment.GetEnvironmentVariable("APPINTBLOCKER_PWSH");
        if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
        {
            return configured;
        }

        var name = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>Puts the stand-ins in front of the script and collects the calls they report.</summary>
    private sealed class StandInRunner(IScriptRunner inner, string standIns) : IScriptRunner
    {
        public List<string> Calls { get; } = [];

        public async Task<ScriptRun> RunAsync(string script, string input, CancellationToken cancellationToken)
        {
            var run = await inner.RunAsync(standIns + script, input, cancellationToken);
            this.Calls.AddRange(run.Output
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("CALL:", StringComparison.Ordinal))
                .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line["CALL:".Length..]))));
            return run;
        }
    }
}
