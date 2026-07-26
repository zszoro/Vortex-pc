using VORTEX.ViewModels;
using Xunit;

namespace VORTEX.Services.Tests;

public sealed class MainViewModelRoutingTests
{
    [Theory]
    [InlineData("discord mande mensagem para o math")]
    [InlineData("mande mensagem para o Math no Discord: oi")]
    [InlineData("envie msg no discord para Math")]
    public void DetectsDiscordMessageRequestsInEitherOrder(string prompt)
    {
        Assert.True(MainViewModel.IsDiscordMessageRequest(prompt));
    }

    [Theory]
    [InlineData("abra o discord")]
    [InlineData("como configuro o discord?")]
    [InlineData("mande mensagem no telegram")]
    public void IgnoresNonDiscordMessageRequests(string prompt)
    {
        Assert.False(MainViewModel.IsDiscordMessageRequest(prompt));
    }

    [Theory]
    [InlineData("mande mensagem para Math no Discord: oi", "Math", "oi")]
    [InlineData("discord mande mensagem para math dizendo bom dia", "math", "bom dia")]
    [InlineData("envie msg no discord para Math \"cheguei\"", "Math", "cheguei")]
    public void ExtractsDiscordMessageText(string prompt, string target, string expected)
    {
        Assert.Equal(expected, MainViewModel.ExtractDiscordMessage(prompt, target));
    }
}
