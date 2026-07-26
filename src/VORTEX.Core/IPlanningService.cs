namespace VORTEX.Core;

public interface IPlanningService
{
    string Content { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string content, CancellationToken cancellationToken = default);
    Task AddObjectiveAsync(string objective, CancellationToken cancellationToken = default);
}
