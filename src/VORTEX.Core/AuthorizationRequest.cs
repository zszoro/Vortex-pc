namespace VORTEX.Core;

public sealed record AuthorizationRequest(
    string Category,
    string Title,
    string Description,
    IReadOnlyList<string> Targets,
    bool IsHighImpact = false);
