using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class ImportPreviewViewModelTests
{
  [Fact]
  public void Default_filter_shows_all_rows()
  {
    var subject = CreateSubject(CreatePreview());

    Assert.Equal(ImportPreviewFilter.All, subject.SelectedFilter);
    Assert.Equal(6, subject.FilteredRows.Count);
  }

  [Fact]
  public void Filters_use_operation_outcomes_not_string_comparisons()
  {
    var subject = CreateSubject(CreatePreview());

    subject.SelectedFilter = ImportPreviewFilter.Additions;
    Assert.Single(subject.FilteredRows);
    Assert.Equal(ImportLibraryAction.Create, subject.FilteredRows[0].LibraryAction);

    subject.SelectedFilter = ImportPreviewFilter.Enrichments;
    Assert.Single(subject.FilteredRows);
    Assert.Equal(ImportLibraryAction.Enrich, subject.FilteredRows[0].LibraryAction);

    subject.SelectedFilter = ImportPreviewFilter.Existing;
    Assert.Single(subject.FilteredRows);
    Assert.Equal(ImportLibraryAction.Existing, subject.FilteredRows[0].LibraryAction);

    subject.SelectedFilter = ImportPreviewFilter.SuggestedMatches;
    Assert.Single(subject.FilteredRows);
    Assert.Equal(ImportLibraryAction.SuggestedMatch, subject.FilteredRows[0].LibraryAction);

    subject.SelectedFilter = ImportPreviewFilter.Conflicts;
    Assert.Single(subject.FilteredRows);
    Assert.Equal(ImportLibraryAction.Conflict, subject.FilteredRows[0].LibraryAction);

    subject.SelectedFilter = ImportPreviewFilter.Warnings;
    Assert.Single(subject.FilteredRows);
    Assert.True(subject.FilteredRows[0].HasWarning);

    subject.SelectedFilter = ImportPreviewFilter.Skipped;
    Assert.Single(subject.FilteredRows);
    Assert.True(subject.FilteredRows[0].IsSkipped);
  }

  [Fact]
  public void Confirmation_summary_contains_exact_counts_and_warnings_remaining()
  {
    var subject = CreateSubject(CreatePreview());

    var summary = subject.BuildConfirmationSummary();

    Assert.Equal(1, summary.ModsCreated);
    Assert.Equal(1, summary.ModsEnriched);
    Assert.Equal(1, summary.ExistingModsUnchanged);
    Assert.Equal(0, summary.CollectionsCreated);
    Assert.Equal(0, summary.MembershipsAdded);
    Assert.Equal(0, summary.ExistingMembershipsUnchanged);
    Assert.Equal(1, summary.CandidatesSkipped);
    Assert.Equal(1, summary.WarningsRemaining);
  }

  [Fact]
  public void CanContinue_is_false_while_blocking_operations_remain()
  {
    var preview = CreatePreview() with
    {
      ValidationIssues =
      [
          new ImportValidationIssue(
              "conflict-1",
              "import.source.owner.conflict",
              "The source identity is already owned by Mod owner-mod.")
      ]
    };

    var subject = CreateSubject(preview);

    Assert.False(subject.CanContinue);
  }

  private static ImportPreviewViewModel CreateSubject(ImportPreview preview)
  {
    var subject = new ImportPreviewViewModel();
    subject.Load(preview);
    return subject;
  }

  private static ImportPreview CreatePreview()
  {
    var target = ImportTargetContext.ForCurrentWorkspace(
        "workspace-1",
        @"C:\Data\workspace.tww3c",
        ImportMembershipDestination.ForLibraryOnly());

    return new ImportPreview(
        target,
        [
            ImportCandidate.CreateWithDisplayName("create-1", "Create Mod"),
            ImportCandidate.CreateWithDisplayName("enrich-1", "Enrich Mod"),
            ImportCandidate.Linked("existing-1", "existing-mod"),
            ImportCandidate.CreateWithDisplayName("suggested-1", "Suggested Mod") with { SuggestedModId = "mod-1" },
            ImportCandidate.CreateWithDisplayName("conflict-1", "Conflict Mod"),
            ImportCandidate.Skipped("skipped-1")
        ],
        Applied: false,
        Operations:
        [
            new ImportPreviewOperation("create-1", ImportLibraryAction.Create, ImportMembershipAction.None, []),
            new ImportPreviewOperation("enrich-1", ImportLibraryAction.Enrich, ImportMembershipAction.None, []),
            new ImportPreviewOperation("existing-1", ImportLibraryAction.Existing, ImportMembershipAction.None, []),
            new ImportPreviewOperation("suggested-1", ImportLibraryAction.SuggestedMatch, ImportMembershipAction.None, []),
            new ImportPreviewOperation(
                "conflict-1",
                ImportLibraryAction.Conflict,
                ImportMembershipAction.Blocked,
                [new ImportValidationIssue("conflict-1", "import.scalar.conflict", "Display name differs.")]),
            new ImportPreviewOperation("skipped-1", ImportLibraryAction.Skip, ImportMembershipAction.Skip, [])
        ],
        ValidationIssues:
        [
            new ImportValidationIssue("create-1", "import.preview.warning", "Non-blocking warning.")
        ],
        WarningCount: 1);
  }
}
