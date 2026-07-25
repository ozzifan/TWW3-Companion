using System.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.ViewModels;

namespace Tww3Companion.Desktop.Services;

public sealed class ImportSourceFileService : IImportSourceFileService
{
  private const int MaxFileSizeBytes = 4 * 1024 * 1024;
  private static readonly FilePickerFileType MarkdownFileType = new("Markdown")
  {
    Patterns = ["*.md"]
  };
  private static readonly FilePickerFileType TextFileType = new("Text")
  {
    Patterns = ["*.txt"]
  };

  private readonly Func<IStorageProvider?>? _storageProviderProvider;
  private readonly Func<FilePickerOpenOptions, CancellationToken, Task<IReadOnlyList<ImportSourceFileSelection>>>? _pickFilesDirectly;

  public ImportSourceFileService(Func<TopLevel?> topLevelProvider)
  {
    ArgumentNullException.ThrowIfNull(topLevelProvider);
    _storageProviderProvider = () => topLevelProvider()?.StorageProvider;
  }

  internal ImportSourceFileService(
      Func<FilePickerOpenOptions, CancellationToken, Task<IReadOnlyList<ImportSourceFileSelection>>> pickFilesDirectly)
  {
    _pickFilesDirectly = pickFilesDirectly ?? throw new ArgumentNullException(nameof(pickFilesDirectly));
  }

  public async Task<ImportSourceDocument?> ChooseTextFileAsync(
      CancellationToken cancellationToken = default)
  {
    var options = CreateFilePickerOptions();
    if (_pickFilesDirectly is not null)
    {
      var selections = await _pickFilesDirectly(options, cancellationToken);
      cancellationToken.ThrowIfCancellationRequested();
      if (selections.Count == 0)
      {
        return null;
      }

      var selection = selections[0];
      if (selection.Content.Length > MaxFileSizeBytes)
      {
        throw new ImportTextDecodingException(
            "import.source.size.exceeded",
            "Import source exceeds the accepted size limit.");
      }

      var text = ImportTextDecoder.Decode(selection.Content.Span);
      return new ImportSourceDocument(Path.GetFileName(selection.Name), text);
    }

    var storageProvider = _storageProviderProvider!();
    if (storageProvider is null)
    {
      return null;
    }

    cancellationToken.ThrowIfCancellationRequested();
    var files = await storageProvider.OpenFilePickerAsync(options);
    cancellationToken.ThrowIfCancellationRequested();
    if (files.Count == 0)
    {
      return null;
    }

    var storageFile = files[0];
    await using var stream = await storageFile.OpenReadAsync();
    var bytes = await ReadBoundedBytesAsync(stream, cancellationToken);
    var decodedText = ImportTextDecoder.Decode(bytes);
    var fileName = Path.GetFileName(storageFile.Name);
    return new ImportSourceDocument(fileName, decodedText);
  }

  internal static FilePickerOpenOptions CreateFilePickerOptions() =>
      new()
      {
        Title = "Import source file",
        AllowMultiple = false,
        FileTypeFilter = [MarkdownFileType, TextFileType]
      };

  internal static async Task<byte[]> ReadBoundedBytesAsync(Stream stream, CancellationToken cancellationToken)
  {
    if (stream.CanSeek)
    {
      if (stream.Length > MaxFileSizeBytes)
      {
        throw new ImportTextDecodingException(
            "import.source.size.exceeded",
            "Import source exceeds the accepted size limit.");
      }

      var buffer = new byte[(int)stream.Length];
      await stream.ReadExactlyAsync(buffer, cancellationToken);
      return buffer;
    }

    using var memoryStream = new MemoryStream();
    var chunk = new byte[81920];
    while (true)
    {
      var read = await stream.ReadAsync(chunk, cancellationToken);
      if (read == 0)
      {
        break;
      }

      if (memoryStream.Length + read > MaxFileSizeBytes)
      {
        throw new ImportTextDecodingException(
            "import.source.size.exceeded",
            "Import source exceeds the accepted size limit.");
      }

      memoryStream.Write(chunk, 0, read);
    }

    return memoryStream.ToArray();
  }

  internal readonly record struct ImportSourceFileSelection(string Name, ReadOnlyMemory<byte> Content);
}
