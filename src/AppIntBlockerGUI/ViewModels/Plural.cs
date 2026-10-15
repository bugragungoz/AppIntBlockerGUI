namespace AppIntBlockerGUI.ViewModels;

internal static class Plural
{
    /// <summary>"1 rule", "24 rules".</summary>
    public static string Of(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
}
