using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VORTEX.Core;
using VORTEX.ViewModels;
using System.Windows.Controls;

namespace VORTEX.UI;

public partial class SettingsWindow : Window
{
    private readonly IUpdateService _updateService;
    private readonly MainViewModel _mainViewModel;
    private readonly UiPreferences _preferences;
    private AppUpdateInfo? _updateInfo;

    public SettingsWindow(IUpdateService updateService, MainViewModel mainViewModel)
    {
        _updateService = updateService;
        _mainViewModel = mainViewModel;
        _preferences = UiPreferences.Load();
        InitializeComponent();
        SelectComboValue(ThemeCombo, _preferences.Theme);
        SelectComboValue(AppearanceCombo, _preferences.PetAppearance);
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

    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ThemeCombo.SelectedItem is not ComboBoxItem item) return;
        _preferences.Theme = item.Content?.ToString() ?? "Vortex";
        UiPreferences.ApplyTheme(_preferences.Theme);
        _preferences.Save();
    }

    private void AppearanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || AppearanceCombo.SelectedItem is not ComboBoxItem item) return;
        _preferences.PetAppearance = item.Content?.ToString() ?? "Orb";
        _mainViewModel.PetAppearance = _preferences.PetAppearance;
        _preferences.Save();
        MessageBox.Show(this,
            $"Skin {_preferences.PetAppearance} aplicada ao VORTEX principal e ao pet flutuante.",
            "Aparência atualizada", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static void SelectComboValue(ComboBox comboBox, string value)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), value, StringComparison.Ordinal))
            ?? comboBox.Items[0];
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
