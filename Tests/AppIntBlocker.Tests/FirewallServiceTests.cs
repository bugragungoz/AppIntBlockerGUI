using System.Text;
using System.Text.Json;
using AppIntBlocker.Core;

namespace AppIntBlocker.Tests;

[TestClass]
public class FirewallServiceTests
{
    [TestMethod]
    public async Task SendsTheRequestAsBase64Json()
    {
        var runner = new FakeRunner(Result("""{"ok":true,"names":["AppBlocker Rule -Demo - a.exe (Inbound)"]}"""));

        var names = await new FirewallService(runner).GetRuleNamesAsync();

        CollectionAssert.AreEqual(new[] { "AppBlocker Rule -Demo - a.exe (Inbound)" }, names.ToArray());
        var request = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(runner.Input))).RootElement;
        Assert.AreEqual("list", request.GetProperty("operation").GetString());
        StringAssert.Contains(runner.Script, "Get-NetFirewallRule");
    }

    [TestMethod]
    public async Task SendsTheRulesToBlock()
    {
        var runner = new FakeRunner(Result("""{"ok":true,"created":2,"skipped":0,"removed":1,"failed":[],"backupPath":"C:\\b.wfw"}"""));
        var rules = PlannedRule.For("Şahin", [new ExeFile(@"C:\Oyunlar\Şahin\ığ.exe", "ığ.exe")]);

        var change = await new FirewallService(runner).BlockAsync(rules, ["old"], @"C:\b.wfw");

        Assert.AreEqual((2, 0, 1, 0, @"C:\b.wfw"), (change.Created, change.Skipped, change.Removed, change.Failed.Count, change.BackupPath));
        var request = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(runner.Input))).RootElement;
        Assert.AreEqual("block", request.GetProperty("operation").GetString());
        Assert.AreEqual(@"C:\b.wfw", request.GetProperty("backupPath").GetString());
        Assert.AreEqual("old", request.GetProperty("removeNames")[0].GetString());
        var first = request.GetProperty("rules")[0];
        Assert.AreEqual("AppBlocker Rule -Şahin - ığ.exe (Inbound)", first.GetProperty("name").GetString());
        Assert.AreEqual(@"C:\Oyunlar\Şahin\ığ.exe", first.GetProperty("program").GetString());
        Assert.AreEqual("Outbound", request.GetProperty("rules")[1].GetProperty("direction").GetString());
    }

    [TestMethod]
    public async Task ReadsAOneItemListWrittenAsTheItemItself()
    {
        var runner = new FakeRunner(Result("""{"ok":true,"created":1,"failed":{"name":"r","error":"Access is denied."}}"""));

        var change = await new FirewallService(runner).BlockAsync([], [], null);

        Assert.AreEqual(1, change.Failed.Count);
        Assert.AreEqual(new FailedRule("r", "Access is denied."), change.Failed[0]);
    }

    [TestMethod]
    public async Task ThrowsTheScriptsError()
    {
        var runner = new FakeRunner(Result("""{"ok":false,"error":"Could not back up the firewall rules: denied"}"""));

        var error = await Assert.ThrowsExceptionAsync<FirewallException>(() => new FirewallService(runner).BackupAsync(@"C:\b.wfw"));

        Assert.AreEqual("Could not back up the firewall rules: denied", error.Message);
    }

    [TestMethod]
    public async Task ThrowsWhenPowerShellPrintsNoResult()
    {
        var runner = new FakeRunner(new ScriptRun(1, "some text", "The term 'Get-NetFirewallRule' is not recognized."));

        var error = await Assert.ThrowsExceptionAsync<FirewallException>(() => new FirewallService(runner).GetRuleNamesAsync());

        StringAssert.Contains(error.Message, "is not recognized");
    }

    private static ScriptRun Result(string json) =>
        new(0, "noise\r\nRESULT:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)) + "\r\n", string.Empty);

    private sealed class FakeRunner(ScriptRun run) : IScriptRunner
    {
        public string Script { get; private set; } = string.Empty;

        public string Input { get; private set; } = string.Empty;

        public Task<ScriptRun> RunAsync(string script, string input, CancellationToken cancellationToken)
        {
            this.Script = script;
            this.Input = input;
            return Task.FromResult(run);
        }
    }
}
