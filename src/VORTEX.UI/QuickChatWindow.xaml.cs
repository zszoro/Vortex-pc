using System.Windows;
using VORTEX.ViewModels;

namespace VORTEX.UI
{
    public partial class QuickChatWindow : Window
    {
        public QuickChatWindow(MainViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
            
            // Posicionar acima do companion
            Left = SystemParameters.WorkArea.Width - Width - 20;
            Top = SystemParameters.WorkArea.Height - Height - 120;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}
