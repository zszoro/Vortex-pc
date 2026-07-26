namespace VORTEX.Core;

public interface IWorkspaceService
{
    WorkspaceContext? Current { get; }
    WorkspaceChangeProposal? PendingProposal { get; }
    Task<WorkspaceContext> OpenAsync(string rootPath, CancellationToken cancellationToken = default);
    Task<WorkspaceContext> CreateAsync(string projectName, CancellationToken cancellationToken = default);
    Task ClearAsync();
    Task<string?> CreateBackupAsync(IEnumerable<string>? targets = null, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetBackups();
    Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default);
    Task<string> BuildRelevantContextAsync(string query, int maxCharacters = 35000, CancellationToken cancellationToken = default);
    Task<string> ProcessAgentResponseAsync(string response, CancellationToken cancellationToken = default);
    Task<string> ApplyProposalAsync(CancellationToken cancellationToken = default);
    void CancelProposal();
}
