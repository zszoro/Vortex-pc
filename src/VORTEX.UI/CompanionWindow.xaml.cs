using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using VORTEX.ViewModels;

namespace VORTEX.UI
{
    public partial class CompanionWindow : Window
    {
        public CompanionWindow(CompanionViewModel viewModel)
        {
            DataContext = viewModel;
            
            viewModel.OnOpenMain += () =>
            {
                Application.Current.MainWindow.Visibility = Visibility.Visible;
                Application.Current.MainWindow.Activate();
            };

            viewModel.OnOpenChat += () =>
            {
                var quickChat = App.ServiceProvider.GetRequiredService<QuickChatWindow>();
                quickChat.Show();
            };

            InitializeComponent();
            
            // Posicionar no canto inferior direito
            Left = SystemParameters.WorkArea.Width - Width - 20;
            Top = SystemParameters.WorkArea.Height - Height - 20;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Application.Current.MainWindow.Visibility = Visibility.Visible;
                Application.Current.MainWindow.Activate();
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
