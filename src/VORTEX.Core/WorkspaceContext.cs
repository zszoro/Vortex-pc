namespace VORTEX.Core;

public sealed class WorkspaceContext
{
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public List<string> Languages { get; set; } = [];
    public List<string> Frameworks { get; set; } = [];
    public List<string> DependencyFiles { get; set; } = [];
    public List<string> Files { get; set; } = [];
    public string ArchitectureSummary { get; set; } = string.Empty;
}
