using System.IO;
using System.Windows;
using Microsoft.Win32;
using VORTEX.Core;

namespace VORTEX.UI;

public partial class LibraryItemDialog : Window
{
    private bool _editing;
    public LibraryItemDraft Draft { get; private set; } = new();

    public LibraryItemDialog(ILibraryService library)
    {
        InitializeComponent();
        CategoryBox.ItemsSource = library.Categories;
        CategoryBox.SelectedItem = "Outros";
    }

    public void SetSource(string path)
    {
        SourcePathBox.Text = path;
        NameBox.Text = Directory.Exists(path)
            ? new DirectoryInfo(path).Name
            : Path.GetFileNameWithoutExtension(path);
        TypeBox.Text = Directory.Exists(path) ? "Projeto/Pasta" : Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
    }

    public void SetSuggestedMetadata(string name, string description, string category, string type)
    {
        NameBox.Text = name;
        DescriptionBox.Text = description;
        CategoryBox.SelectedItem = category;
        TypeBox.Text = type;
        TagsBox.Text = "vortex, chat, reutilizável";
        WhenUseBox.Text = "Quando um pedido futuro precisar deste conteúdo ou solução.";
    }

    public void LoadItem(LibraryItem item)
    {
        _editing = true;
        Title = "Editar item da Vortex Library";
        SourcePathBox.Text = item.FilePath;
        SourcePathBox.IsEnabled = false;
        NameBox.Text = item.Name; DescriptionBox.Text = item.Description;
        CategoryBox.SelectedItem = item.Category; SubcategoryBox.Text = item.Subcategory;
        TagsBox.Text = string.Join(", ", item.Tags); TypeBox.Text = item.Type;
        FrameworkBox.Text = item.Framework; PlatformBox.Text = item.Platform;
        DependenciesBox.Text = string.Join(", ", item.Dependencies);
        VersionBox.Text = item.Version; AuthorBox.Text = item.Author;
        WhenUseBox.Text = item.WhenToUse; WhenAvoidBox.Text = item.WhenToAvoid;
        NotesBox.Text = string.Join(Environment.NewLine,
            new[] { item.Compatibility, item.Examples, item.License, item.Notes }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public void ApplyTo(LibraryItem item)
    {
        var draft = Draft;
        item.Name = draft.Name; item.Description = draft.Description;
        item.Category = draft.Category; item.Subcategory = draft.Subcategory;
        item.Tags = Split(draft.Tags); item.Type = draft.Type;
        item.Framework = draft.Framework; item.Platform = draft.Platform;
        item.Dependencies = Split(draft.Dependencies); item.Version = draft.Version;
        item.Author = draft.Author; item.WhenToUse = draft.WhenToUse;
        item.WhenToAvoid = draft.WhenToAvoid; item.Notes = draft.Notes;
    }

    private void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        if (_editing) return;
        var picker = new OpenFileDialog { Title = "Selecione um recurso para a Vortex Library", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) SetSource(picker.FileName);
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_editing) return;
        var picker = new OpenFolderDialog { Title = "Selecione uma pasta ou projeto para a Vortex Library", Multiselect = false };
        if (picker.ShowDialog(this) == true) SetSource(picker.FolderName);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_editing && string.IsNullOrWhiteSpace(SourcePathBox.Text))
        {
            MessageBox.Show(this, "Selecione um arquivo ou uma pasta.", "Vortex Library", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "Informe o nome do recurso.", "Vortex Library", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Draft = new LibraryItemDraft
        {
            SourcePath = SourcePathBox.Text.Trim(), Name = NameBox.Text.Trim(),
            Description = DescriptionBox.Text.Trim(), Category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? "Outros" : CategoryBox.Text.Trim(),
            Subcategory = SubcategoryBox.Text.Trim(), Tags = TagsBox.Text.Trim(), Type = TypeBox.Text.Trim(),
            Framework = FrameworkBox.Text.Trim(), Platform = PlatformBox.Text.Trim(),
            Dependencies = DependenciesBox.Text.Trim(), Version = VersionBox.Text.Trim(), Author = AuthorBox.Text.Trim(),
            WhenToUse = WhenUseBox.Text.Trim(), WhenToAvoid = WhenAvoidBox.Text.Trim(), Notes = NotesBox.Text.Trim()
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static List<string> Split(string value) => value.Split([',', ';'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
