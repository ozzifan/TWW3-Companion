using System.Globalization;
using Tww3Companion.Application.Common;

namespace Tww3Companion.Application.Workspaces.Transfer;

public static class WorkspaceTransferValidation
{
  public const string SupportedFormat = "workspace-export-v1";

  public static IReadOnlyList<OperationError> Validate(WorkspaceTransferSnapshot snapshot)
  {
    var errors = new List<OperationError>();
    if (snapshot.Format != SupportedFormat)
    {
      errors.Add(Error(
          "workspace.transfer.format.unsupported",
          "The export format is not supported."));
      return errors;
    }

    ValidateWorkspace(snapshot.Workspace, errors);
    ValidateMods(snapshot.Mods, errors);
    ValidateSourceReferences(snapshot.SourceReferences, snapshot.Mods, errors);
    ValidateCollections(snapshot.Collections, errors);
    ValidateMemberships(snapshot.Memberships, snapshot.Mods, snapshot.Collections, errors);
    return errors;
  }

  public static bool ContentEquals(WorkspaceTransferSnapshot? left, WorkspaceTransferSnapshot? right)
  {
    if (left is null || right is null)
    {
      return left is null && right is null;
    }

    if (!string.Equals(left.Format, right.Format, StringComparison.Ordinal))
    {
      return false;
    }

    if (!WorkspaceEquals(left.Workspace, right.Workspace))
    {
      return false;
    }

    return SequenceEqual(left.Mods, right.Mods, ModEquals) &&
           SequenceEqual(left.SourceReferences, right.SourceReferences, SourceReferenceEquals) &&
           SequenceEqual(left.Collections, right.Collections, CollectionEquals) &&
           SequenceEqual(left.Memberships, right.Memberships, MembershipEquals);
  }

  private static void ValidateWorkspace(WorkspaceTransferWorkspace workspace, List<OperationError> errors)
  {
    if (!IsCanonicalUuid(workspace.Id))
    {
      errors.Add(Error("workspace.transfer.identity.invalid", "The Workspace identity is invalid."));
    }

    if (!IsNonBlankDisplayName(workspace.DisplayName))
    {
      errors.Add(Error("workspace.transfer.identity.invalid", "The Workspace display name is invalid."));
    }

    if (workspace.ModifiedUtc < workspace.CreatedUtc)
    {
      errors.Add(Error("workspace.transfer.identity.invalid", "The Workspace timestamps are invalid."));
    }
  }

  private static void ValidateMods(IReadOnlyList<WorkspaceTransferMod> mods, List<OperationError> errors)
  {
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var mod in mods)
    {
      if (!IsCanonicalUuid(mod.Id))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Mod identity is invalid."));
        continue;
      }

      if (!IsNonBlankDisplayName(mod.DisplayName))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Mod display name is invalid."));
      }

      if (!seen.Add(mod.Id))
      {
        errors.Add(Error("workspace.transfer.identity.duplicate", "A Mod identity is duplicated."));
      }
    }
  }

  private static void ValidateSourceReferences(
      IReadOnlyList<WorkspaceTransferSourceReference> sourceReferences,
      IReadOnlyList<WorkspaceTransferMod> mods,
      List<OperationError> errors)
  {
    var modIds = mods.Select(mod => mod.Id).ToHashSet(StringComparer.Ordinal);
    var seen = new HashSet<(string SourceType, string ExternalId)>();
    foreach (var reference in sourceReferences)
    {
      if (string.IsNullOrWhiteSpace(reference.SourceType))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Source Reference type is invalid."));
      }

      if (string.IsNullOrWhiteSpace(reference.ExternalId?.Trim()))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Source Reference external identity is invalid."));
      }

      if (!IsCanonicalUuid(reference.ModId))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Source Reference Mod identity is invalid."));
      }
      else if (!modIds.Contains(reference.ModId))
      {
        errors.Add(Error("workspace.transfer.reference.missing", "A Source Reference refers to a missing Mod."));
      }

      var key = (reference.SourceType, reference.ExternalId ?? string.Empty);
      if (!seen.Add(key))
      {
        errors.Add(Error("workspace.transfer.identity.duplicate", "A Source Reference identity is duplicated."));
      }
    }
  }

  private static void ValidateCollections(
      IReadOnlyList<WorkspaceTransferCollection> collections,
      List<OperationError> errors)
  {
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var collection in collections)
    {
      if (!IsCanonicalUuid(collection.Id))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Collection identity is invalid."));
        continue;
      }

      if (!IsNonBlankDisplayName(collection.DisplayName))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Collection display name is invalid."));
      }

      if (!seen.Add(collection.Id))
      {
        errors.Add(Error("workspace.transfer.identity.duplicate", "A Collection identity is duplicated."));
      }
    }
  }

  private static void ValidateMemberships(
      IReadOnlyList<WorkspaceTransferMembership> memberships,
      IReadOnlyList<WorkspaceTransferMod> mods,
      IReadOnlyList<WorkspaceTransferCollection> collections,
      List<OperationError> errors)
  {
    var modIds = mods.Select(mod => mod.Id).ToHashSet(StringComparer.Ordinal);
    var collectionIds = collections.Select(collection => collection.Id).ToHashSet(StringComparer.Ordinal);
    var seenMemberships = new HashSet<(string CollectionId, string ModId)>();
    var positionsByCollection = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

    foreach (var membership in memberships)
    {
      if (!IsCanonicalUuid(membership.CollectionId))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Membership Collection identity is invalid."));
      }
      else if (!collectionIds.Contains(membership.CollectionId))
      {
        errors.Add(Error("workspace.transfer.reference.missing", "A Membership refers to a missing Collection."));
      }

      if (!IsCanonicalUuid(membership.ModId))
      {
        errors.Add(Error("workspace.transfer.identity.invalid", "A Membership Mod identity is invalid."));
      }
      else if (!modIds.Contains(membership.ModId))
      {
        errors.Add(Error("workspace.transfer.reference.missing", "A Membership refers to a missing Mod."));
      }

      if (membership.Position < 0)
      {
        errors.Add(Error("workspace.transfer.position.invalid", "A Membership position is invalid."));
      }
      else if (IsCanonicalUuid(membership.CollectionId))
      {
        if (!positionsByCollection.TryGetValue(membership.CollectionId, out var positions))
        {
          positions = [];
          positionsByCollection[membership.CollectionId] = positions;
        }

        if (!positions.Add(membership.Position))
        {
          errors.Add(Error("workspace.transfer.position.invalid", "A Membership position is duplicated within a Collection."));
        }
      }

      if (!seenMemberships.Add((membership.CollectionId, membership.ModId)))
      {
        errors.Add(Error("workspace.transfer.identity.duplicate", "A Membership identity is duplicated."));
      }
    }
  }

  private static bool WorkspaceEquals(WorkspaceTransferWorkspace left, WorkspaceTransferWorkspace right) =>
      string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
      string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
      left.CreatedUtc == right.CreatedUtc &&
      left.ModifiedUtc == right.ModifiedUtc;

  private static bool ModEquals(WorkspaceTransferMod left, WorkspaceTransferMod right) =>
      string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
      string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal);

  private static bool SourceReferenceEquals(
      WorkspaceTransferSourceReference left,
      WorkspaceTransferSourceReference right) =>
      string.Equals(left.SourceType, right.SourceType, StringComparison.Ordinal) &&
      string.Equals(left.ExternalId, right.ExternalId, StringComparison.Ordinal) &&
      string.Equals(left.ModId, right.ModId, StringComparison.Ordinal);

  private static bool CollectionEquals(WorkspaceTransferCollection left, WorkspaceTransferCollection right) =>
      string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
      string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal);

  private static bool MembershipEquals(WorkspaceTransferMembership left, WorkspaceTransferMembership right) =>
      string.Equals(left.CollectionId, right.CollectionId, StringComparison.Ordinal) &&
      string.Equals(left.ModId, right.ModId, StringComparison.Ordinal) &&
      left.Position == right.Position;

  private static bool SequenceEqual<T>(
      IReadOnlyList<T> left,
      IReadOnlyList<T> right,
      Func<T, T, bool> equals)
  {
    if (left.Count != right.Count)
    {
      return false;
    }

    for (var index = 0; index < left.Count; index++)
    {
      if (!equals(left[index], right[index]))
      {
        return false;
      }
    }

    return true;
  }

  private static bool IsCanonicalUuid(string value) =>
      Guid.TryParseExact(value, "D", out var parsed) &&
      value == parsed.ToString("D", CultureInfo.InvariantCulture);

  private static bool IsNonBlankDisplayName(string value) =>
      !string.IsNullOrWhiteSpace(value?.Trim());

  private static OperationError Error(string code, string message) =>
      new(code, message, false, "Choose a different export and try again.");
}
