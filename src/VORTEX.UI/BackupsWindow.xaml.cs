using System.Windows;
using VORTEX.Core;
using System.IO;

namespace VORTEX.UI;

public partial class BackupsWindow : Window
{
    private readonly IWorkspaceService _workspaceService;
    private readonly List<string> _backupPaths;

    public BackupsWindow(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        InitializeComponent();
        _backupPaths = workspaceService.GetBackups().ToList();
        BackupsList.ItemsSource = _backupPaths.Select(path =>
            $"{Path.GetFileName(path)}  •  {path}").ToList();
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (BackupsList.SelectedIndex < 0)
        {
            MessageBox.Show(this, "Selecione um backup.", "VORTEX",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            await _workspaceService.RestoreBackupAsync(_backupPaths[BackupsList.SelectedIndex]);
            MessageBox.Show(this, "Workspace restaurada.", "VORTEX",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Falha na restauração",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
