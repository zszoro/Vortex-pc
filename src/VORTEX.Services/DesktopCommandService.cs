using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using VORTEX.Core;

namespace VORTEX.Services;

public sealed partial class DesktopCommandService : IDesktopCommandService
{
    private static readonly HashSet<string> TerminalCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "cd", "dir", "ls", "pwd", "echo", "clear", "cls", "type", "cat",
        "get-childitem", "get-content", "set-content", "add-content", "copy-item",
        "move-item", "new-item", "remove-item", "rename-item", "test-path",
        "dotnet", "git", "npm", "npx", "node", "python", "py", "pip", "code",
        "explorer", "start", "ping", "ipconfig", "whoami", "tasklist", "where"
    };

    private static readonly Regex DestructivePattern = new(
        @"(^|\s)(remove-item|del|erase|rmdir|rd|format|clear-content|stop-process|shutdown|git\s+reset\s+--hard)(\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string _workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public async Task<DesktopCommandResult> TryExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var text = input.Trim();
        if (string.IsNullOrEmpty(text)) return new(false, string.Empty);

        var confirmed = text.StartsWith("/confirmar ", StringComparison.OrdinalIgnoreCase);
        if (confirmed) text = text[11..].TrimStart();

        if (TryGetTerminalCommand(text, out var command))
        {
            if (DestructivePattern.IsMatch(command) && !confirmed)
            {
                return new(true,
                    "⚠️ Este comando pode apagar ou interromper dados/processos. Para executar, envie novamente começando com `/confirmar `.",
                    true);
            }

            return await ExecutePowerShellAsync(command, cancellationToken);
        }

        var openFolder = OpenFolderPattern().Match(text);
        if (openFolder.Success)
        {
            return OpenPath(ExpandPath(openFolder.Groups["path"].Value), true);
        }

        var openApp = OpenAppPattern().Match(text);
        if (openApp.Success)
        {
            return OpenApplication(openApp.Groups["app"].Value.Trim());
        }

        var move = MovePattern().Match(text);
        if (move.Success)
        {
            if (!confirmed)
            {
                return new(true,
                    "⚠️ Mover arquivos ou pastas altera o sistema. Reenvie a instrução começando com `/confirmar `.",
                    true);
            }
            var source = EscapePowerShell(ExpandPath(move.Groups["source"].Value));
            var destination = EscapePowerShell(ExpandPath(move.Groups["destination"].Value));
            return await ExecutePowerShellAsync(
                $"Move-Item -LiteralPath '{source}' -Destination '{destination}'", cancellationToken);
        }

        var write = WriteFilePattern().Match(text);
        if (write.Success)
        {
            if (!confirmed)
            {
                return new(true,
                    "⚠️ Modificar um arquivo exige confirmação. Reenvie começando com `/confirmar `.",
                    true);
            }
            var path = EscapePowerShell(ExpandPath(write.Groups["path"].Value));
            var content = EscapePowerShell(write.Groups["content"].Value);
            return await ExecutePowerShellAsync(
                $"Set-Content -LiteralPath '{path}' -Value '{content}' -Encoding UTF8", cancellationToken);
        }

        return new(false, string.Empty);
    }

    private async Task<DesktopCommandResult> ExecutePowerShellAsync(
        string command,
        CancellationToken cancellationToken)
    {
        if (command.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
        {
            var requested = command[3..].Trim().Trim('"', '\'');
            var target = ExpandPath(requested);
            if (!Directory.Exists(target))
                return new(true, $"Pasta não encontrada: {target}", true);
            _workingDirectory = Path.GetFullPath(target);
            return new(true, $"Diretório atual: {_workingDirectory}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            return new(true, "O comando excedeu o limite de 45 segundos e foi interrompido.", true);
        }

        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();
        var combined = new StringBuilder();
        if (!string.IsNullOrEmpty(output)) combined.AppendLine(output);
        if (!string.IsNullOrEmpty(error)) combined.AppendLine(error);
        if (combined.Length == 0) combined.Append("Comando concluído sem saída.");
        const int maxOutput = 12000;
        var result = combined.ToString().Trim();
        if (result.Length > maxOutput)
            result = result[..maxOutput] + "\n\n[saída reduzida]";
        return new(true, result, process.ExitCode != 0);
    }

    private static DesktopCommandResult OpenPath(string path, bool requireDirectory)
    {
        if (requireDirectory && !Directory.Exists(path))
            return new(true, $"Pasta não encontrada: {path}", true);
        if (!requireDirectory && !File.Exists(path) && !Directory.Exists(path))
            return new(true, $"Caminho não encontrado: {path}", true);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return new(true, $"Aberto: {path}");
    }

    private static DesktopCommandResult OpenApplication(string requested)
    {
        var app = requested.Trim().Trim('.', '"', '\'').ToLowerInvariant();
        var executableName = app switch
        {
            "bloco de notas" or "notepad" => "notepad.exe",
            "calculadora" or "calculator" => "calc.exe",
            "explorador" or "explorer" or "explorador de arquivos" => "explorer.exe",
            "paint" => "mspaint.exe",
            "terminal" or "powershell" => "powershell.exe",
            "prompt" or "cmd" => "cmd.exe",
            "chrome" or "google chrome" => "chrome.exe",
            "edge" or "microsoft edge" => "msedge.exe",
            "firefox" or "mozilla firefox" => "firefox.exe",
            "spotify" => "Spotify.exe",
            "discord" => "Discord.exe",
            "visual studio code" or "vs code" or "vscode" => "Code.exe",
            "configurações" or "configuracoes" => "ms-settings:",
            _ => requested.Trim().Trim('"', '\'')
        };
        try
        {
            var executable = ResolveInstalledApplication(executableName);
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            return new(true, $"Aplicativo aberto: {requested}");
        }
        catch (Exception ex)
        {
            return new(true, $"Não consegui abrir “{requested}”: {ex.Message}", true);
        }
    }

    internal static string ResolveInstalledApplication(string executable)
    {
        if (executable.Contains(':') || Path.IsPathRooted(executable)) return executable;

        if (OperatingSystem.IsWindows())
        {
            foreach (var root in new[]
                     {
                         @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths",
                         @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths",
                         @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths"
                     })
            {
                var registered = Registry.GetValue($@"{root}\{executable}", null, null) as string;
                if (!string.IsNullOrWhiteSpace(registered) && File.Exists(registered))
                    return registered;
            }
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string[] candidates = executable.ToLowerInvariant() switch
        {
            "chrome.exe" =>
            [
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe")
            ],
            "msedge.exe" =>
            [
                Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe")
            ],
            "firefox.exe" =>
            [
                Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe"),
                Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe")
            ],
            "code.exe" =>
            [
                Path.Combine(local, "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(programFiles, "Microsoft VS Code", "Code.exe")
            ],
            "spotify.exe" =>
            [
                Path.Combine(local, "Microsoft", "WindowsApps", "Spotify.exe")
            ],
            "discord.exe" =>
            [
                Path.Combine(local, "Discord", "Update.exe")
            ],
            _ => []
        };
        return candidates.FirstOrDefault(File.Exists) ?? executable;
    }

    private static bool TryGetTerminalCommand(string text, out string command)
    {
        command = text;
        if (text.StartsWith("/terminal ", StringComparison.OrdinalIgnoreCase))
        {
            command = text[10..].TrimStart();
            return true;
        }
        if (text.StartsWith("$ ") || text.StartsWith("> "))
        {
            command = text[2..];
            return true;
        }
        var first = text.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        return TerminalCommands.Contains(first);
    }

    private string ExpandPath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"', '\''));
        if (expanded == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (expanded.StartsWith("~\\") || expanded.StartsWith("~/"))
            expanded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded[2..]);
        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(_workingDirectory, expanded));
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''");

    [GeneratedRegex(@"^(?:abra|abrir|abra a)\s+pasta\s+[""']?(?<path>.+?)[""']?[.!]?$", RegexOptions.IgnoreCase)]
    private static partial Regex OpenFolderPattern();

    [GeneratedRegex(@"^(?:abra|abrir|inicie|iniciar)\s+(?:o|a)?\s*(?<app>.+?)$", RegexOptions.IgnoreCase)]
    private static partial Regex OpenAppPattern();

    [GeneratedRegex(@"^(?:mova|mover)\s+(?:a pasta|o arquivo)?\s*[""'](?<source>.+?)[""']\s+para\s+[""'](?<destination>.+?)[""']$", RegexOptions.IgnoreCase)]
    private static partial Regex MovePattern();

    [GeneratedRegex(@"^(?:escreva|modifique|alterar)\s+(?:o )?arquivo\s+[""'](?<path>.+?)[""']\s+(?:com|para)\s+[""'](?<content>[\s\S]*?)[""']$", RegexOptions.IgnoreCase)]
    private static partial Regex WriteFilePattern();
}
