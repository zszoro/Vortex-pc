using VORTEX.Core;
using VORTEX.Services;
using Xunit;

namespace VORTEX.Services.Tests;

public sealed class LibraryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vortex-library-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeCreatesIndexableCategoryStructure()
    {
        var service = new LibraryService(Path.Combine(_root, "Library"));
        await service.InitializeAsync();
        Assert.True(Directory.Exists(Path.Combine(service.RootPath, "Sites")));
        Assert.True(Directory.Exists(Path.Combine(service.RootPath, "Business")));
        Assert.True(File.Exists(Path.Combine(service.RootPath, "library-index.json")));
        Assert.Contains("Componentes UI", service.Categories);
        await service.AddCategoryAsync("Minha categoria");
        Assert.Contains("Minha categoria", service.Categories);
        Assert.True(Directory.Exists(Path.Combine(service.RootPath, "Minha categoria")));
    }

    [Fact]
    public async Task ImportSearchFavoriteDuplicateAndDeleteRoundTrip()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "card.html");
        await File.WriteAllTextAsync(source, "<article>Padaria</article>");
        var service = new LibraryService(Path.Combine(_root, "Library"));
        var item = await service.ImportAsync(new LibraryItemDraft
        {
            SourcePath = source, Name = "Cardápio de padaria",
            Description = "Componente para mostrar produtos e preços de uma padaria.",
            Category = "Sites", Tags = "padaria, cardápio, produtos",
            Framework = "HTML", Platform = "Web", Type = "Componente UI",
            WhenToUse = "Sites de alimentação"
        });

        var matches = await service.SearchAsync(new LibrarySearchOptions { Query = "site padaria produtos" });
        Assert.Equal(item.Id, Assert.Single(matches).Id);
        Assert.True(File.Exists(Path.Combine(service.RootPath, item.FilePath)));
        Assert.False(string.IsNullOrWhiteSpace(item.Hash));

        await service.ToggleFavoriteAsync(item.Id);
        Assert.True(Assert.Single(await service.SearchAsync(
            new LibrarySearchOptions { FavoritesOnly = true })).IsFavorite);

        var copy = await service.DuplicateAsync(item.Id);
        Assert.NotEqual(item.Id, copy.Id);
        Assert.True(File.Exists(Path.Combine(service.RootPath, copy.FilePath)));
        await service.DeleteAsync(copy.Id);
        Assert.Null(await service.GetAsync(copy.Id));
    }

    [Fact]
    public void RegistrySelectsBusinessSuiteFromIntent()
    {
        var tool = new AgentToolRegistry().SelectFor(
            "Crie um sistema para uma clínica com estoque e financeiro");
        Assert.Equal("business", tool.Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
