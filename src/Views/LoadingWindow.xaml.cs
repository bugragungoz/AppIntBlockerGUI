// <copyright file="LoadingWindow.xaml.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Views
{
    using System;
    using System.Windows;
    using System.Windows.Threading;

    public partial class LoadingWindow : Window
    {
        private readonly DispatcherTimer animationTimer;
        private int dotCount = 0;
        private string baseStatusText = "Initializing";

        public LoadingWindow()
        {
            this.InitializeComponent();

            this.animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            this.animationTimer.Tick += this.AnimationTimer_Tick;
            this.animationTimer.Start();
            this.UpdateStatusText();
        }

        public void UpdateStatus(string newStatus)
        {
            this.baseStatusText = newStatus;
            this.dotCount = 0; // Reset dots when status changes
            this.UpdateStatusText();
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            this.dotCount = (this.dotCount + 1) % 4;
            this.UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            this.StatusText.Text = $"{this.baseStatusText}{new string('.', this.dotCount)}";
        }
    }
}
