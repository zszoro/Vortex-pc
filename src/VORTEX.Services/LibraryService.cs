using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VORTEX.Core;

namespace VORTEX.Services;

public sealed class LibraryService : ILibraryService
{
    private static readonly string[] InitialCategories =
    [
        "Aplicativos", "Sites", "Bots", "Desktop", "Mobile", "Jogos", "Business",
        "Componentes UI", "Templates", "Layouts", "Dashboards", "Temas", "Assets 3D",
        "Ícones", "Imagens", "Vídeos", "Áudios", "Fontes", "Banco de Dados", "APIs",
        "Automações", "Prompts", "Agentes", "Documentação", "Scripts", "Utilitários", "Outros"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _indexPath;
    private readonly string _categoriesPath;
    private readonly List<string> _categories = [.. InitialCategories];
    private List<LibraryItem> _items = [];
    private bool _initialized;

    public LibraryService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VORTEX", "Library"))
    {
    }

    public LibraryService(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        _indexPath = Path.Combine(RootPath, "library-index.json");
        _categoriesPath = Path.Combine(RootPath, "library-categories.json");
    }

    public string RootPath { get; }
    public IReadOnlyList<string> Categories => _categories;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(RootPath);
            if (File.Exists(_categoriesPath))
            {
                var savedCategories = JsonSerializer.Deserialize<List<string>>(
                    await File.ReadAllTextAsync(_categoriesPath, cancellationToken), JsonOptions) ?? [];
                foreach (var category in savedCategories.Where(category =>
                             !_categories.Contains(category, StringComparer.OrdinalIgnoreCase)))
                    _categories.Add(category);
            }
            foreach (var category in _categories)
                Directory.CreateDirectory(Path.Combine(RootPath, SafeName(category)));

            if (File.Exists(_indexPath))
            {
                var json = await File.ReadAllTextAsync(_indexPath, cancellationToken);
                _items = JsonSerializer.Deserialize<List<LibraryItem>>(json, JsonOptions) ?? [];
            }
            else
            {
                await File.WriteAllTextAsync(_indexPath, "[]", cancellationToken);
            }
            if (!File.Exists(_categoriesPath))
                await File.WriteAllTextAsync(_categoriesPath,
                    JsonSerializer.Serialize(_categories, JsonOptions), cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddCategoryAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var category = name.Trim();
        if (string.IsNullOrWhiteSpace(category))
            throw new InvalidOperationException("Informe o nome da categoria.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_categories.Contains(category, StringComparer.OrdinalIgnoreCase)) return;
            _categories.Add(category);
            Directory.CreateDirectory(Path.Combine(RootPath, SafeName(category)));
            await File.WriteAllTextAsync(_categoriesPath,
                JsonSerializer.Serialize(_categories, JsonOptions), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<LibraryItem>> SearchAsync(
        LibrarySearchOptions options, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var terms = SplitTerms(options.Query);
        IEnumerable<(LibraryItem Item, int Score)> query = _items.Select(item =>
            (item, Score(item, terms)));

        if (terms.Count > 0) query = query.Where(entry => entry.Score > 0);
        if (!string.IsNullOrWhiteSpace(options.Category))
            query = query.Where(entry => EqualsText(entry.Item.Category, options.Category));
        if (!string.IsNullOrWhiteSpace(options.Framework))
            query = query.Where(entry => Contains(entry.Item.Framework, options.Framework));
        if (!string.IsNullOrWhiteSpace(options.Platform))
            query = query.Where(entry => Contains(entry.Item.Platform, options.Platform));
        if (!string.IsNullOrWhiteSpace(options.Type))
            query = query.Where(entry => Contains(entry.Item.Type, options.Type));
        if (options.FavoritesOnly) query = query.Where(entry => entry.Item.IsFavorite);

        query = options.SortBy switch
        {
            "Nome" => query.OrderBy(entry => entry.Item.Name),
            "Mais recentes" => query.OrderByDescending(entry => entry.Item.CreatedAt),
            "Usados recentemente" => query.OrderByDescending(entry => entry.Item.LastUsedAt),
            _ => query.OrderByDescending(entry => entry.Score)
                .ThenByDescending(entry => entry.Item.LastUsedAt)
                .ThenBy(entry => entry.Item.Name)
        };
        return query.Take(Math.Clamp(options.Limit, 1, 500)).Select(entry => entry.Item).ToList();
    }

    public async Task<LibraryItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _items.FirstOrDefault(item => item.Id == id);
    }

    public Task<LibraryItem> ImportAsync(
        LibraryItemDraft draft, CancellationToken cancellationToken = default) =>
        ImportInternalAsync(draft, false, cancellationToken);

    private async Task<LibraryItem> ImportInternalAsync(
        LibraryItemDraft draft, bool allowLibrarySource, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        var source = Path.GetFullPath(draft.SourcePath);
        var isFolder = Directory.Exists(source);
        if (!isFolder && !File.Exists(source))
            throw new FileNotFoundException("O arquivo ou a pasta selecionada não existe.", source);
        if (!allowLibrarySource && IsInsideLibrary(source))
            throw new InvalidOperationException("Escolha um recurso fora da Biblioteca para importá-lo.");
        if (string.IsNullOrWhiteSpace(draft.Name))
            throw new InvalidOperationException("Informe um nome para o item.");

        if (!_categories.Contains(draft.Category, StringComparer.OrdinalIgnoreCase))
            await AddCategoryAsync(draft.Category, cancellationToken);

        var item = FromDraft(draft, isFolder);
        var categoryDirectory = Path.Combine(RootPath, SafeName(item.Category));
        var itemDirectory = Path.Combine(categoryDirectory, $"{SafeName(item.Name)}-{item.Id:N}"[..(SafeName(item.Name).Length + 9)]);
        Directory.CreateDirectory(itemDirectory);
        var destination = Path.Combine(itemDirectory, Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)));
        try
        {
            if (isFolder) CopyDirectory(source, destination, cancellationToken);
            else File.Copy(source, destination, false);
            item.FilePath = Path.GetRelativePath(RootPath, destination);
            item.Hash = await ComputeHashAsync(destination, isFolder, cancellationToken);
            item.ThumbnailPath = IsPreviewableImage(destination) ? item.FilePath : string.Empty;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                _items.Add(item);
                await SaveIndexUnsafeAsync(cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
            return item;
        }
        catch
        {
            if (Directory.Exists(itemDirectory)) Directory.Delete(itemDirectory, true);
            throw;
        }
    }

    public async Task UpdateAsync(LibraryItem item, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = _items.FindIndex(candidate => candidate.Id == item.Id);
            if (index < 0) throw new KeyNotFoundException("Item não encontrado na Biblioteca.");
            item.UpdatedAt = DateTime.UtcNow;
            _items[index] = item;
            await SaveIndexUnsafeAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibraryItem> DuplicateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var source = await GetAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Item não encontrado na Biblioteca.");
        return await ImportInternalAsync(new LibraryItemDraft
        {
            SourcePath = ResolvePath(source.FilePath),
            Name = source.Name + " (cópia)", Description = source.Description,
            Category = source.Category, Subcategory = source.Subcategory,
            Tags = string.Join(", ", source.Tags), Type = source.Type,
            Framework = source.Framework, Platform = source.Platform,
            Dependencies = string.Join(", ", source.Dependencies),
            Compatibility = source.Compatibility, Version = source.Version,
            Author = source.Author, WhenToUse = source.WhenToUse,
            WhenToAvoid = source.WhenToAvoid, Notes = source.Notes,
            Examples = source.Examples, License = source.License
        }, true, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id == id)
                ?? throw new KeyNotFoundException("Item não encontrado na Biblioteca.");
            var storedPath = ResolvePath(item.FilePath);
            var itemDirectory = Directory.GetParent(storedPath)?.FullName;
            if (itemDirectory != null && IsInsideLibrary(itemDirectory) && !EqualsText(itemDirectory, RootPath))
                Directory.Delete(itemDirectory, true);
            _items.Remove(item);
            await SaveIndexUnsafeAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, item => item.IsFavorite = !item.IsFavorite, cancellationToken);

    public Task MarkUsedAsync(Guid id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, item => item.LastUsedAt = DateTime.UtcNow, cancellationToken);

    private async Task MutateAsync(Guid id, Action<LibraryItem> action, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id == id)
                ?? throw new KeyNotFoundException("Item não encontrado na Biblioteca.");
            action(item);
            item.UpdatedAt = DateTime.UtcNow;
            await SaveIndexUnsafeAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized) await InitializeAsync(cancellationToken);
    }

    private async Task SaveIndexUnsafeAsync(CancellationToken cancellationToken)
    {
        var temporary = _indexPath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(_items, JsonOptions), cancellationToken);
        File.Move(temporary, _indexPath, true);
    }

    private static LibraryItem FromDraft(LibraryItemDraft draft, bool isFolder) => new()
    {
        Name = draft.Name.Trim(), Description = draft.Description.Trim(),
        Category = string.IsNullOrWhiteSpace(draft.Category) ? "Outros" : draft.Category.Trim(),
        Subcategory = draft.Subcategory.Trim(), Tags = SplitList(draft.Tags),
        Type = draft.Type.Trim(), Framework = draft.Framework.Trim(), Platform = draft.Platform.Trim(),
        Dependencies = SplitList(draft.Dependencies), Compatibility = draft.Compatibility.Trim(),
        Version = string.IsNullOrWhiteSpace(draft.Version) ? "1.0.0" : draft.Version.Trim(),
        Author = draft.Author.Trim(), WhenToUse = draft.WhenToUse.Trim(),
        WhenToAvoid = draft.WhenToAvoid.Trim(), Notes = draft.Notes.Trim(),
        Examples = draft.Examples.Trim(), License = draft.License.Trim(), IsFolder = isFolder
    };

    private static List<string> SplitList(string value) => value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static List<string> SplitTerms(string value) => value
        .Split([' ', ',', ';', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(term => term.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static int Score(LibraryItem item, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0) return 1;
        var score = 0;
        foreach (var term in terms)
        {
            if (Contains(item.Name, term)) score += 12;
            if (Contains(item.Description, term)) score += 8;
            if (item.Tags.Any(tag => Contains(tag, term))) score += 10;
            if (Contains(item.Category, term) || Contains(item.Subcategory, term)) score += 7;
            if (Contains(item.Framework, term) || Contains(item.Platform, term) || Contains(item.Type, term)) score += 6;
            if (Contains(item.WhenToUse, term) || Contains(item.Examples, term)) score += 4;
        }
        if (item.IsFavorite) score += 2;
        return score;
    }

    private static bool Contains(string value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    private static bool EqualsText(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private string ResolvePath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        if (!IsInsideLibrary(path)) throw new InvalidOperationException("Caminho inválido no índice da Biblioteca.");
        return path;
    }

    private bool IsInsideLibrary(string path)
    {
        var root = RootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(path);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || EqualsText(full, RootPath);
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Outros" : cleaned;
    }

    private static void CopyDirectory(string source, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new DirectoryInfo(directory);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            CopyDirectory(directory, Path.Combine(destination, info.Name), cancellationToken);
        }
    }

    private static async Task<string> ComputeHashAsync(string path, bool isFolder, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IEnumerable<string> files = isFolder
            ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).OrderBy(file => file)
            : new[] { path };
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(path, file)));
            await using var stream = File.OpenRead(file);
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                hash.AppendData(buffer.AsSpan(0, read));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static bool IsPreviewableImage(string path) => !Directory.Exists(path) &&
        new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" }
            .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
