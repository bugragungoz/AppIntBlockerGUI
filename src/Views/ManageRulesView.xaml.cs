// <copyright file="ManageRulesView.xaml.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace AppIntBlockerGUI.Views
{
    using System.Windows.Controls;
    using AppIntBlockerGUI.ViewModels;

    /// <summary>
    /// Interaction logic for ManageRulesView.xaml
    /// </summary>
    public partial class ManageRulesView : UserControl
    {
        public ManageRulesView()
        {
            this.InitializeComponent();

            // Auto refresh when the view becomes visible
            this.IsVisibleChanged += this.OnVisibilityChanged;
        }

        private void OnVisibilityChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            // Simple visibility check - no automatic refresh to prevent crashes
            if (this.IsVisible && this.DataContext is ManageRulesViewModel viewModel)
            {
                // User can manually click REFRESH RULES button when ready
                System.Diagnostics.Debug.WriteLine("ManageRulesView is now visible - ready for manual refresh");
            }
        }
    }
}
