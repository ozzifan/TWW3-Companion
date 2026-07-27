using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class WorkspaceTransferValidationTests
{
  [Fact]
  public void Validate_ValidPopulatedSnapshot_ReturnsNoErrors()
  {
    var errors = WorkspaceTransferValidation.Validate(ValidSnapshot());

    Assert.Empty(errors);
  }

  [Fact]
  public void Validate_ValidSnapshotWithPositionGaps_ReturnsNoErrors()
  {
    var snapshot = ValidSnapshot() with
    {
      Memberships =
      [
        new("33333333-3333-3333-3333-333333333333", "22222222-2222-2222-2222-222222222222", 0),
        new("33333333-3333-3333-3333-333333333333", "44444444-4444-4444-4444-444444444444", 2),
        new("33333333-3333-3333-3333-333333333333", "55555555-5555-5555-5555-555555555555", 4)
      ],
      Mods =
      [
        new("22222222-2222-2222-2222-222222222222", "Mod A"),
        new("44444444-4444-4444-4444-444444444444", "Mod B"),
        new("55555555-5555-5555-5555-555555555555", "Mod C")
      ]
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Empty(errors);
  }

  [Fact]
  public void Validate_UnsupportedFormat_ReturnsFormatError()
  {
    var errors = WorkspaceTransferValidation.Validate(ValidSnapshot() with { Format = "workspace-export-v2" });

    Assert.Contains(errors, error => error.Code == "workspace.transfer.format.unsupported");
  }

  [Fact]
  public void Validate_NonCanonicalWorkspaceUuid_ReturnsIdentityError()
  {
    var snapshot = ValidSnapshot() with
    {
      Workspace = ValidSnapshot().Workspace with { Id = "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA" }
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Contains(errors, error => error.Code == "workspace.transfer.identity.invalid");
  }

  [Fact]
  public void Validate_DuplicateModIds_ReturnsDuplicateError()
  {
    var snapshot = ValidSnapshot() with
    {
      Mods =
      [
        new("22222222-2222-2222-2222-222222222222", "Mod A"),
        new("22222222-2222-2222-2222-222222222222", "Mod B")
      ]
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Contains(errors, error => error.Code == "workspace.transfer.identity.duplicate");
  }

  [Fact]
  public void Validate_DuplicateCollectionIds_ReturnsDuplicateError()
  {
    var snapshot = ValidSnapshot() with
    {
      Collections =
      [
        new("33333333-3333-3333-3333-333333333333", "Collection A"),
        new("33333333-3333-3333-3333-333333333333", "Collection B")
      ]
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Contains(errors, error => error.Code == "workspace.transfer.identity.duplicate");
  }

  [Fact]
  public void Validate_DuplicateSourceReferenceIdentity_ReturnsDuplicateError()
  {
    var snapshot = ValidSnapshot() with
    {
      SourceReferences =
      [
        new("steam-workshop", "1234567890", "22222222-2222-2222-2222-222222222222"),
        new("steam-workshop", "1234567890", "22222222-2222-2222-2222-222222222222")
      ]
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Contains(errors, error => error.Code == "workspace.transfer.identity.duplicate");
  }

  [Fact]
  public void Validate_MissingReferencedMod_ReturnsReferenceError()
  {
    var snapshot = ValidSnapshot() with
    {
      SourceReferences =
      [
        new("steam-workshop", "1234567890", "99999999-9999-9999-9999-999999999999")
      ]
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Contains(errors, error => error.Code == "workspace.transfer.reference.missing");
  }

  [Fact]
  public void Validate_DuplicateMembershipIdentity_ReturnsDuplicateError()
  {
    var snapshot = ValidSnapshot() with
    {
      Memberships =
      [
        new("33333333-3333-3333-3333-333333333333", "22222222-2222-2222-2222-222222222222", 0),
        new("33333333-3333-3333-3333-333333333333", "22222222-2222-2222-2222-222222222222", 1)
      ]
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Contains(errors, error => error.Code == "workspace.transfer.identity.duplicate");
  }

  [Fact]
  public void Validate_NegativePosition_ReturnsPositionError()
  {
    var snapshot = ValidSnapshot() with
    {
      Memberships =
      [
        new("33333333-3333-3333-3333-333333333333", "22222222-2222-2222-2222-222222222222", -1)
      ]
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Contains(errors, error => error.Code == "workspace.transfer.position.invalid");
  }

  [Fact]
  public void Validate_DuplicatePositionWithinCollection_ReturnsPositionError()
  {
    var snapshot = ValidSnapshot() with
    {
      Mods =
      [
        new("22222222-2222-2222-2222-222222222222", "Mod A"),
        new("44444444-4444-4444-4444-444444444444", "Mod B")
      ],
      Memberships =
      [
        new("33333333-3333-3333-3333-333333333333", "22222222-2222-2222-2222-222222222222", 0),
        new("33333333-3333-3333-3333-333333333333", "44444444-4444-4444-4444-444444444444", 0)
      ]
    };

    var errors = WorkspaceTransferValidation.Validate(snapshot);

    Assert.Contains(errors, error => error.Code == "workspace.transfer.position.invalid");
  }

  [Fact]
  public void ContentEquals_DifferentListInstancesWithSameValues_ReturnsTrue()
  {
    var left = ValidSnapshot();
    var right = ValidSnapshot() with
    {
      Mods = [new("22222222-2222-2222-2222-222222222222", "Mod A")]
    };

    Assert.True(WorkspaceTransferValidation.ContentEquals(left, right));
  }

  [Fact]
  public void ContentEquals_DifferentValues_ReturnsFalse()
  {
    var left = ValidSnapshot();
    var right = ValidSnapshot() with
    {
      Workspace = left.Workspace with { DisplayName = "Changed" }
    };

    Assert.False(WorkspaceTransferValidation.ContentEquals(left, right));
  }

  private static WorkspaceTransferSnapshot ValidSnapshot() => new(
      Format: "workspace-export-v1",
      Workspace: new WorkspaceTransferWorkspace(
          "11111111-1111-1111-1111-111111111111",
          "My Workspace",
          DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
          DateTimeOffset.Parse("2026-07-25T11:00:00Z")),
      Mods:
      [
        new("22222222-2222-2222-2222-222222222222", "Mod A")
      ],
      SourceReferences:
      [
        new("steam-workshop", "1234567890", "22222222-2222-2222-2222-222222222222")
      ],
      Collections:
      [
        new("33333333-3333-3333-3333-333333333333", "Collection A")
      ],
      Memberships:
      [
        new("33333333-3333-3333-3333-333333333333", "22222222-2222-2222-2222-222222222222", 0)
      ]);
}
