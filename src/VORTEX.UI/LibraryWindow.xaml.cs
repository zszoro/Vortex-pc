using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VORTEX.Core;
using VORTEX.ViewModels;

namespace VORTEX.UI;

public partial class LibraryWindow : Window, INotifyPropertyChanged
{
    private readonly ILibraryService _library;
    private LibraryItem? _selectedItem;
    public ObservableCollection<LibraryItem> Items { get; } = [];

    public LibraryItem? SelectedItem
    {
        get => _selectedItem;
        private set { _selectedItem = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LibraryWindow(ILibraryService library)
    {
        _library = library;
        InitializeComponent();
        DataContext = this;
        CategoryList.Items.Add("Todas");
        foreach (var category in library.Categories) CategoryList.Items.Add(category);
        CategoryList.SelectedIndex = 0;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var results = await _library.SearchAsync(new LibrarySearchOptions
        {
            Query = SearchBox.Text,
            Category = CategoryList.SelectedIndex > 0 ? CategoryList.SelectedItem?.ToString() ?? "" : "",
            Framework = FilterValue(FrameworkFilter, "Todos"),
            Platform = FilterValue(PlatformFilter, "Todas"),
            Type = FilterValue(TypeFilter, "Todos"),
            FavoritesOnly = FavoritesOnly.IsChecked == true,
            SortBy = (SortFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Relevância"
        });
        Items.Clear();
        foreach (var item in results) Items.Add(item);
        GridItems.ItemsSource = Items;
        ListItems.ItemsSource = Items;
        EmptyState.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = $"{Items.Count} item(ns) • {_library.RootPath}";
    }

    private async void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox(
            "Nome da nova categoria:", "Vortex Library", "Nova categoria").Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        await _library.AddCategoryAsync(name);
        CategoryList.Items.Add(name);
        CategoryList.SelectedItem = name;
    }

    private static string FilterValue(ComboBox combo, string allPrefix)
    {
        var value = (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        return value.StartsWith(allPrefix, StringComparison.OrdinalIgnoreCase) ? "" : value;
    }

    private async void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        await RefreshAsync();
    }

    private async void CategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await RefreshAsync();
    }

    private void ItemSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is LibraryItem item)
        {
            SelectedItem = item;
            if (!ReferenceEquals(list, GridItems)) GridItems.SelectedItem = null;
            if (!ReferenceEquals(list, ListItems)) ListItems.SelectedItem = null;
        }
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e) => await OpenImportDialogAsync();

    public Task BeginAddFlowAsync() => OpenImportDialogAsync();

    private async Task OpenImportDialogAsync(string? sourcePath = null)
    {
        var dialog = new LibraryItemDialog(_library) { Owner = Application.Current.MainWindow };
        if (!string.IsNullOrWhiteSpace(sourcePath)) dialog.SetSource(sourcePath);
        if (dialog.ShowDialog() != true) return;
        try
        {
            var item = await _library.ImportAsync(dialog.Draft);
            StatusText.Text = $"{item.Name} foi adicionado à Biblioteca.";
            await RefreshAsync();
            SelectedItem = Items.FirstOrDefault(candidate => candidate.Id == item.Id);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Falha ao adicionar item", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null) return;
        await _library.ToggleFavoriteAsync(SelectedItem.Id);
        await RefreshAsync();
    }

    private async void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null) return;
        await _library.DuplicateAsync(SelectedItem.Id);
        await RefreshAsync();
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null) return;
        var dialog = new LibraryItemDialog(_library) { Owner = Application.Current.MainWindow };
        dialog.LoadItem(SelectedItem);
        if (dialog.ShowDialog() != true) return;
        dialog.ApplyTo(SelectedItem);
        await _library.UpdateAsync(SelectedItem);
        await RefreshAsync();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null) return;
        if (MessageBox.Show($"Excluir '{SelectedItem.Name}' e sua cópia armazenada na Biblioteca?",
                "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        await _library.DeleteAsync(SelectedItem.Id);
        SelectedItem = null;
        await RefreshAsync();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null) return;
        var path = Path.Combine(_library.RootPath, SelectedItem.FilePath);
        var target = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private async void UseInProject_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null) return;
        await _library.MarkUsedAsync(SelectedItem.Id);
        var viewModel = App.ServiceProvider.GetService(typeof(MainViewModel)) as MainViewModel;
        if (viewModel != null)
            viewModel.UserInput = $"Use o recurso da Vortex Library '{SelectedItem.Name}' no projeto atual. Categoria: {SelectedItem.Category}. Caminho na Library: {SelectedItem.FilePath}.";
        StatusText.Text = $"{SelectedItem.Name} preparado no chat para uso no projeto.";
    }

    private void GridView_Click(object sender, RoutedEventArgs e)
    {
        GridItems.Visibility = Visibility.Visible;
        ListItems.Visibility = Visibility.Collapsed;
    }

    private void ListView_Click(object sender, RoutedEventArgs e)
    {
        GridItems.Visibility = Visibility.Collapsed;
        ListItems.Visibility = Visibility.Visible;
    }

    private void Window_DragOver(object sender, DragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            await OpenImportDialogAsync(paths[0]);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
