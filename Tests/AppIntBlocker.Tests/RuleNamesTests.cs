using AppIntBlocker.Core;

namespace AppIntBlocker.Tests;

[TestClass]
public class RuleNamesTests
{
    // The expected names are what AppIntBlocker.ps1 writes for the same files.
    [TestMethod]
    public void NamesAFileInTheFolderLikeTheScript()
    {
        Assert.AreEqual("AppBlocker Rule -Demo - main.exe (Inbound)", RuleNames.Build("Demo", "main.exe", RuleDirection.Inbound));
        Assert.AreEqual("AppBlocker Rule -Demo - main.exe (Outbound)", RuleNames.Build("Demo", "main.exe", RuleDirection.Outbound));
    }

    [TestMethod]
    public void NamesAFileInASubfolderByItsPath()
    {
        Assert.AreEqual(@"AppBlocker Rule -Demo - x64\updater.exe (Outbound)", RuleNames.Build("Demo", @"x64\updater.exe", RuleDirection.Outbound));
    }

    [TestMethod]
    public void CutsLongNamesAt220CharactersLikeTheScript()
    {
        var name = RuleNames.Build("Demo", new string('a', 300) + ".exe", RuleDirection.Inbound);

        var expectedBase = ("AppBlocker Rule -Demo - " + new string('a', 300))[..220];
        Assert.AreEqual(expectedBase + "... (Inbound)", name);
    }

    [TestMethod]
    public void ReadsTheApplicationBackFromARuleName()
    {
        Assert.AreEqual("putty", RuleNames.ApplicationOf("AppBlocker Rule -putty - putty.exe (Inbound)"));
        Assert.AreEqual("My App", RuleNames.ApplicationOf(@"AppBlocker Rule -My App - bin\a - b.exe (Outbound)"));
        Assert.IsNull(RuleNames.ApplicationOf("Some other rule"));
    }

    [TestMethod]
    public void ReadsTheFileBackFromARuleName()
    {
        Assert.AreEqual(@"bin\a - b.exe", RuleNames.FileOf(@"AppBlocker Rule -My App - bin\a - b.exe (Outbound)", "My App"));
        Assert.AreEqual("putty.exe", RuleNames.FileOf("AppBlocker Rule -putty - putty.exe (Inbound)", "putty"));
    }

    [TestMethod]
    public void PlansAnInboundAndAnOutboundRulePerFile()
    {
        var files = new[] { new ExeFile(@"C:\Apps\Demo\main.exe", "main.exe"), new ExeFile(@"C:\Apps\Demo\x64\up.exe", @"x64\up.exe") };

        var rules = PlannedRule.For("Demo", files);

        CollectionAssert.AreEqual(
            new[]
            {
                new PlannedRule("AppBlocker Rule -Demo - main.exe (Inbound)", @"C:\Apps\Demo\main.exe", RuleDirection.Inbound),
                new PlannedRule("AppBlocker Rule -Demo - main.exe (Outbound)", @"C:\Apps\Demo\main.exe", RuleDirection.Outbound),
                new PlannedRule(@"AppBlocker Rule -Demo - x64\up.exe (Inbound)", @"C:\Apps\Demo\x64\up.exe", RuleDirection.Inbound),
                new PlannedRule(@"AppBlocker Rule -Demo - x64\up.exe (Outbound)", @"C:\Apps\Demo\x64\up.exe", RuleDirection.Outbound),
            },
            rules.ToArray());
    }

    [TestMethod]
    public void GroupsRulesByApplication()
    {
        var apps = BlockedApp.Group(new[]
        {
            "AppBlocker Rule -putty - putty.exe (Inbound)",
            "AppBlocker Rule -Demo - main.exe (Outbound)",
            "AppBlocker Rule -putty - putty.exe (Outbound)",
            "AppBlocker Rule -demo - main.exe (Inbound)",
            @"AppBlocker Rule -Demo - x64\up.exe (Inbound)",
            "AppBlocker Rule -broken",
        });

        Assert.AreEqual(3, apps.Count);
        Assert.AreEqual("Demo", apps[0].Name);
        Assert.AreEqual(3, apps[0].RuleNames.Count);
        CollectionAssert.AreEqual(new[] { "main.exe", @"x64\up.exe" }, apps[0].Files.ToArray());
        Assert.AreEqual("putty", apps[1].Name);
        CollectionAssert.AreEqual(new[] { "putty.exe" }, apps[1].Files.ToArray());
        Assert.AreEqual(BlockedApp.UnknownName, apps[2].Name);
    }
}
