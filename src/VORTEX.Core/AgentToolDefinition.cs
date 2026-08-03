namespace VORTEX.Core;

public sealed record AgentToolDefinition(
    string Id,
    string Name,
    string Description,
    string LibraryCategory,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> TriggerTerms);

public interface IAgentToolRegistry
{
    IReadOnlyList<AgentToolDefinition> Tools { get; }
    AgentToolDefinition SelectFor(string request);
}

public interface IProjectComposer
{
    Task<string> BuildContextAsync(
        string request, CancellationToken cancellationToken = default);
}
