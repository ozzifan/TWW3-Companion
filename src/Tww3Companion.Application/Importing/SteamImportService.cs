namespace Tww3Companion.Application.Importing;

public static class SteamImportService
{
  public static Task<SteamImportResult> BuildPreviewAsync(SteamImportResult result)
  {
    return ImportHandoffService.BuildPreviewAsync(result, static preview => preview with { Applied = false });
  }

  public static async Task<SteamImportResult> ApplyAsync(SteamImportResult result, bool confirm)
  {
    return await ImportHandoffService.ApplyAsync(
        result,
        confirm,
        static preview => preview with { Applied = false },
        Validate,
        static preview => preview with { Applied = true });
  }

  private static void Validate(SteamImportResult result)
  {
    if (result.Candidates.Any(candidate => candidate.DisplayName is null))
    {
      throw new SteamImportValidationException("Each Steam import candidate must have metadata before it can be applied.");
    }
  }
}

public sealed class SteamImportValidationException(string message) : Exception(message);
