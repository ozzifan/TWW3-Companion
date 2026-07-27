using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;

namespace Tww3Companion.Infrastructure.Storage.Transfer;

public static class WorkspaceJsonCodec
{
  private static readonly JsonSerializerOptions Options = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
  };

  public static OperationResult<string> Serialize(WorkspaceTransferSnapshot snapshot)
  {
    var validationErrors = WorkspaceTransferValidation.Validate(snapshot);
    if (validationErrors.Count > 0)
    {
      return SerializeFailure(validationErrors[0]);
    }

    var document = ToDocument(Canonicalize(snapshot));
    var json = JsonSerializer.Serialize(document, Options);
    return new OperationResult<string>.Success($"{json}\n");
  }

  public static OperationResult<WorkspaceTransferSnapshot> Deserialize(string json)
  {
    if (json.Length > 0 && json[0] == '\uFEFF')
    {
      return Failure("workspace.transfer.format.unsupported", "The export text encoding is invalid.");
    }

    try
    {
      var trimmed = json.Trim();
      if (trimmed.Length == 0)
      {
        return Failure("workspace.transfer.format.unsupported", "The export is not valid JSON.");
      }

      if (HasTrailingContent(trimmed))
      {
        return Failure("workspace.transfer.format.unsupported", "The export contains trailing content.");
      }

      var document = JsonSerializer.Deserialize<WorkspaceExportDocument>(trimmed, Options);
      if (document is null)
      {
        return Failure("workspace.transfer.format.unsupported", "The export is not valid JSON.");
      }

      var snapshot = FromDocument(document);
      var validationErrors = WorkspaceTransferValidation.Validate(snapshot);
      if (validationErrors.Count > 0)
      {
        return SnapshotFailure(validationErrors[0]);
      }

      return new OperationResult<WorkspaceTransferSnapshot>.Success(snapshot);
    }
    catch (JsonException)
    {
      return Failure("workspace.transfer.format.unsupported", "The export is not valid JSON.");
    }
  }

  private static WorkspaceTransferSnapshot Canonicalize(WorkspaceTransferSnapshot snapshot) =>
      new(
          snapshot.Format,
          snapshot.Workspace,
          snapshot.Mods.OrderBy(mod => mod.Id, StringComparer.Ordinal).ToArray(),
          snapshot.SourceReferences
              .OrderBy(reference => reference.SourceType, StringComparer.Ordinal)
              .ThenBy(reference => reference.ExternalId, StringComparer.Ordinal)
              .ToArray(),
          snapshot.Collections.OrderBy(collection => collection.Id, StringComparer.Ordinal).ToArray(),
          snapshot.Memberships
              .OrderBy(membership => membership.CollectionId, StringComparer.Ordinal)
              .ThenBy(membership => membership.Position)
              .ThenBy(membership => membership.ModId, StringComparer.Ordinal)
              .ToArray());

  private static WorkspaceExportDocument ToDocument(WorkspaceTransferSnapshot snapshot) =>
      new()
      {
        Format = snapshot.Format,
        Workspace = new WorkspaceExportWorkspaceDocument
        {
          Id = snapshot.Workspace.Id,
          DisplayName = snapshot.Workspace.DisplayName,
          CreatedUtc = snapshot.Workspace.CreatedUtc,
          ModifiedUtc = snapshot.Workspace.ModifiedUtc
        },
        Mods = snapshot.Mods.Select(mod => new WorkspaceExportModDocument
        {
          Id = mod.Id,
          DisplayName = mod.DisplayName
        }).ToArray(),
        SourceReferences = snapshot.SourceReferences.Select(reference => new WorkspaceExportSourceReferenceDocument
        {
          SourceType = reference.SourceType,
          ExternalId = reference.ExternalId,
          ModId = reference.ModId
        }).ToArray(),
        Collections = snapshot.Collections.Select(collection => new WorkspaceExportCollectionDocument
        {
          Id = collection.Id,
          DisplayName = collection.DisplayName
        }).ToArray(),
        Memberships = snapshot.Memberships.Select(membership => new WorkspaceExportMembershipDocument
        {
          CollectionId = membership.CollectionId,
          ModId = membership.ModId,
          Position = membership.Position
        }).ToArray()
      };

  private static WorkspaceTransferSnapshot FromDocument(WorkspaceExportDocument document) =>
      new(
          document.Format ?? string.Empty,
          new WorkspaceTransferWorkspace(
              document.Workspace?.Id ?? string.Empty,
              document.Workspace?.DisplayName ?? string.Empty,
              document.Workspace?.CreatedUtc ?? default,
              document.Workspace?.ModifiedUtc ?? default),
          document.Mods?.Select(mod => new WorkspaceTransferMod(mod.Id ?? string.Empty, mod.DisplayName ?? string.Empty)).ToArray() ?? [],
          document.SourceReferences?.Select(reference => new WorkspaceTransferSourceReference(
              reference.SourceType ?? string.Empty,
              reference.ExternalId ?? string.Empty,
              reference.ModId ?? string.Empty)).ToArray() ?? [],
          document.Collections?.Select(collection => new WorkspaceTransferCollection(
              collection.Id ?? string.Empty,
              collection.DisplayName ?? string.Empty)).ToArray() ?? [],
          document.Memberships?.Select(membership => new WorkspaceTransferMembership(
              membership.CollectionId ?? string.Empty,
              membership.ModId ?? string.Empty,
              membership.Position)).ToArray() ?? []);

  private static bool HasTrailingContent(string trimmed)
  {
    var depth = 0;
    var inString = false;
    var escaped = false;
    for (var index = 0; index < trimmed.Length; index++)
    {
      var character = trimmed[index];
      if (inString)
      {
        if (escaped)
        {
          escaped = false;
          continue;
        }

        if (character == '\\')
        {
          escaped = true;
          continue;
        }

        if (character == '"')
        {
          inString = false;
        }

        continue;
      }

      if (character == '"')
      {
        inString = true;
        continue;
      }

      if (character == '{')
      {
        depth++;
        continue;
      }

      if (character == '}')
      {
        depth--;
        if (depth == 0)
        {
          for (var trailing = index + 1; trailing < trimmed.Length; trailing++)
          {
            if (!char.IsWhiteSpace(trimmed[trailing]))
            {
              return true;
            }
          }

          return false;
        }
      }
    }

    return false;
  }

  private static OperationResult<string>.Failure SerializeFailure(OperationError error) =>
      new(error);

  private static OperationResult<WorkspaceTransferSnapshot>.Failure SnapshotFailure(OperationError error) =>
      new(error);

  private static OperationResult<WorkspaceTransferSnapshot>.Failure Failure(string code, string message) =>
      new(new OperationError(code, message, false, "Choose a different export and try again."));

  private sealed class WorkspaceExportDocument
  {
    public string? Format { get; init; }
    public WorkspaceExportWorkspaceDocument? Workspace { get; init; }
    public WorkspaceExportModDocument[]? Mods { get; init; }
    public WorkspaceExportSourceReferenceDocument[]? SourceReferences { get; init; }
    public WorkspaceExportCollectionDocument[]? Collections { get; init; }
    public WorkspaceExportMembershipDocument[]? Memberships { get; init; }
  }

  private sealed class WorkspaceExportWorkspaceDocument
  {
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public DateTimeOffset ModifiedUtc { get; init; }
  }

  private sealed class WorkspaceExportModDocument
  {
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
  }

  private sealed class WorkspaceExportSourceReferenceDocument
  {
    public string? SourceType { get; init; }
    public string? ExternalId { get; init; }
    public string? ModId { get; init; }
  }

  private sealed class WorkspaceExportCollectionDocument
  {
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
  }

  private sealed class WorkspaceExportMembershipDocument
  {
    public string? CollectionId { get; init; }
    public string? ModId { get; init; }
    public int Position { get; init; }
  }
}
