using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.ViewModels;

namespace Tww3Companion.Desktop.Services;

public sealed class ImportTaskCoordinator(
    IImportEngine engine,
    ISteamMetadataClient metadataClient) : IImportTaskCoordinator
{
  private readonly IImportEngine _engine = engine ?? throw new ArgumentNullException(nameof(engine));
  private readonly ISteamMetadataClient _metadataClient =
      metadataClient ?? throw new ArgumentNullException(nameof(metadataClient));

  public async Task<ImportSourceLoadResult> LoadSourceAsync(
      ImportSourceRequest request,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    return request.Kind switch
    {
      ImportSourceKind.Markdown => await LoadMarkdownAsync(request, cancellationToken),
      ImportSourceKind.SteamCollection => await LoadSteamCollectionAsync(request, cancellationToken),
      ImportSourceKind.SteamItems => await LoadSteamItemsAsync(request, cancellationToken),
      _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unsupported import source kind.")
    };
  }

  public Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default) =>
      _engine.BuildPreviewAsync(targetContext, candidates, cancellationToken);

  public Task<ImportPreview> ResolveAsync(
      ImportPreview preview,
      ImportCandidate resolvedCandidate,
      CancellationToken cancellationToken = default) =>
      _engine.ResolveAsync(preview, resolvedCandidate, cancellationToken);

  public Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      CancellationToken cancellationToken = default) =>
      _engine.ApplyAsync(preview, confirm: true, cancellationToken);

  private async Task<ImportSourceLoadResult> LoadMarkdownAsync(
      ImportSourceRequest request,
      CancellationToken cancellationToken)
  {
    var parsed = MarkdownImportAdapter.Parse(request.InputText);
    var disclosedWorkshopIds = parsed.Candidates
        .Where(candidate => candidate.SourceReference is not null)
        .Select(candidate => candidate.SourceReference!.WorkshopId)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    var diagnostics = parsed.Diagnostics
        .Select(diagnostic => new ImportTaskDiagnostic(
            "import.source.markdown.read",
            diagnostic.Message,
            IsBlocking: false,
            SafeNextAction: "Continue or correct the source input."))
        .ToList();

    if (!request.RequestMetadata)
    {
      return new ImportSourceLoadResult(parsed.Candidates, diagnostics, disclosedWorkshopIds);
    }

    var enrichedCandidates = new List<object>();
    foreach (var candidate in parsed.Candidates)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (candidate.SourceReference is null || candidate.Kind != ImportCandidateKind.Candidate)
      {
        enrichedCandidates.Add(candidate);
        continue;
      }

      var workshopItemId = candidate.SourceReference.WorkshopId;
      try
      {
        var metadata = await _metadataClient.GetWorkshopItemAsync(workshopItemId, cancellationToken);
        enrichedCandidates.Add(candidate with { Value = metadata.DisplayName });
      }
      catch (Exception exception) when (exception is not OperationCanceledException)
      {
        enrichedCandidates.Add(ImportCandidate.Unresolved(
            $"markdown:{candidate.SourceLine}",
            ImportSourceReference.SteamWorkshop(workshopItemId)));
        diagnostics.Add(CreateLookupDiagnostic());
      }
    }

    return new ImportSourceLoadResult(enrichedCandidates, diagnostics, disclosedWorkshopIds);
  }

  private async Task<ImportSourceLoadResult> LoadSteamCollectionAsync(
      ImportSourceRequest request,
      CancellationToken cancellationToken)
  {
    var tokens = TokenizeInput(request.InputText);
    if (tokens.Count != 1 || !IsNumericWorkshopId(tokens[0]))
    {
      return new ImportSourceLoadResult(
          [],
          [CreateCollectionValidationDiagnostic()],
          []);
    }

    var collectionId = tokens[0];
    if (!request.RequestMetadata)
    {
      return new ImportSourceLoadResult(
          [new SteamImportCandidate(collectionId)],
          [],
          [collectionId]);
    }

    var result = await SteamCollectionImportAdapter.ParseAsync(
        collectionId,
        _metadataClient,
        cancellationToken);

    return new ImportSourceLoadResult(
        result.Candidates,
        MapSteamDiagnostics(result.Diagnostics),
        [collectionId]);
  }

  private async Task<ImportSourceLoadResult> LoadSteamItemsAsync(
      ImportSourceRequest request,
      CancellationToken cancellationToken)
  {
    var tokens = TokenizeInput(request.InputText);
    var disclosedWorkshopIds = new List<string>();
    var diagnostics = new List<ImportTaskDiagnostic>();

    foreach (var token in tokens)
    {
      if (TryGetWorkshopItemId(token, out var workshopItemId))
      {
        disclosedWorkshopIds.Add(workshopItemId);
      }
      else
      {
        diagnostics.Add(new ImportTaskDiagnostic(
            "import.source.steam.item.invalid",
            "Steam item input must be a numeric Workshop ID or Workshop URL.",
            IsBlocking: true,
            SafeNextAction: "Remove or correct invalid tokens and try again."));
      }
    }

    if (!request.RequestMetadata)
    {
      var candidates = disclosedWorkshopIds
          .Select(workshopItemId => new SteamImportCandidate(workshopItemId))
          .Cast<object>()
          .ToArray();
      return new ImportSourceLoadResult(candidates, diagnostics, disclosedWorkshopIds);
    }

    var result = await SteamSingleItemImportAdapter.ParseAsync(
        request.InputText,
        _metadataClient,
        cancellationToken);

    return new ImportSourceLoadResult(
        result.Candidates,
        diagnostics.Concat(MapSteamDiagnostics(result.Diagnostics)).ToArray(),
        disclosedWorkshopIds);
  }

  private static List<string> TokenizeInput(string input) =>
      input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .ToList();

  private static bool IsNumericWorkshopId(string value) =>
      value.Length > 0 && value.All(char.IsAsciiDigit);

  private static bool TryGetWorkshopItemId(string sourceReference, out string workshopItemId)
  {
    if (sourceReference.All(char.IsAsciiDigit))
    {
      workshopItemId = sourceReference;
      return true;
    }

    if (Uri.TryCreate(sourceReference, UriKind.Absolute, out var uri) &&
        (uri.Host.Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("www.steamcommunity.com", StringComparison.OrdinalIgnoreCase)) &&
        (uri.AbsolutePath.Equals("/sharedfiles/filedetails/", StringComparison.OrdinalIgnoreCase) ||
         uri.AbsolutePath.Equals("/sharedfiles/filedetails", StringComparison.OrdinalIgnoreCase)))
    {
      var id = uri.Query.TrimStart('?').Split('&')
          .Select(parameter => parameter.Split('=', 2))
          .FirstOrDefault(parameter => parameter.Length == 2 && parameter[0].Equals("id", StringComparison.OrdinalIgnoreCase))?[1];
      if (!string.IsNullOrWhiteSpace(id) && id.All(char.IsAsciiDigit))
      {
        workshopItemId = id;
        return true;
      }
    }

    workshopItemId = string.Empty;
    return false;
  }

  private static ImportTaskDiagnostic CreateCollectionValidationDiagnostic() =>
      new(
          "import.source.steam.collection.invalid",
          "Steam collection input must contain exactly one numeric collection ID.",
          IsBlocking: true,
          SafeNextAction: "Enter one numeric collection ID and try again.");

  private static ImportTaskDiagnostic CreateLookupDiagnostic() =>
      new(
          "import.source.steam.lookup.failed",
          "Workshop metadata lookup failed.",
          IsBlocking: false,
          SafeNextAction: "Retry metadata lookup or resolve the item manually.");

  private static IReadOnlyList<ImportTaskDiagnostic> MapSteamDiagnostics(
      IReadOnlyList<SteamImportDiagnostic> diagnostics) =>
      diagnostics.Select(diagnostic => new ImportTaskDiagnostic(
          diagnostic.IsLookupFailure
              ? "import.source.steam.lookup.failed"
              : "import.source.steam.item.invalid",
          diagnostic.Message,
          IsBlocking: !diagnostic.IsLookupFailure,
          SafeNextAction: diagnostic.IsLookupFailure
              ? "Retry metadata lookup or resolve the item manually."
              : "Correct the source input and try again."))
          .ToArray();
}
