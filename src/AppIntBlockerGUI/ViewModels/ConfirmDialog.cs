using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppIntBlockerGUI.ViewModels;

/// <summary>Asks the user to confirm a change; pages await the answer.</summary>
public delegate Task<bool> Confirm(string title, string body, string confirmText, bool destructive);

/// <summary>
/// A question in the dialog over the window: the title names the decision, the body says what
/// happens, and the buttons repeat the action ("Remove", "Cancel").
/// </summary>
public sealed partial class ConfirmDialog(string title, string body, string confirmText, bool destructive) : ObservableObject
{
    private readonly TaskCompletionSource<bool> answer = new();

    public string Title { get; } = title;

    public string Body { get; } = body;

    public string ConfirmText { get; } = confirmText;

    public bool IsDestructive { get; } = destructive;

    public bool IsSafe => !this.IsDestructive;

    public Task<bool> Answer => this.answer.Task;

    [RelayCommand]
    private void Accept() => this.answer.TrySetResult(true);

    [RelayCommand]
    private void Cancel() => this.answer.TrySetResult(false);
}
