using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppIntBlockerGUI.ViewModels;

namespace AppIntBlockerGUI.Views;

public partial class BlockView : UserControl
{
    public BlockView()
    {
        this.InitializeComponent();
    }

    // Leaving the folder box lists the files of the folder typed or pasted into it.
    private void OnFolderLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (this.DataContext is BlockViewModel model && model.ScanCommand.CanExecute(null))
        {
            model.ScanCommand.Execute(null);
        }
    }

    private void OnActivityChanged(object sender, TextChangedEventArgs e) => this.ActivityBox.ScrollToEnd();

    // The file list and the log scroll on their own. Once one has nothing more to scroll in the
    // wheel's direction, the page scrolls instead of the wheel stopping there.
    private void OnInnerWheel(object sender, MouseWheelEventArgs e)
    {
        var (offset, scrollable) = sender switch
        {
            ScrollViewer list => (list.VerticalOffset, list.ScrollableHeight),
            TextBox box => (box.VerticalOffset, box.ExtentHeight - box.ViewportHeight),
            _ => (0d, 0d),
        };
        var atEnd = e.Delta > 0 ? offset <= 0 : offset >= scrollable;
        if (atEnd)
        {
            // Same distance as a ScrollViewer's own wheel step: the system's lines per notch, 16px a line.
            e.Handled = true;
            var lines = Math.Max(1, SystemParameters.WheelScrollLines) * (double)e.Delta / Mouse.MouseWheelDeltaForOneLine;
            this.PageScroller.ScrollToVerticalOffset(this.PageScroller.VerticalOffset - (lines * 16));
        }
    }
}
