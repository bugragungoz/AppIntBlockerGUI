using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using AppIntBlocker.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppIntBlockerGUI.ViewModels;

/// <summary>The window: two pages, the confirmation dialog and the links.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    public const string GitHubProfile = "https://github.com/bugragungoz";

    private readonly ActivityLog log;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBlock), nameof(ShowBlockedApps))]
    private object currentPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen), nameof(IsDialogClosed))]
    private ConfirmDialog? dialog;

    public MainViewModel(FirewallService firewall, ActivityLog log)
    {
        this.log = log;
        this.Block = new BlockViewModel(firewall, log, this.ConfirmAsync, () => this.BlockedApps!.MarkStale());
        this.BlockedApps = new BlockedAppsViewModel(firewall, log, this.ConfirmAsync, () => this.ShowBlock = true);
        this.currentPage = this.Block;
        log.Add("Ready. Choose an application's folder to block it.");
    }

    public BlockViewModel Block { get; }

    public BlockedAppsViewModel BlockedApps { get; }

    public bool IsDialogOpen => this.Dialog is not null;

    public bool IsDialogClosed => this.Dialog is null;

    public string Version { get; } =
        "AppIntBlocker " + (Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? string.Empty);

    public bool ShowBlock
    {
        get => this.CurrentPage == this.Block;
        set
        {
            if (value)
            {
                this.CurrentPage = this.Block;
            }
        }
    }

    public bool ShowBlockedApps
    {
        get => this.CurrentPage == this.BlockedApps;
        set
        {
            if (value)
            {
                this.CurrentPage = this.BlockedApps;
                _ = this.BlockedApps.LoadIfStaleAsync();
            }
        }
    }

    public async Task<bool> ConfirmAsync(string title, string body, string confirmText, bool destructive)
    {
        this.Dialog = new ConfirmDialog(title, body, confirmText, destructive);
        try
        {
            return await this.Dialog.Answer;
        }
        finally
        {
            this.Dialog = null;
        }
    }

    [RelayCommand]
    private void OpenFirewall() => this.Open("wf.msc");

    [RelayCommand]
    private void OpenGitHub() => this.Open(GitHubProfile);

    private void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            this.log.Add($"Error: Could not open {target}: {ex.Message}");
        }
    }
}
