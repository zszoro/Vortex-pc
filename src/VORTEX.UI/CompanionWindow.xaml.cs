using System.Windows;
using System.Windows.Input;
using VORTEX.ViewModels;

namespace VORTEX.UI;

public partial class CompanionWindow : Window
{
    public CompanionWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Left = SystemParameters.WorkArea.Right - Width - 14;
        Top = SystemParameters.WorkArea.Bottom - Height - 14;
    }

    private void Pet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            OpenMain();
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Pet_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ToggleQuickPanel();
    }

    private void ToggleQuickPanel()
    {
        QuickPanel.Visibility = QuickPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (QuickPanel.Visibility == Visibility.Visible)
        {
            PetInput.Focus();
            Keyboard.Focus(PetInput);
        }
    }

    private static void OpenMain()
    {
        var main = Application.Current.MainWindow;
        if (main == null) return;
        main.Show();
        main.WindowState = WindowState.Normal;
        main.Activate();
    }

    private void OpenMain_Click(object sender, RoutedEventArgs e) => OpenMain();
    private void ToggleChat_Click(object sender, RoutedEventArgs e) => ToggleQuickPanel();

    private void Topmost_Click(object sender, RoutedEventArgs e) =>
        Topmost = TopmostMenuItem.IsChecked;

    private void Exit_Click(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();
}
