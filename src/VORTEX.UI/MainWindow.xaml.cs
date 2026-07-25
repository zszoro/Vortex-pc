using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VORTEX.ViewModels;

namespace VORTEX.UI
{
    public partial class MainWindow
    {
        public MainWindow(MainViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settings = App.ServiceProvider.GetRequiredService<SettingsWindow>();
            settings.Owner = this;
            settings.ShowDialog();
        }
    }
}
