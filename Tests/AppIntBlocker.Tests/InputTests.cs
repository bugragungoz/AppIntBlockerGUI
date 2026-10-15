using AppIntBlocker.Core;

namespace AppIntBlocker.Tests;

[TestClass]
public class InputTests
{
    private string folder = string.Empty;

    [TestInitialize]
    public void CreateFolder()
    {
        this.folder = Path.Combine(Path.GetTempPath(), "AppIntBlockerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.folder);
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(this.folder, recursive: true);

    [TestMethod]
    public void DropsTheQuotesOfCopyAsPath()
    {
        Assert.AreEqual(@"C:\Program Files\Example", InputChecks.NormalizePath("  \"C:\\Program Files\\Example\"  "));
    }

    [TestMethod]
    public void ChecksTheApplicationName()
    {
        Assert.IsNull(InputChecks.CheckApplicationName("Example"));
        Assert.IsNull(InputChecks.CheckApplicationName("Demo-Beta"));
        Assert.IsNotNull(InputChecks.CheckApplicationName("   "));
        Assert.IsNotNull(InputChecks.CheckApplicationName("Demo - Beta"));
        Assert.IsNotNull(InputChecks.CheckApplicationName("Demo -Beta"));
    }

    [TestMethod]
    public void SuggestsAValidNameFromTheFolder()
    {
        Assert.AreEqual("Example", InputChecks.SuggestName(Path.Combine("Apps", "Example") + Path.DirectorySeparatorChar));
        Assert.AreEqual("Game-Deluxe", InputChecks.SuggestName(Path.Combine("Games", "Game - Deluxe")));
    }

    [TestMethod]
    public void AcceptsAnApplicationFolder()
    {
        Assert.IsNull(InputChecks.CheckFolder(this.folder, Path.Combine(this.folder, "Windows"), []));
    }

    [TestMethod]
    public void RefusesAMissingFolderAndAWholeDrive()
    {
        Assert.IsNotNull(InputChecks.CheckFolder("", "", []));
        Assert.IsNotNull(InputChecks.CheckFolder(Path.Combine(this.folder, "missing"), "", []));
        Assert.IsNotNull(InputChecks.CheckFolder(Path.GetPathRoot(this.folder)!, "", []));
    }

    [TestMethod]
    public void RefusesTheWindowsFolderAndEverythingInIt()
    {
        var windows = Directory.CreateDirectory(Path.Combine(this.folder, "Windows")).FullName;
        var system32 = Directory.CreateDirectory(Path.Combine(windows, "System32")).FullName;
        var lookalike = Directory.CreateDirectory(Path.Combine(this.folder, "WindowsApps")).FullName;

        Assert.IsNotNull(InputChecks.CheckFolder(windows, windows, []));
        Assert.IsNotNull(InputChecks.CheckFolder(system32, windows, []));
        Assert.IsNull(InputChecks.CheckFolder(lookalike, windows, []));
    }

    [TestMethod]
    public void RefusesProgramFilesButNotTheProgramsInIt()
    {
        var programFiles = Directory.CreateDirectory(Path.Combine(this.folder, "Program Files")).FullName;
        var program = Directory.CreateDirectory(Path.Combine(programFiles, "Example")).FullName;

        Assert.IsNotNull(InputChecks.CheckFolder(programFiles + Path.DirectorySeparatorChar, "", [programFiles]));
        Assert.IsNull(InputChecks.CheckFolder(program, "", [programFiles]));
    }

    [TestMethod]
    public void FindsEveryExeInTheFolderAndItsSubfolders()
    {
        Directory.CreateDirectory(Path.Combine(this.folder, "x64"));
        foreach (var file in new[] { "Main.EXE", "core.dll", "notes.exe.txt", Path.Combine("x64", "updater.exe"), "a.exe" })
        {
            File.WriteAllText(Path.Combine(this.folder, file), string.Empty);
        }

        var found = ExeScanner.Find(this.folder);

        CollectionAssert.AreEqual(
            new[] { "a.exe", "Main.EXE", Path.Combine("x64", "updater.exe") },
            found.Select(file => file.RelativePath).ToArray());
        Assert.AreEqual(Path.Combine(this.folder, "Main.EXE"), found[1].FullPath);
    }
}
