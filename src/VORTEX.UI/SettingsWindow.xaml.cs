using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace VORTEX.UI
{
    public partial class SettingsWindow
    {
        public SettingsWindow()
        {
            InitializeComponent();
            SettingsFrame.Navigate(App.ServiceProvider.GetRequiredService<SettingsIAPage>());
        }
    }
}
