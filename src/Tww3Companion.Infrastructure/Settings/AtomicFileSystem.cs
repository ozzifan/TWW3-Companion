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

  public Stream CreateWriteProbe(string directory) => new FileStream(
      Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}"),
      FileMode.CreateNew,
      FileAccess.Write,
      FileShare.None,
      1,
      FileOptions.DeleteOnClose);
}
