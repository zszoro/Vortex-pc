using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VORTEX.Core;
using VORTEX.ViewModels;
using System.Diagnostics;
using System.Windows.Threading;
using System.IO;
using Microsoft.Win32;

namespace VORTEX.UI;

public partial class MainWindow
{
    private readonly IUpdateService _updateService;
    private AppUpdateInfo? _updateInfo;
    private readonly DispatcherTimer _updateTimer;
    private Window? _embeddedWindow;

    public MainWindow(MainViewModel viewModel, IUpdateService updateService)
    {
        DataContext = viewModel;
        _updateService = updateService;
        InitializeComponent();
        viewModel.SpotifyPanelRequested += OpenSpotifyPanel;
        viewModel.PlanningPanelRequested += OpenPlanningPanel;
        Loaded += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer.Start();
        Closed += (_, _) => _updateTimer.Stop();
        Closed += (_, _) =>
        {
            viewModel.SpotifyPanelRequested -= OpenSpotifyPanel;
            viewModel.PlanningPanelRequested -= OpenPlanningPanel;
        };
    }

    private async Task CheckForUpdatesAsync()
    {
        _updateInfo = await _updateService.CheckAsync();
        if (!_updateInfo.IsUpdateAvailable) return;
        UpdateBannerTitle.Text = $"Atualização VORTEX {_updateInfo.LatestVersion} disponível";
        UpdateBannerNotes.Text = _updateInfo.Notes;
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.ServiceProvider.GetRequiredService<SettingsWindow>();
        ShowOverlay(settings);
    }

    private void DismissUpdate_Click(object sender, RoutedEventArgs e) =>
        UpdateBanner.Visibility = Visibility.Collapsed;

    private void OpenUpdate_Click(object sender, RoutedEventArgs e) =>
        _updateService.OpenDownloadPage(_updateInfo?.DownloadUrl);

    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string content } && !string.IsNullOrWhiteSpace(content))
            Clipboard.SetText(content);
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void FocusChat_Click(object sender, RoutedEventArgs e) => ChatInput.Focus();

    private void Terminal_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "/terminal ";
        ChatInput.Focus();
        ChatInput.CaretIndex = ChatInput.Text.Length;
    }

    private void Memory_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "Lembre e organize os pontos mais importantes desta conversa: ";
        ChatInput.Focus();
    }

    private void Automations_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "Crie uma automação para: ";
        ChatInput.Focus();
    }

    private void Spotify_Click(object sender, RoutedEventArgs e) => OpenSpotifyPanel();
    private void Planning_Click(object sender, RoutedEventArgs e) => OpenPlanningPanel();

    private void OpenSpotifyPanel()
    {
        Dispatcher.Invoke(() =>
        {
            var window = App.ServiceProvider.GetRequiredService<SpotifyWindow>();
            ShowOverlay(window);
        });
    }

    private void OpenPlanningPanel()
    {
        Dispatcher.Invoke(() =>
        {
            var window = App.ServiceProvider.GetRequiredService<PlanningWindow>();
            ShowOverlay(window);
        });
    }

    private void ShowOverlay(Window window)
    {
        CloseCurrentOverlay();
        _embeddedWindow = window;
        var content = window.Content;
        window.Content = null;
        OverlayContent.Content = content;
        OverlayHost.Visibility = Visibility.Visible;
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e)
    {
        CloseCurrentOverlay();
        ViewModel.RefreshSpotifyState();
        OverlayHost.Visibility = Visibility.Collapsed;
    }

    private void CloseCurrentOverlay()
    {
        OverlayContent.Content = null;
        _embeddedWindow?.Close();
        _embeddedWindow = null;
    }

    private void Summarize_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "Resuma nossa conversa e liste as próximas ações.";
        ChatInput.Focus();
    }

    private async void Files_Click(object sender, RoutedEventArgs e)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var authorization = App.ServiceProvider.GetRequiredService<IAuthorizationService>();
        if (!await authorization.RequestAsync(new(
                "Acesso a pastas", "Abrir Downloads",
                "O Explorador de Arquivos será aberto na pasta Downloads.", [downloads])))
            return;
        Process.Start(new ProcessStartInfo(downloads) { UseShellExecute = true });
    }

    private void Backups_Click(object sender, RoutedEventArgs e)
    {
        var workspace = App.ServiceProvider.GetRequiredService<IWorkspaceService>();
        if (workspace.Current == null)
        {
            MessageBox.Show(this, "Abra uma Workspace antes de acessar backups.", "VORTEX",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        new BackupsWindow(workspace) { Owner = this }.ShowDialog();
    }

    private async void NewConversation_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewConversationWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (dialog.Choice == "blank")
            {
                await ViewModel.StartBlankConversationAsync();
                return;
            }
            if (dialog.Choice == "open")
            {
                var folderDialog = new OpenFolderDialog
                {
                    Title = "Selecione a pasta do projeto",
                    Multiselect = false
                };
                if (folderDialog.ShowDialog(this) == true)
                    await ViewModel.OpenWorkspaceAsync(folderDialog.FolderName);
                return;
            }
            if (dialog.Choice == "create")
                await ViewModel.CreateWorkspaceAsync(dialog.ProjectName);
        }
        catch (OperationCanceledException)
        {
            // O modal de autorização já informou a decisão do usuário.
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Não foi possível abrir a Workspace",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
