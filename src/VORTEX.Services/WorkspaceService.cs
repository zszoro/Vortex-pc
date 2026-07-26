using System.Text.Json;
using System.Text.RegularExpressions;
using VORTEX.Core;

namespace VORTEX.Services;

public sealed class WorkspaceService : IWorkspaceService
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules", "dist", "build",
        ".next", ".nuxt", "coverage", "packages", "artifacts", "__pycache__", ".venv", "venv"
    };

    private static readonly Dictionary<string, string> LanguageByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "C#", [".xaml"] = "XAML", [".js"] = "JavaScript",
            [".jsx"] = "JavaScript/React", [".ts"] = "TypeScript",
            [".tsx"] = "TypeScript/React", [".vue"] = "Vue", [".py"] = "Python",
            [".java"] = "Java", [".kt"] = "Kotlin", [".kts"] = "Kotlin",
            [".cpp"] = "C++", [".c"] = "C", [".h"] = "C/C++",
            [".rs"] = "Rust", [".go"] = "Go", [".php"] = "PHP",
            [".html"] = "HTML", [".css"] = "CSS", [".scss"] = "SCSS",
            [".dart"] = "Dart", [".swift"] = "Swift", [".lua"] = "Lua"
        };

    private readonly IAuthorizationService _authorization;
    private readonly string _stateFile;
    private readonly string _backupsRoot;
    private bool _currentAuthorized;

    public WorkspaceContext? Current { get; private set; }

    public WorkspaceService(IAuthorizationService authorization, string? dataDirectory = null)
    {
        _authorization = authorization;
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VORTEX");
        Directory.CreateDirectory(dataDirectory);
        _stateFile = Path.Combine(dataDirectory, "workspace.json");
        _backupsRoot = Path.Combine(dataDirectory, "backups");
        Current = LoadPersisted();
    }

    public async Task<WorkspaceContext> OpenAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException(fullPath);
        var authorized = await _authorization.RequestAsync(new(
            "Leitura de arquivos",
            "Abrir Workspace",
            "O VORTEX irá ler a estrutura e os arquivos de configuração deste projeto para entender linguagens, dependências e arquitetura.",
            [fullPath]), cancellationToken);
        if (!authorized) throw new OperationCanceledException("Acesso à Workspace não autorizado.");

        Current = await Task.Run(() => Index(fullPath, cancellationToken), cancellationToken);
        _currentAuthorized = true;
        await PersistAsync(cancellationToken);
        return Current;
    }

    public async Task<WorkspaceContext> CreateAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        var safeName = Regex.Replace(projectName.Trim(), @"[^\p{L}\p{N}._ -]", string.Empty);
        if (string.IsNullOrWhiteSpace(safeName))
            throw new ArgumentException("Informe um nome de projeto válido.", nameof(projectName));
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VORTEX", "Projetos", safeName);
        var authorized = await _authorization.RequestAsync(new(
            "Criação de arquivos",
            "Criar novo projeto",
            "O VORTEX criará uma nova pasta de projeto e a definirá como Workspace atual.",
            [root]), cancellationToken);
        if (!authorized) throw new OperationCanceledException("Criação não autorizada.");
        Directory.CreateDirectory(root);
        Current = Index(root, cancellationToken);
        _currentAuthorized = true;
        await PersistAsync(cancellationToken);
        return Current;
    }

    public Task ClearAsync()
    {
        Current = null;
        _currentAuthorized = false;
        if (File.Exists(_stateFile)) File.Delete(_stateFile);
        return Task.CompletedTask;
    }

    public async Task<string?> CreateBackupAsync(
        IEnumerable<string>? targets = null,
        CancellationToken cancellationToken = default)
    {
        if (Current == null || !Directory.Exists(Current.RootPath)) return null;
        var backupRoot = Path.Combine(
            _backupsRoot, Current.Name,
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(backupRoot);

        var sourceTargets = targets?.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (sourceTargets == null || sourceTargets.Count == 0)
            sourceTargets = [Current.RootPath];

        foreach (var source in sourceTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsInsideWorkspace(source)) continue;
            if (File.Exists(source))
            {
                CopyFile(source, backupRoot);
            }
            else if (Directory.Exists(source))
            {
                foreach (var file in EnumerateFiles(source))
                    CopyFile(file, backupRoot);
            }
        }
        await File.WriteAllTextAsync(
            Path.Combine(backupRoot, "backup-info.json"),
            JsonSerializer.Serialize(new
            {
                workspace = Current.RootPath,
                createdAt = DateTimeOffset.Now,
                targets = sourceTargets
            }, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        return backupRoot;
    }

    public IReadOnlyList<string> GetBackups()
    {
        if (Current == null) return [];
        var projectBackups = Path.Combine(_backupsRoot, Current.Name);
        return Directory.Exists(projectBackups)
            ? Directory.GetDirectories(projectBackups).OrderByDescending(path => path).ToList()
            : [];
    }

    public async Task RestoreBackupAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        if (Current == null) throw new InvalidOperationException("Nenhuma Workspace está aberta.");
        var fullBackupPath = Path.GetFullPath(backupPath);
        var allowedRoot = Path.GetFullPath(_backupsRoot) + Path.DirectorySeparatorChar;
        if (!fullBackupPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(fullBackupPath))
            throw new InvalidOperationException("Backup inválido.");
        var authorized = await _authorization.RequestAsync(new(
            "Restauração de arquivos",
            "Restaurar Workspace",
            "Os arquivos atuais da Workspace serão substituídos pela versão selecionada. Antes disso, um backup de segurança será criado.",
            [Current.RootPath, fullBackupPath], true), cancellationToken);
        if (!authorized) throw new OperationCanceledException("Restauração não autorizada.");

        await CreateBackupAsync(cancellationToken: cancellationToken);
        foreach (var currentFile in EnumerateFiles(Current.RootPath))
            File.Delete(currentFile);
        foreach (var backupFile in Directory.EnumerateFiles(fullBackupPath, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(backupFile).Equals("backup-info.json", StringComparison.OrdinalIgnoreCase))
                continue;
            var relative = Path.GetRelativePath(fullBackupPath, backupFile);
            var destination = Path.Combine(Current.RootPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(backupFile, destination, true);
        }
        Current = Index(Current.RootPath, cancellationToken);
        await PersistAsync(cancellationToken);
    }

    public async Task<string> BuildRelevantContextAsync(
        string query,
        int maxCharacters = 35000,
        CancellationToken cancellationToken = default)
    {
        if (Current == null) return "Nenhuma Workspace vinculada.";
        if (!_currentAuthorized)
        {
            _currentAuthorized = await _authorization.RequestAsync(new(
                "Leitura de arquivos",
                "Retomar Workspace",
                "O VORTEX restaurará o contexto salvo e poderá ler os arquivos relevantes para esta conversa.",
                [Current.RootPath]), cancellationToken);
            if (!_currentAuthorized) return "Workspace persistida, mas a leitura não foi autorizada nesta sessão.";
        }
        var tokens = Regex.Matches(query.ToLowerInvariant(), @"[\p{L}\p{N}_.-]{3,}")
            .Select(match => match.Value).Distinct().ToList();
        var visualIntent = Regex.IsMatch(query, @"interface|visual|tema|layout|design|tela|componente", RegexOptions.IgnoreCase);
        var candidates = Current.Files
            .Select(relative =>
            {
                var score = tokens.Count(token => relative.Contains(token, StringComparison.OrdinalIgnoreCase)) * 10;
                var extension = Path.GetExtension(relative);
                if (visualIntent && extension is ".xaml" or ".css" or ".scss" or ".tsx" or ".jsx" or ".vue" or ".html")
                    score += 25;
                if (Current.DependencyFiles.Contains(relative)) score += 8;
                if (Path.GetFileName(relative).StartsWith("README", StringComparison.OrdinalIgnoreCase)) score += 5;
                return (relative, score);
            })
            .OrderByDescending(item => item.score)
            .ThenBy(item => item.relative.Count(character => character == Path.DirectorySeparatorChar))
            .ThenBy(item => item.relative)
            .Take(30)
            .ToList();
        var builder = new System.Text.StringBuilder();
        foreach (var (relative, _) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(Current.RootPath, relative);
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length > 512_000) continue;
            string content;
            try { content = await File.ReadAllTextAsync(fullPath, cancellationToken); }
            catch { continue; }
            var block = $"\n--- ARQUIVO: {relative} ---\n{content}\n--- FIM: {relative} ---\n";
            if (builder.Length + block.Length > maxCharacters) break;
            builder.Append(block);
        }
        return builder.Length > 0
            ? builder.ToString()
            : "Nenhum conteúdo textual relevante pôde ser carregado.";
    }

    public async Task<string> ProcessAgentResponseAsync(
        string response,
        CancellationToken cancellationToken = default)
    {
        if (Current == null) return response;
        var match = Regex.Match(
            response,
            @"<vortex-file-actions>(?<json>[\s\S]*?)</vortex-file-actions>",
            RegexOptions.IgnoreCase);
        if (!match.Success) return response;
        List<WorkspaceFileChange>? changes;
        try
        {
            changes = JsonSerializer.Deserialize<List<WorkspaceFileChange>>(
                match.Groups["json"].Value,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return response.Replace(match.Value, string.Empty).Trim()
                   + "\n\n⚠️ O plano de arquivos retornado pela IA era inválido e não foi aplicado.";
        }
        if (changes == null || changes.Count == 0)
            return response.Replace(match.Value, string.Empty).Trim();

        var resolved = changes.Select(change => (
            Change: change,
            Source: ResolveWorkspacePath(change.Path),
            Destination: string.IsNullOrWhiteSpace(change.DestinationPath)
                ? null
                : ResolveWorkspacePath(change.DestinationPath!))).ToList();
        var targets = resolved.SelectMany(item =>
                new[] { item.Source, item.Destination }.Where(path => path != null).Cast<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var description = string.Join("\n", resolved.Select(item =>
            $"{item.Change.Operation}: {Path.GetRelativePath(Current.RootPath, item.Source)}" +
            (item.Destination == null ? string.Empty : $" → {Path.GetRelativePath(Current.RootPath, item.Destination)}")));
        var authorized = await _authorization.RequestAsync(new(
            "Modificar Workspace",
            $"Aplicar {changes.Count} alteração(ões)",
            "O VORTEX analisou o pedido e propõe estas mudanças. Um backup completo será criado antes de aplicar:\n\n" + description,
            targets, true), cancellationToken);
        var cleanResponse = response.Replace(match.Value, string.Empty).Trim();
        if (!authorized) return cleanResponse + "\n\nA aplicação das mudanças foi cancelada.";

        var backup = await CreateBackupAsync(cancellationToken: cancellationToken);
        foreach (var item in resolved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (item.Change.Operation.ToLowerInvariant())
            {
                case "delete":
                    if (File.Exists(item.Source)) File.Delete(item.Source);
                    else if (Directory.Exists(item.Source)) Directory.Delete(item.Source, true);
                    break;
                case "move":
                case "rename":
                    if (item.Destination == null)
                        throw new InvalidOperationException("Destino ausente para mover/renomear.");
                    Directory.CreateDirectory(Path.GetDirectoryName(item.Destination)!);
                    if (File.Exists(item.Source)) File.Move(item.Source, item.Destination, true);
                    else if (Directory.Exists(item.Source)) Directory.Move(item.Source, item.Destination);
                    break;
                default:
                    Directory.CreateDirectory(Path.GetDirectoryName(item.Source)!);
                    await File.WriteAllTextAsync(item.Source, item.Change.Content ?? string.Empty, cancellationToken);
                    break;
            }
        }
        Current = Index(Current.RootPath, cancellationToken);
        await PersistAsync(cancellationToken);
        return cleanResponse + $"\n\n✅ {changes.Count} alteração(ões) aplicada(s). Backup: {backup}";
    }

    private WorkspaceContext Index(string rootPath, CancellationToken cancellationToken)
    {
        var files = EnumerateFiles(rootPath).Take(20000).ToList();
        var relativeFiles = files.Select(file => Path.GetRelativePath(rootPath, file)).ToList();
        var languages = files
            .Select(file => LanguageByExtension.GetValueOrDefault(Path.GetExtension(file)))
            .Where(language => language != null).Distinct().Order().Cast<string>().ToList();
        var names = files.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var frameworks = DetectFrameworks(names, files);
        var dependencyFiles = relativeFiles.Where(file =>
            file.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("requirements.txt", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("pyproject.toml", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("Cargo.toml", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("go.mod", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("pom.xml", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("build.gradle", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("composer.json", StringComparison.OrdinalIgnoreCase)).ToList();
        var directoryCount = relativeFiles.Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory)).Distinct().Count();
        cancellationToken.ThrowIfCancellationRequested();
        return new WorkspaceContext
        {
            Name = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar)),
            RootPath = rootPath,
            IndexedAt = DateTime.UtcNow,
            FileCount = relativeFiles.Count,
            DirectoryCount = directoryCount,
            Languages = languages,
            Frameworks = frameworks,
            DependencyFiles = dependencyFiles,
            Files = relativeFiles,
            ArchitectureSummary =
                $"{relativeFiles.Count} arquivos em {directoryCount} diretórios. " +
                $"Linguagens: {string.Join(", ", languages.DefaultIfEmpty("não identificadas"))}. " +
                $"Frameworks: {string.Join(", ", frameworks.DefaultIfEmpty("não identificados"))}. " +
                $"Manifestos: {string.Join(", ", dependencyFiles.Take(20).DefaultIfEmpty("nenhum"))}."
        };
    }

    private IEnumerable<string> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> subdirectories;
            IEnumerable<string> files;
            try
            {
                subdirectories = Directory.EnumerateDirectories(directory);
                files = Directory.EnumerateFiles(directory);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
            foreach (var subdirectory in subdirectories)
                if (!IgnoredDirectories.Contains(Path.GetFileName(subdirectory)))
                    pending.Push(subdirectory);
            foreach (var file in files) yield return file;
        }
    }

    private static List<string> DetectFrameworks(HashSet<string?> names, List<string> files)
    {
        var frameworks = new List<string>();
        if (names.Contains("next.config.js") || names.Contains("next.config.mjs")) frameworks.Add("Next.js");
        if (names.Contains("angular.json")) frameworks.Add("Angular");
        if (names.Contains("vite.config.ts") || names.Contains("vite.config.js")) frameworks.Add("Vite");
        if (names.Contains("vue.config.js")) frameworks.Add("Vue");
        if (names.Contains("Cargo.toml")) frameworks.Add("Rust/Cargo");
        if (names.Contains("go.mod")) frameworks.Add("Go Modules");
        if (names.Contains("fabric.mod.json")) frameworks.Add("Minecraft Fabric");
        if (names.Contains("plugin.yml")) frameworks.Add("Bukkit/Spigot/Paper");
        if (names.Contains("build.gradle") || names.Contains("build.gradle.kts")) frameworks.Add("Gradle");
        if (files.Any(file => file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))) frameworks.Add(".NET");
        return frameworks.Distinct().ToList();
    }

    private bool IsInsideWorkspace(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Current!.RootPath))
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        return candidate.Equals(Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveWorkspacePath(string relativePath)
    {
        if (Current == null) throw new InvalidOperationException("Nenhuma Workspace aberta.");
        var fullPath = Path.GetFullPath(Path.Combine(Current.RootPath, relativePath));
        if (!IsInsideWorkspace(fullPath))
            throw new UnauthorizedAccessException($"Caminho fora da Workspace: {relativePath}");
        return fullPath;
    }

    private void CopyFile(string source, string backupRoot)
    {
        var relative = Path.GetRelativePath(Current!.RootPath, source);
        var destination = Path.Combine(backupRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, true);
    }

    private WorkspaceContext? LoadPersisted()
    {
        try
        {
            var context = File.Exists(_stateFile)
                ? JsonSerializer.Deserialize<WorkspaceContext>(File.ReadAllText(_stateFile))
                : null;
            return context != null && Directory.Exists(context.RootPath) ? context : null;
        }
        catch { return null; }
    }

    private Task PersistAsync(CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(_stateFile, JsonSerializer.Serialize(Current, new JsonSerializerOptions
        {
            WriteIndented = true
        }), cancellationToken);
}
