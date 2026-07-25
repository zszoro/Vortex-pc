using System.Windows.Controls;
using VORTEX.ViewModels;

namespace VORTEX.UI
{
    public partial class SettingsIAPage : Page
    {
        public SettingsIAPage(SetupViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
