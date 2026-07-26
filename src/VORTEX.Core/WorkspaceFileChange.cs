namespace VORTEX.Core;

public sealed class WorkspaceFileChange
{
    public string Operation { get; set; } = "write";
    public string Path { get; set; } = string.Empty;
    public string? DestinationPath { get; set; }
    public string? Content { get; set; }
}
