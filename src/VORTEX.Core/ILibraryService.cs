namespace VORTEX.Core;

public interface ILibraryService
{
    string RootPath { get; }
    IReadOnlyList<string> Categories { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task AddCategoryAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LibraryItem>> SearchAsync(
        LibrarySearchOptions options, CancellationToken cancellationToken = default);
    Task<LibraryItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LibraryItem> ImportAsync(
        LibraryItemDraft draft, CancellationToken cancellationToken = default);
    Task UpdateAsync(LibraryItem item, CancellationToken cancellationToken = default);
    Task<LibraryItem> DuplicateAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkUsedAsync(Guid id, CancellationToken cancellationToken = default);
}
