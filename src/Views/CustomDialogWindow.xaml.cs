using System.Windows;
using System.Windows.Input;

namespace AppIntBlockerGUI.Views
{
    public partial class CustomDialogWindow : Window
    {
        public CustomDialogWindow(string title, string message, string icon = "ℹ️", bool showCancelButton = false)
        {
            InitializeComponent();
            
            DataContext = new DialogViewModel
            {
                Title = title,
                Message = message,
                Icon = icon,
                ShowCancelButton = showCancelButton
            };

            if (!showCancelButton)
            {
                CancelButton.Visibility = Visibility.Collapsed;
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    public class DialogViewModel
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Icon { get; set; } = "ℹ️";
        public bool ShowCancelButton { get; set; } = false;
    }
} 