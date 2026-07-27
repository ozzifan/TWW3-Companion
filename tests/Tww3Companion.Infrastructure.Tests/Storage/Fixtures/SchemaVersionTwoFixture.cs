using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Storage;
using Tww3Companion.Infrastructure.Storage.Transfer;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage.Fixtures;

internal static class SchemaVersionTwoFixture
{
  public const string WorkspaceUuid = "12345678-1234-4abc-8def-1234567890ab";
  public const string ModAId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
  public const string ModBId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
  public const string CollectionId = "cccccccc-cccc-cccc-cccc-cccccccccccc";

  public static async Task CreatePopulatedAsync(string path)
  {
    var id = WorkspaceId.Parse(WorkspaceUuid) is ValidationResult<WorkspaceId>.Success parsedId
        ? parsedId.Value
        : throw new InvalidOperationException("Invalid workspace id.");
    var name = WorkspaceName.Create("Fixture Workspace") is ValidationResult<WorkspaceName>.Success parsedName
        ? parsedName.Value
        : throw new InvalidOperationException("Invalid workspace name.");
    var workspace = Workspace.Create(
        id,
        name,
        DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
        DateTimeOffset.Parse("2026-07-25T11:00:00Z")) is ValidationResult<Workspace>.Success parsedWorkspace
        ? parsedWorkspace.Value
        : throw new InvalidOperationException("Invalid workspace.");

    await new SqliteWorkspaceStore().CreateAsync(path, workspace, CancellationToken.None);

    await using var connection = await new SqliteConnectionFactory().OpenAsync(path, CancellationToken.None);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        INSERT INTO mods (id, display_name) VALUES
          ($modA, 'Alpha'),
          ($modB, 'Beta');
        INSERT INTO source_references (source_type, external_id, mod_id) VALUES
          ('steam-workshop', '1', $modA),
          ('steam-workshop', '2', $modB);
        INSERT INTO collections (id, display_name) VALUES
          ($collection, 'Collection');
        INSERT INTO collection_memberships (collection_id, mod_id, position) VALUES
          ($collection, $modA, 0),
          ($collection, $modB, 2);
        """;
    command.Parameters.AddWithValue("$modA", ModAId);
    command.Parameters.AddWithValue("$modB", ModBId);
    command.Parameters.AddWithValue("$collection", CollectionId);
    await command.ExecuteNonQueryAsync();
  }
}
