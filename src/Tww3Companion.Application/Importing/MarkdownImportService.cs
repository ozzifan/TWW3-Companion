namespace Tww3Companion.Application.Importing;

public static class MarkdownImportService
{
  public static Task<MarkdownImportResult> BuildPreviewAsync(MarkdownImportResult result)
  {
    ArgumentNullException.ThrowIfNull(result);

    return Task.FromResult(result with { Applied = false });
  }

  public static async Task<MarkdownImportResult> ApplyAsync(MarkdownImportResult result, bool confirm)
  {
    var preview = await BuildPreviewAsync(result);
    if (!confirm) return preview;

    Validate(preview);
    return preview with { Applied = true };
  }

  private static void Validate(MarkdownImportResult result)
  {
    if (result.Candidates.Any(candidate => candidate.Kind == ImportCandidateKind.Candidate && candidate.SourceReference is null))
    {
      throw new ImportValidationException("Each import candidate must be resolved before it can be applied.");
    }
  }
}

public sealed class ImportValidationException(string message) : Exception(message);
