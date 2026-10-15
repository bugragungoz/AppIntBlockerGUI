using System.Diagnostics;
using System.Text;

namespace AppIntBlocker.Core;

/// <summary>What a script run printed.</summary>
public sealed record ScriptRun(int ExitCode, string Output, string Errors);

public interface IScriptRunner
{
    /// <summary>Runs <paramref name="script"/> and writes <paramref name="input"/> to its standard input.</summary>
    Task<ScriptRun> RunAsync(string script, string input, CancellationToken cancellationToken);
}

/// <summary>Runs a script with powershell.exe (or another PowerShell, for tests) as a child process.</summary>
public sealed class PowerShellRunner : IScriptRunner
{
    private readonly string executable;

    public PowerShellRunner(string executable)
    {
        this.executable = executable;
    }

    /// <summary>Windows PowerShell from System32, by full path so nothing earlier on PATH runs instead.</summary>
    public static PowerShellRunner WindowsPowerShell() =>
        new(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"));

    public async Task<ScriptRun> RunAsync(string script, string input, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(this.executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass" })
        {
            start.ArgumentList.Add(argument);
        }

        // -EncodedCommand takes the script as base64 UTF-16, so it never passes through a file.
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {this.executable}.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellationToken);
            return new ScriptRun(process.ExitCode, await output, await errors);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
