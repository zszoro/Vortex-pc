using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VORTEX.Core;

namespace VORTEX.UI;

public partial class SettingsWindow : Window
{
    private readonly IUpdateService _updateService;
    private AppUpdateInfo? _updateInfo;

    public SettingsWindow(IUpdateService updateService)
    {
        _updateService = updateService;
        InitializeComponent();
        SettingsFrame.Navigate(App.ServiceProvider.GetRequiredService<SettingsIAPage>());
        Loaded += async (_, _) => await CheckUpdatesAsync();
    }

    private async Task CheckUpdatesAsync()
    {
        UpdateTitle.Text = "Verificando atualizações...";
        _updateInfo = await _updateService.CheckAsync();
        UpdateTitle.Text = _updateInfo.IsUpdateAvailable
            ? $"Atualização VORTEX {_updateInfo.LatestVersion} disponível"
            : $"VORTEX {_updateInfo.CurrentVersion} está atualizado";
        UpdateVersion.Text =
            $"Instalada: {_updateInfo.CurrentVersion}  •  Mais recente: {_updateInfo.LatestVersion}";
        UpdateNotes.Text = _updateInfo.Notes;
        UpdateButton.Content = _updateInfo.IsUpdateAvailable
            ? "Atualizar VORTEX"
            : "Abrir página de versões";
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) =>
        await CheckUpdatesAsync();

    private void Update_Click(object sender, RoutedEventArgs e) =>
        _updateService.OpenDownloadPage(_updateInfo?.DownloadUrl);

    private void TopmostChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var companion = App.ServiceProvider.GetService<CompanionWindow>();
        if (companion != null) companion.Topmost = TopmostCheckBox.IsChecked == true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
