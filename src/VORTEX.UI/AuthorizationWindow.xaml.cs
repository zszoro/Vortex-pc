using System.Windows;
using VORTEX.Core;

namespace VORTEX.UI;

public partial class AuthorizationWindow : Window
{
    public AuthorizationWindow(AuthorizationRequest request)
    {
        InitializeComponent();
        TitleText.Text = request.Title;
        CategoryText.Text = request.Category;
        DescriptionText.Text = request.Description;
        TargetsList.ItemsSource = request.Targets.Count > 0
            ? request.Targets
            : ["Nenhum caminho específico informado."];
    }

    private void Allow_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Deny_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
