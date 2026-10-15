using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AppIntBlockerGUI;

public partial class MainWindow : Window
{
    private IInputElement? focusBeforeDialog;

    public MainWindow()
    {
        this.InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (App.IsDark)
        {
            UseDarkTitleBar(new WindowInteropHelper(this).Handle);
        }
    }

    // The dialog takes the focus when it opens (on Cancel, the safe answer) and gives it back when it closes.
    private void OnDialogVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (this.DialogLayer.IsVisible)
        {
            this.focusBeforeDialog = Keyboard.FocusedElement;
            this.Dispatcher.BeginInvoke(() => this.CancelButton.Focus());
        }
        else if (this.focusBeforeDialog is UIElement { IsVisible: true, IsEnabled: true } element)
        {
            this.Dispatcher.BeginInvoke(() => element.Focus());
        }
    }

    // Windows draws the title bar; ask for its dark version so it matches the dark theme.
    private static void UseDarkTitleBar(IntPtr window)
    {
        const int DwmUseImmersiveDarkMode = 20;
        const int DwmUseImmersiveDarkModeBefore20H1 = 19;
        var on = 1;
        if (DwmSetWindowAttribute(window, DwmUseImmersiveDarkMode, ref on, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(window, DwmUseImmersiveDarkModeBefore20H1, ref on, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
