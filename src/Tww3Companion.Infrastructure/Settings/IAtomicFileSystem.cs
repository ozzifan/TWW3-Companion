namespace Tww3Companion.Infrastructure.Settings;

public interface IAtomicFileSystem
{
    Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token);
    void MoveWithoutOverwrite(string source, string destination);
    Stream CreateWriteProbe(string directory);
}
