using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using AppIntBlocker.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AppIntBlockerGUI.ViewModels;

/// <summary>An .exe in the chosen folder, with whether it gets blocked.</summary>
public sealed partial class FileChoice(ExeFile file, Action selectionChanged) : ObservableObject
{
    [ObservableProperty]
    private bool isSelected = true;

    public ExeFile File { get; } = file;

    public string RelativePath => this.File.RelativePath;

    partial void OnIsSelectedChanged(bool value) => selectionChanged();
}

/// <summary>The Block page: choose a folder, review its .exe files, block them.</summary>
public sealed partial class BlockViewModel : ObservableObject
{
    private const string FolderHint = "Every .exe in the folder and its subfolders gets two rules: inbound and outbound.";
    private const string NameHint = "Shown in Windows Firewall as AppBlocker Rule -<name> - <file>.";

    private readonly FirewallService firewall;
    private readonly ActivityLog log;
    private readonly Confirm confirm;
    private readonly Action blocked;
    private CancellationTokenSource? scan;
    private bool scanning;
    private string scannedFolder = string.Empty;
    private string suggestedName = string.Empty;

    [ObservableProperty]
    private string folder = string.Empty;

    [ObservableProperty]
    private string folderMessage = FolderHint;

    [ObservableProperty]
    private string folderMessageKind = string.Empty;

    [ObservableProperty]
    private string applicationName = string.Empty;

    [ObservableProperty]
    private string nameMessage = NameHint;

    [ObservableProperty]
    private string nameMessageKind = string.Empty;

    [ObservableProperty]
    private string selectionText = string.Empty;

    [ObservableProperty]
    private bool hasFiles;

    [ObservableProperty]
    private bool backUpFirst = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BlockCommand), nameof(BrowseCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string resultMessage = string.Empty;

    [ObservableProperty]
    private string resultKind = string.Empty;

    public BlockViewModel(FirewallService firewall, ActivityLog log, Confirm confirm, Action blocked)
    {
        this.firewall = firewall;
        this.log = log;
        this.confirm = confirm;
        this.blocked = blocked;
    }

    public ObservableCollection<FileChoice> Files { get; } = [];

    public ActivityLog Log => this.log;

    public string BackupFolder => AppPaths.BackupFolder;

    private bool IsIdle => !this.IsBusy;

    partial void OnApplicationNameChanged(string value)
    {
        var error = value.Length == 0 ? null : InputChecks.CheckApplicationName(value);
        this.NameMessage = error is null ? NameHint : "Error: " + error;
        this.NameMessageKind = error is null ? string.Empty : "Error";
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task BrowseAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the application's folder" };
        if (Directory.Exists(this.Folder))
        {
            dialog.InitialDirectory = this.Folder;
        }

        if (dialog.ShowDialog() == true)
        {
            this.Folder = dialog.FolderName;
            await this.ScanAsync();
        }
    }

    /// <summary>Lists the .exe files of the folder in the box; runs when it loses focus or on Enter.</summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        var path = InputChecks.NormalizePath(this.Folder);
        if (path != this.Folder)
        {
            this.Folder = path;
        }

        // The same folder again (Enter, then leaving the box) needs no second scan while one runs or
        // after one found files; after an error it is scanned again.
        var sameFolder = string.Equals(path, this.scannedFolder, StringComparison.OrdinalIgnoreCase);
        if (sameFolder && (this.scanning || this.HasFiles))
        {
            return;
        }

        this.scan?.Cancel();
        this.scan = new CancellationTokenSource();
        var token = this.scan.Token;
        this.scannedFolder = path;
        this.Files.Clear();
        this.HasFiles = false;
        this.SelectionText = string.Empty;
        this.ShowResult(string.Empty, string.Empty);
        if (path.Length == 0)
        {
            this.ShowFolderMessage(FolderHint, string.Empty);
            return;
        }

        var error = InputChecks.CheckFolder(path, AppPaths.WindowsFolder, AppPaths.OtherSystemFolders);
        if (error is not null)
        {
            this.ShowFolderMessage("Error: " + error, "Error");
            return;
        }

        this.ShowFolderMessage("Looking for .exe files...", string.Empty);
        IReadOnlyList<ExeFile> found;
        this.scanning = true;
        try
        {
            found = await Task.Run(() => ExeScanner.Find(path, token), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            this.ShowFolderMessage("Error: The folder could not be read. " + ex.Message, "Error");
            return;
        }
        finally
        {
            // A newer scan may have started meanwhile; only the latest one clears the flag.
            if (this.scan?.Token == token)
            {
                this.scanning = false;
            }
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        foreach (var file in found)
        {
            this.Files.Add(new FileChoice(file, this.UpdateSelectionText));
        }

        this.HasFiles = found.Count > 0;
        this.UpdateSelectionText();
        if (found.Count == 0)
        {
            this.ShowFolderMessage("Error: There is no .exe file in this folder or its subfolders.", "Error");
        }
        else
        {
            this.ShowFolderMessage(Plural.Of(found.Count, ".exe file", ".exe files") + " found. Clear the ones that should stay online.", string.Empty);
        }

        // Fill in the name from the folder, unless the user typed one.
        if (this.ApplicationName.Length == 0 || this.ApplicationName == this.suggestedName)
        {
            this.suggestedName = InputChecks.SuggestName(path);
            this.ApplicationName = this.suggestedName;
        }
    }

    [RelayCommand]
    private void SelectAll() => this.SetAll(true);

    [RelayCommand]
    private void SelectNone() => this.SetAll(false);

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task BlockAsync()
    {
        this.ShowResult(string.Empty, string.Empty);
        var name = this.ApplicationName.Trim();
        var nameError = InputChecks.CheckApplicationName(name);
        if (nameError is not null)
        {
            this.NameMessage = "Error: " + nameError;
            this.NameMessageKind = "Error";
            this.ShowResult("Error: Check the name above.", "Error");
            return;
        }

        if (!string.Equals(InputChecks.NormalizePath(this.Folder), this.scannedFolder, StringComparison.OrdinalIgnoreCase))
        {
            await this.ScanAsync();
            this.ShowResult("The file list now shows the new folder. Check it, then choose Block again.", "Info");
            return;
        }

        var selected = this.Files.Where(file => file.IsSelected).Select(file => file.File).ToList();
        if (selected.Count == 0)
        {
            this.ShowResult("Error: Select at least one file to block.", "Error");
            return;
        }

        this.IsBusy = true;
        try
        {
            this.ShowResult("Reading the firewall rules...", string.Empty);
            var existing = await this.firewall.GetRuleNamesAsync();
            var replace = existing
                .Where(rule => string.Equals(RuleNames.ApplicationOf(rule), name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (replace.Count > 0 && !await this.confirm(
                    $"Replace the rules for {name}?",
                    $"{name} already has {Plural.Of(replace.Count, "rule", "rules")}. Replacing them removes those rules and blocks the files selected now.",
                    "Replace",
                    destructive: false))
            {
                this.ShowResult("Nothing changed.", string.Empty);
                return;
            }

            var rules = PlannedRule.For(name, selected);
            var backup = this.BackUpFirst ? AppPaths.NewBackupPath() : null;
            this.ShowResult(backup is null ? "Adding the rules..." : "Backing up, then adding the rules...", string.Empty);
            this.log.Add($"Blocking {Plural.Of(selected.Count, "file", "files")} of {name}: {Plural.Of(rules.Count, "rule", "rules")}.");

            var change = await this.firewall.BlockAsync(rules, replace, backup);
            if (change.BackupPath is not null)
            {
                this.log.Add($"Backed up the firewall rules to {change.BackupPath}");
            }

            if (change.Removed > 0)
            {
                this.log.Add($"Removed {Plural.Of(change.Removed, "earlier rule", "earlier rules")} of {name}.");
            }

            foreach (var failure in change.Failed)
            {
                this.log.Add($"Error: {failure.Name}: {failure.Error}");
            }

            this.log.Add($"Added {Plural.Of(change.Created, "rule", "rules")}" +
                (change.Skipped > 0 ? $"; {Plural.Of(change.Skipped, "rule", "rules")} already existed." : "."));
            this.blocked();

            if (change.Failed.Count == 0)
            {
                this.ShowResult($"Done: {name} is blocked. {Plural.Of(change.Created + change.Skipped, "rule", "rules")} in place.", "Ok");
            }
            else
            {
                this.ShowResult($"Error: {change.Failed.Count} of {Plural.Of(rules.Count, "rule", "rules")} could not be added. The activity log below lists them.", "Error");
            }
        }
        catch (Exception ex) when (ex is FirewallException or Win32Exception or IOException or UnauthorizedAccessException)
        {
            this.log.Add("Error: " + ex.Message);
            this.ShowResult("Error: " + ex.Message, "Error");
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    private void SetAll(bool selected)
    {
        foreach (var file in this.Files)
        {
            file.IsSelected = selected;
        }
    }

    private void UpdateSelectionText()
    {
        var count = this.Files.Count(file => file.IsSelected);
        this.SelectionText = $"{count} of {this.Files.Count} selected";
    }

    private void ShowFolderMessage(string message, string kind)
    {
        this.FolderMessage = message;
        this.FolderMessageKind = kind;
    }

    private void ShowResult(string message, string kind)
    {
        this.ResultMessage = message;
        this.ResultKind = kind;
    }
}
