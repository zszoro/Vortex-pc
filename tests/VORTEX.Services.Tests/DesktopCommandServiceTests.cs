using VORTEX.Services;
using Xunit;

namespace VORTEX.Services.Tests;

public sealed class DesktopCommandServiceTests
{
    [Fact]
    public async Task ExecutesRecognizedTerminalCommand()
    {
        var service = new DesktopCommandService();

        var result = await service.TryExecuteAsync("echo vortex-ok");

        Assert.True(result.Handled);
        Assert.False(result.IsError);
        Assert.Contains("vortex-ok", result.Output);
    }

    [Fact]
    public async Task BlocksDestructiveCommandWithoutConfirmation()
    {
        var service = new DesktopCommandService();

        var result = await service.TryExecuteAsync("Remove-Item -LiteralPath 'anything'");

        Assert.True(result.Handled);
        Assert.True(result.IsError);
        Assert.Contains("/confirmar", result.Output);
    }

    [Fact]
    public async Task IgnoresOrdinaryConversation()
    {
        var service = new DesktopCommandService();

        var result = await service.TryExecuteAsync("Qual foi a última coisa que eu disse?");

        Assert.False(result.Handled);
    }
}
