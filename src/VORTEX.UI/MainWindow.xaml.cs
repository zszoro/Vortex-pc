using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VORTEX.Core;
using VORTEX.ViewModels;
using System.Diagnostics;
using System.Windows.Threading;
using System.IO;

namespace VORTEX.UI;

public partial class MainWindow
{
    private readonly IUpdateService _updateService;
    private AppUpdateInfo? _updateInfo;
    private readonly DispatcherTimer _updateTimer;

    public MainWindow(MainViewModel viewModel, IUpdateService updateService)
    {
        DataContext = viewModel;
        _updateService = updateService;
        InitializeComponent();
        Loaded += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer.Start();
        Closed += (_, _) => _updateTimer.Stop();
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
        settings.Owner = this;
        settings.ShowDialog();
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

    private void Summarize_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.UserInput = "Resuma nossa conversa e liste as próximas ações.";
        ChatInput.Focus();
    }

    private void Files_Click(object sender, RoutedEventArgs e)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Process.Start(new ProcessStartInfo(downloads) { UseShellExecute = true });
    }
}
