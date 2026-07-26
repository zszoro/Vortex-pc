using VORTEX.Services;
using Xunit;
using VORTEX.Core;

namespace VORTEX.Services.Tests;

public sealed class DesktopCommandServiceTests
{
    [Fact]
    public async Task ExecutesRecognizedTerminalCommand()
    {
        var service = CreateService();

        var result = await service.TryExecuteAsync("echo vortex-ok");

        Assert.True(result.Handled);
        Assert.False(result.IsError);
        Assert.Contains("vortex-ok", result.Output);
    }

    [Fact]
    public async Task BlocksDestructiveCommandWithoutConfirmation()
    {
        var service = CreateService(allow: false);

        var result = await service.TryExecuteAsync("Remove-Item -LiteralPath 'anything'");

        Assert.True(result.Handled);
        Assert.True(result.IsError);
        Assert.Contains("autorização negada", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IgnoresOrdinaryConversation()
    {
        var service = CreateService();

        var result = await service.TryExecuteAsync("Qual foi a última coisa que eu disse?");

        Assert.False(result.Handled);
    }

    [Fact]
    public void ResolvesChromeOutsideTheApplicationDirectory()
    {
        var resolved = DesktopCommandService.ResolveInstalledApplication("chrome.exe");

        Assert.True(Path.IsPathRooted(resolved));
        Assert.True(File.Exists(resolved));
        Assert.EndsWith("chrome.exe", resolved, StringComparison.OrdinalIgnoreCase);
    }

    private static DesktopCommandService CreateService(bool allow = true) =>
        new(new FakeAuthorizationService(allow), new FakeWorkspaceService());

    private sealed class FakeAuthorizationService(bool allow) : IAuthorizationService
    {
        public Task<bool> RequestAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(allow);
    }

    private sealed class FakeWorkspaceService : IWorkspaceService
    {
        public WorkspaceContext? Current => null;
        public WorkspaceChangeProposal? PendingProposal => null;
        public Task<WorkspaceContext> OpenAsync(string rootPath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<WorkspaceContext> CreateAsync(string projectName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task ClearAsync() => Task.CompletedTask;
        public Task<string?> CreateBackupAsync(
            IEnumerable<string>? targets = null,
            CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public IReadOnlyList<string> GetBackups() => [];
        public Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<string> BuildRelevantContextAsync(
            string query, int maxCharacters = 35000, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);
        public Task<string> ProcessAgentResponseAsync(
            string response, CancellationToken cancellationToken = default) => Task.FromResult(response);
        public Task<string> ApplyProposalAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);
        public void CancelProposal() { }
    }
}
