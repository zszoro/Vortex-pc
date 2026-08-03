namespace VORTEX.Core;

public sealed class LibraryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Outros";
    public string Subcategory { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Type { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = [];
    public string Compatibility { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public string Author { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public string WhenToUse { get; set; } = string.Empty;
    public string WhenToAvoid { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Examples { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public bool IsFolder { get; set; }
}

public sealed class LibraryItemDraft
{
    public string SourcePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Outros";
    public string Subcategory { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Dependencies { get; set; } = string.Empty;
    public string Compatibility { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public string WhenToUse { get; set; } = string.Empty;
    public string WhenToAvoid { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Examples { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
}

public sealed class LibrarySearchOptions
{
    public string Query { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool FavoritesOnly { get; set; }
    public string SortBy { get; set; } = "Relevância";
    public int Limit { get; set; } = 100;
}
