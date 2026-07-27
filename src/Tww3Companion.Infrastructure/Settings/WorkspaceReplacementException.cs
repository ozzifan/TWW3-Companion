namespace Tww3Companion.Infrastructure.Settings;

public sealed class WorkspaceReplacementException(string recoveryPath) : Exception
{
  public string RecoveryPath { get; } = recoveryPath;
}
