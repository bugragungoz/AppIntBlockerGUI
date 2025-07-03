using System;
using System.Windows;
using System.Windows.Threading;

namespace AppIntBlockerGUI.Views
{
    public partial class LoadingWindow : Window
    {
        private readonly DispatcherTimer _animationTimer;
        private int _dotCount = 0;
        private string _baseStatusText = "Initializing";

        public LoadingWindow()
        {
            InitializeComponent();
            
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
            UpdateStatusText();
        }

        public void UpdateStatus(string newStatus)
        {
            _baseStatusText = newStatus;
            _dotCount = 0; // Reset dots when status changes
            UpdateStatusText();
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            _dotCount = (_dotCount + 1) % 4;
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            StatusText.Text = $"{_baseStatusText}{new string('.', _dotCount)}";
        }
    }
} 