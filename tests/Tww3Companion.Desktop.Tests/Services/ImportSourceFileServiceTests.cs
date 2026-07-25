using System.Text;
using Avalonia.Platform.Storage;
using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;
using Xunit;
using static Tww3Companion.Desktop.Services.ImportSourceFileService;

namespace Tww3Companion.Desktop.Tests.Services;

public sealed class ImportSourceFileServiceTests
{
  [Fact]
  public async Task ChooseTextFileAsync_returns_null_when_no_file_is_selected()
  {
    var service = CreateService([]);

    var result = await service.ChooseTextFileAsync(TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task ChooseTextFileAsync_returns_filename_only_and_decoded_text()
  {
    var service = CreateService([
        new ImportSourceFileSelection("folder/notes.md", Encoding.UTF8.GetBytes("- item"))
    ]);

    var result = await service.ChooseTextFileAsync(TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal("notes.md", result.Name);
    Assert.Equal("- item", result.Text);
    Assert.DoesNotContain("folder", result.Name, StringComparison.Ordinal);
  }

  [Fact]
  public async Task ChooseTextFileAsync_limits_picker_to_markdown_and_text_files()
  {
    FilePickerOpenOptions? capturedOptions = null;
    var service = new ImportSourceFileService((options, _) =>
    {
      capturedOptions = options;
      return Task.FromResult<IReadOnlyList<ImportSourceFileSelection>>([]);
    });

    await service.ChooseTextFileAsync(TestContext.Current.CancellationToken);

    var patterns = capturedOptions!.FileTypeFilter!
        .SelectMany(type => type.Patterns ?? [])
        .ToArray();
    Assert.Contains("*.md", patterns);
    Assert.Contains("*.txt", patterns);
    Assert.False(capturedOptions.AllowMultiple);
  }

  [Fact]
  public async Task ChooseTextFileAsync_rejects_files_larger_than_four_mebibytes()
  {
    var oversized = new byte[(4 * 1024 * 1024) + 1];
    var service = CreateService([new ImportSourceFileSelection("large.md", oversized)]);

    await Assert.ThrowsAsync<ImportTextDecodingException>(() =>
        service.ChooseTextFileAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ChooseTextFileAsync_honors_cancellation()
  {
    using var cancellationSource = new CancellationTokenSource();
    await cancellationSource.CancelAsync();
    var service = new ImportSourceFileService((_, token) =>
    {
      token.ThrowIfCancellationRequested();
      return Task.FromResult<IReadOnlyList<ImportSourceFileSelection>>([]);
    });

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        service.ChooseTextFileAsync(cancellationSource.Token));
  }

  [Fact]
  public void CreateFilePickerOptions_uses_expected_filters()
  {
    var options = CreateFilePickerOptions();

    Assert.Equal("Import source file", options.Title);
    Assert.False(options.AllowMultiple);
  }

  [Fact]
  public async Task ReadBoundedBytesAsync_rejects_oversized_seekable_streams()
  {
    await using var stream = new MemoryStream(new byte[(4 * 1024 * 1024) + 1]);

    await Assert.ThrowsAsync<ImportTextDecodingException>(() =>
        ReadBoundedBytesAsync(stream, TestContext.Current.CancellationToken));
  }

  private static ImportSourceFileService CreateService(IReadOnlyList<ImportSourceFileSelection> files) =>
      new((_, _) => Task.FromResult(files));
}
