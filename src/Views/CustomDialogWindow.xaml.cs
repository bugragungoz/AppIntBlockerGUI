// <copyright file="CustomDialogWindow.xaml.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Views
{
    using System.Windows;
    using System.Windows.Input;

    public partial class CustomDialogWindow : Window
    {
        public CustomDialogWindow(string title, string message, string icon = "ℹ️", bool showCancelButton = false)
        {
            this.InitializeComponent();

            this.DataContext = new DialogViewModel
            {
                Title = title,
                Message = message,
                Icon = icon,
                ShowCancelButton = showCancelButton
            };

            if (!showCancelButton)
            {
                this.CancelButton.Visibility = Visibility.Collapsed;
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
        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Icon { get; set; } = "ℹ️";

        public bool ShowCancelButton { get; set; } = false;
    }
}
