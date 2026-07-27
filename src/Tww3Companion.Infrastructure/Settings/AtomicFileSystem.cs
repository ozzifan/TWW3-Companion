namespace Tww3Companion.Infrastructure.Settings;

internal sealed class AtomicFileSystem : IAtomicFileSystem
{
  public async Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token)
  {
    var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
    try
    {
      await File.WriteAllTextAsync(temporaryPath, content, token);
      File.Move(temporaryPath, path, overwrite: true);
    }
    finally
    {
      File.Delete(temporaryPath);
    }
  }

  public void MoveWithoutOverwrite(string source, string destination) =>
      File.Move(source, destination, overwrite: false);

  public void ReplaceWithRecovery(string preparedPath, string destinationPath, string recoveryPath)
  {
    File.Move(destinationPath, recoveryPath, overwrite: false);
    try
    {
      File.Move(preparedPath, destinationPath, overwrite: false);
    }
    catch
    {
      try
      {
        File.Move(recoveryPath, destinationPath, overwrite: false);
      }
      catch
      {
        throw new WorkspaceReplacementException(recoveryPath);
      }

      throw;
    }
  }

  public Stream CreateWriteProbe(string directory) => new FileStream(
      Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}"),
      FileMode.CreateNew,
      FileAccess.Write,
      FileShare.None,
      1,
      FileOptions.DeleteOnClose);
}
