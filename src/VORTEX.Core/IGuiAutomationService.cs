namespace VORTEX.Core;

public interface IGuiAutomationService
{
    Task PrepareDiscordMessageAsync(
        string recipient,
        string message,
        CancellationToken cancellationToken = default);

    Task ConfirmDiscordSendAsync(CancellationToken cancellationToken = default);
}
