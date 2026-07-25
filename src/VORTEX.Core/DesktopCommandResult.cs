namespace VORTEX.Core;

public sealed record DesktopCommandResult(bool Handled, string Output, bool IsError = false);
