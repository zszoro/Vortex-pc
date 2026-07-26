using System.Windows;

namespace VORTEX.UI;

public partial class NewConversationWindow : Window
{
    public string Choice { get; private set; } = string.Empty;
    public string ProjectName => ProjectNameInput.Text.Trim();

    public NewConversationWindow() => InitializeComponent();

    private void Blank_Click(object sender, RoutedEventArgs e)
    {
        Choice = "blank";
        DialogResult = true;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        Choice = "open";
        DialogResult = true;
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show(this, "Informe o nome do projeto.", "VORTEX",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Choice = "create";
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
