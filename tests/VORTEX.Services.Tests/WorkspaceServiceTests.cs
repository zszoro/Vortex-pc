using VORTEX.Core;
using VORTEX.Services;
using Xunit;

namespace VORTEX.Services.Tests;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task IndexesAndAppliesAuthorizedFilePlanWithBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "vortex-tests", Guid.NewGuid().ToString("N"));
        var data = Path.Combine(Path.GetTempPath(), "vortex-tests-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "Sample.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "Console.WriteLine(\"old\");");
            var service = new WorkspaceService(new AllowAuthorization(), data);

            var context = await service.OpenAsync(root);
            var response = await service.ProcessAgentResponseAsync("""
                Vou atualizar o arquivo.
                <vortex-file-actions>
                [{"operation":"write","path":"Program.cs","content":"Console.WriteLine(\"new\");"}]
                </vortex-file-actions>
                """);

            Assert.Contains("C#", context.Languages);
            Assert.Contains(".NET", context.Frameworks);
            Assert.Contains("aplicada", response);
            Assert.Equal("Console.WriteLine(\"new\");", await File.ReadAllTextAsync(Path.Combine(root, "Program.cs")));
            Assert.NotEmpty(service.GetBackups());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(data)) Directory.Delete(data, true);
        }
    }

    private sealed class AllowAuthorization : IAuthorizationService
    {
        public Task<bool> RequestAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
