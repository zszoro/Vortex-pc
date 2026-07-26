using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VORTEX.Core;
using VORTEX.ViewModels;

namespace VORTEX.UI;

public partial class MainWindow
{
    private readonly IUpdateService _updateService;
    private AppUpdateInfo? _updateInfo;

    public MainWindow(MainViewModel viewModel, IUpdateService updateService)
    {
        DataContext = viewModel;
        _updateService = updateService;
        InitializeComponent();
        Loaded += async (_, _) => await CheckForUpdatesAsync();
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
}
