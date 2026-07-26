namespace VORTEX.Core;

public sealed class WorkspaceChangeProposal
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<WorkspaceFileChange> Changes { get; init; } = [];
    public IReadOnlyList<string> Previews { get; init; } = [];
}
