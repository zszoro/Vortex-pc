using VORTEX.Core;

namespace VORTEX.Services;

public sealed class PlanningService : IPlanningService
{
    private readonly string _path;
    public string Content { get; private set; } = string.Empty;

    public PlanningService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VORTEX", "planning");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "vortex-planejamento.md");
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
            await SaveAsync(
                "# Planejamento VORTEX\n\n## Objetivos\n\n## Notas\n\n## Concluídos\n",
                cancellationToken);
        else
            Content = await File.ReadAllTextAsync(_path, cancellationToken);
    }

    public async Task SaveAsync(string content, CancellationToken cancellationToken = default)
    {
        Content = content;
        await File.WriteAllTextAsync(_path, content, cancellationToken);
    }

    public async Task AddObjectiveAsync(
        string objective, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Content)) await LoadAsync(cancellationToken);
        var marker = "## Objetivos";
        var index = Content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        Content = index < 0
            ? Content + $"\n\n## Objetivos\n\n- [ ] {objective.Trim()}\n"
            : Content.Insert(index + marker.Length, $"\n\n- [ ] {objective.Trim()}");
        await SaveAsync(Content, cancellationToken);
    }
}
