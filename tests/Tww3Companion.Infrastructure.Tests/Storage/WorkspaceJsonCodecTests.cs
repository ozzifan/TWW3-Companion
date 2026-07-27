using System.Text;
using Json.Schema;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Infrastructure.Storage.Transfer;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class WorkspaceJsonCodecTests
{
  private static readonly string SchemaPath = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory,
      "..", "..", "..", "..", "..",
      "schemas",
      "workspace-export-v1.schema.json"));

  [Fact]
  public void Serialize_ProducesDeterministicUtf8WithoutBomAndTrailingNewline()
  {
    var snapshot = CreateSnapshot();

    var first = AssertSuccess(WorkspaceJsonCodec.Serialize(snapshot));
    var second = AssertSuccess(WorkspaceJsonCodec.Serialize(snapshot));

    Assert.Equal(first, second);
    Assert.Equal('{', first[0]);
    Assert.EndsWith("\n", first);
    Assert.Equal(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(first));
    Assert.Contains("\"format\": \"workspace-export-v1\"", first, StringComparison.Ordinal);
    Assert.Contains("\"sourceReferences\"", first, StringComparison.Ordinal);
    Assert.DoesNotContain("schema_migrations", first, StringComparison.Ordinal);
    Assert.DoesNotContain("application_metadata", first, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Serialize_ValidSnapshot_MatchesPublicSchema()
  {
    var serialized = AssertSuccess(WorkspaceJsonCodec.Serialize(CreateSnapshot()));
    var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath, TestContext.Current.CancellationToken));
    using var document = System.Text.Json.JsonDocument.Parse(serialized);
    var result = schema.Evaluate(
        document.RootElement,
        new EvaluationOptions
        {
          OutputFormat = OutputFormat.List,
          RequireFormatValidation = true
        });

    Assert.True(result.IsValid, result.ToString());
  }

  [Fact]
  public void Deserialize_RejectsMalformedJson()
  {
    var result = WorkspaceJsonCodec.Deserialize("{not-json");

    Assert.IsType<Application.Common.OperationResult<WorkspaceTransferSnapshot>.Failure>(result);
  }

  [Fact]
  public void Deserialize_RejectsBomFollowedByInvalidText()
  {
    var result = WorkspaceJsonCodec.Deserialize("\uFEFF{not-json");

    Assert.IsType<Application.Common.OperationResult<WorkspaceTransferSnapshot>.Failure>(result);
  }

  [Fact]
  public void Deserialize_RejectsUnsupportedFormat()
  {
    var json = """
      {
        "format": "workspace-export-v2",
        "workspace": {
          "id": "11111111-1111-1111-1111-111111111111",
          "displayName": "My Workspace",
          "createdUtc": "2026-07-25T10:00:00Z",
          "modifiedUtc": "2026-07-25T11:00:00Z"
        },
        "mods": [],
        "sourceReferences": [],
        "collections": [],
        "memberships": []
      }
      """;

    var result = WorkspaceJsonCodec.Deserialize(json);

    var failure = Assert.IsType<Application.Common.OperationResult<WorkspaceTransferSnapshot>.Failure>(result);
    Assert.Equal("workspace.transfer.format.unsupported", failure.Error.Code);
  }

  [Fact]
  public void Deserialize_RejectsUnknownProperty()
  {
    var json = AssertSuccess(WorkspaceJsonCodec.Serialize(CreateSnapshot()))
        .Replace("\"memberships\"", "\"extra\": 1,\n  \"memberships\"", StringComparison.Ordinal);

    var result = WorkspaceJsonCodec.Deserialize(json);

    Assert.IsType<Application.Common.OperationResult<WorkspaceTransferSnapshot>.Failure>(result);
  }

  [Fact]
  public void Deserialize_RejectsTrailingContent()
  {
    var json = $"{AssertSuccess(WorkspaceJsonCodec.Serialize(CreateSnapshot())).TrimEnd()}\n{{}}";

    var result = WorkspaceJsonCodec.Deserialize(json);

    Assert.IsType<Application.Common.OperationResult<WorkspaceTransferSnapshot>.Failure>(result);
  }

  [Fact]
  public void RoundTrip_PreservesSnapshot()
  {
    var snapshot = CreateSnapshot();
    var serialized = AssertSuccess(WorkspaceJsonCodec.Serialize(snapshot));
    var restored = WorkspaceJsonCodec.Deserialize(serialized);
    var success = restored as Application.Common.OperationResult<WorkspaceTransferSnapshot>.Success
        ?? throw new Xunit.Sdk.XunitException(
            ((Application.Common.OperationResult<WorkspaceTransferSnapshot>.Failure)restored).Error.Code);

    Assert.True(WorkspaceTransferValidation.ContentEquals(snapshot, success.Value));
  }

  private static string AssertSuccess(Application.Common.OperationResult<string> result) =>
      Assert.IsType<Application.Common.OperationResult<string>.Success>(result).Value;

  private static WorkspaceTransferSnapshot CreateSnapshot() =>
      new(
          "workspace-export-v1",
          new WorkspaceTransferWorkspace(
              "11111111-1111-4111-8111-111111111111",
              "My Workspace",
              DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-25T11:00:00Z")),
          [
            new("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", "Alpha"),
            new("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb", "Beta")
          ],
          [
            new("steam-workshop", "1", "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            new("steam-workshop", "2", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb")
          ],
          [
            new("cccccccc-cccc-4ccc-8ccc-cccccccccccc", "Collection")
          ],
          [
            new("cccccccc-cccc-4ccc-8ccc-cccccccccccc", "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", 0),
            new("cccccccc-cccc-4ccc-8ccc-cccccccccccc", "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb", 1)
          ]);
}
