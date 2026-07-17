namespace Tww3Companion.Infrastructure.Tests.Support;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tww3c-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
