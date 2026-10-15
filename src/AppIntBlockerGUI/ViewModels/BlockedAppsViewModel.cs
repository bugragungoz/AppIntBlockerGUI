using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using AppIntBlocker.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppIntBlockerGUI.ViewModels;

/// <summary>One row of the Blocked apps page.</summary>
public sealed class BlockedAppItem(BlockedApp app)
{
    private const int PreviewFiles = 3;

    public string Name => app.Name;

    public IReadOnlyList<string> RuleNames => app.RuleNames;

    public string Summary =>
        $"{Plural.Of(app.Files.Count, "file", "files")}, {Plural.Of(app.RuleNames.Count, "rule", "rules")}";

    public string FilesPreview =>
        app.Files.Count <= PreviewFiles
            ? string.Join(", ", app.Files)
            : string.Join(", ", app.Files.Take(PreviewFiles)) + $" and {app.Files.Count - PreviewFiles} more";

    public string RemoveLabel => $"Remove the rules for {app.Name}";
}

/// <summary>The Blocked apps page: what the firewall holds, and removing it.</summary>
public sealed partial class BlockedAppsViewModel : ObservableObject
{
    private readonly FirewallService firewall;
    private readonly ActivityLog log;
    private readonly Confirm confirm;
    private bool stale = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand), nameof(RemoveCommand), nameof(RemoveAllCommand), nameof(BackupCommand))]
    private bool isBusy;

    [ObservableProperty]
    private bool hasApps;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string statusKind = string.Empty;

    public BlockedAppsViewModel(FirewallService firewall, ActivityLog log, Confirm confirm, Action goToBlock)
    {
        this.firewall = firewall;
        this.log = log;
        this.confirm = confirm;
        this.GoToBlockCommand = new RelayCommand(goToBlock);
    }

    public ObservableCollection<BlockedAppItem> Apps { get; } = [];

    public IRelayCommand GoToBlockCommand { get; }

    private bool IsIdle => !this.IsBusy;

    /// <summary>Called after rules change elsewhere, so the list is read again when the page opens.</summary>
    public void MarkStale() => this.stale = true;

    public async Task LoadIfStaleAsync()
    {
        if (this.stale && !this.IsBusy)
        {
            await this.RefreshAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task RefreshAsync()
    {
        await this.RunAsync("Reading the firewall rules...", async () =>
        {
            await this.LoadAsync();
            this.ShowStatus(string.Empty, string.Empty);
        });
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task RemoveAsync(BlockedAppItem? item)
    {
        if (item is null || !await this.confirm(
                $"Remove the rules for {item.Name}?",
                $"{item.Name} gets its internet access back. {Plural.Of(item.RuleNames.Count, "rule is", "rules are")} removed.",
                "Remove",
                destructive: true))
        {
            return;
        }

        await this.RunAsync("Removing the rules...", async () =>
        {
            var change = await this.firewall.RemoveAsync(item.RuleNames);
            this.Report(change, $"{item.Name} is no longer blocked.");
            await this.LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task RemoveAllAsync()
    {
        var names = this.Apps.SelectMany(app => app.RuleNames).ToList();
        if (names.Count == 0 || !await this.confirm(
                $"Remove all {Plural.Of(names.Count, "rule", "rules")}?",
                "Every application in this list gets its internet access back.",
                "Remove all",
                destructive: true))
        {
            return;
        }

        await this.RunAsync("Removing the rules...", async () =>
        {
            var change = await this.firewall.RemoveAsync(names);
            this.Report(change, "No application is blocked any more.");
            await this.LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task BackupAsync()
    {
        await this.RunAsync("Backing up the firewall rules...", async () =>
        {
            var path = await this.firewall.BackupAsync(AppPaths.NewBackupPath());
            this.log.Add($"Backed up the firewall rules to {path}");
            this.ShowStatus($"Done: backed up to {path}", "Ok");
        });
    }

    private async Task LoadAsync()
    {
        var names = await this.firewall.GetRuleNamesAsync();
        this.Apps.Clear();
        foreach (var app in BlockedApp.Group(names))
        {
            this.Apps.Add(new BlockedAppItem(app));
        }

        this.stale = false;
        this.HasApps = this.Apps.Count > 0;
        this.IsEmpty = this.Apps.Count == 0;
    }

    private void Report(FirewallChange change, string doneMessage)
    {
        this.log.Add($"Removed {Plural.Of(change.Removed, "rule", "rules")}.");
        foreach (var failure in change.Failed)
        {
            this.log.Add($"Error: {failure.Name}: {failure.Error}");
        }

        if (change.Failed.Count == 0)
        {
            this.ShowStatus($"Done: {doneMessage} {Plural.Of(change.Removed, "rule", "rules")} removed.", "Ok");
        }
        else
        {
            this.ShowStatus($"Error: {Plural.Of(change.Failed.Count, "rule", "rules")} could not be removed. The activity log on the Block page lists them.", "Error");
        }
    }

    private async Task RunAsync(string working, Func<Task> action)
    {
        this.IsBusy = true;
        this.ShowStatus(working, string.Empty);
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is FirewallException or Win32Exception or IOException or UnauthorizedAccessException)
        {
            this.log.Add("Error: " + ex.Message);
            this.ShowStatus("Error: " + ex.Message, "Error");
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    private void ShowStatus(string message, string kind)
    {
        this.StatusMessage = message;
        this.StatusKind = kind;
    }
}
