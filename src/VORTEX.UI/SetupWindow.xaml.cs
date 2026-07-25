using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using VORTEX.ViewModels;

namespace VORTEX.UI
{
    public partial class SetupWindow
    {
        public SetupWindow(SetupViewModel viewModel)
        {
            DataContext = viewModel;
            viewModel.OnSetupComplete += () =>
            {
                var main = App.ServiceProvider.GetRequiredService<MainWindow>();
                Application.Current.MainWindow = main;
                main.Show();
                var companion = App.ServiceProvider.GetRequiredService<CompanionWindow>();
                companion.Show();
                this.Close();
            };
            InitializeComponent();
        }
    }
}
