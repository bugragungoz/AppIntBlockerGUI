using System.Windows.Controls;
using AppIntBlockerGUI.ViewModels;

namespace AppIntBlockerGUI.Views
{
    /// <summary>
    /// Interaction logic for ManageRulesView.xaml
    /// </summary>
    public partial class ManageRulesView : UserControl
    {
        public ManageRulesView()
        {
            InitializeComponent();
            
            // Auto refresh when the view becomes visible
            this.IsVisibleChanged += OnVisibilityChanged;
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