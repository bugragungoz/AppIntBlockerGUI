using System.Windows;
using AppIntBlocker.Core;
using AppIntBlockerGUI.ViewModels;
using Microsoft.Win32;

namespace AppIntBlockerGUI;

public partial class App : Application
{
    /// <summary>True when Windows is set to dark apps, or when the setting cannot be read: Bugra is dark first.</summary>
    public static bool IsDark { get; private set; } = true;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IsDark = ReadAppsUseDarkTheme();
        this.Resources.MergedDictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri(IsDark ? "Theme/Colors.Dark.xaml" : "Theme/Colors.Light.xaml", UriKind.Relative),
        });

        var log = new ActivityLog(AppPaths.LogFile);
        var firewall = new FirewallService(PowerShellRunner.WindowsPowerShell());
        var window = new MainWindow { DataContext = new MainViewModel(firewall, log) };
        this.MainWindow = window;
        window.Show();
    }

    private static bool ReadAppsUseDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int light || light == 0;
    }
}
