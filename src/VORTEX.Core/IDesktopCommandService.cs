namespace VORTEX.Core;

public interface IDesktopCommandService
{
    Task<DesktopCommandResult> TryExecuteAsync(string input, CancellationToken cancellationToken = default);
}
