namespace Tww3Companion.Application.Importing;

public enum ImportLibraryAction
{
  Create,
  Enrich,
  Existing,
  SuggestedMatch,
  Conflict,
  Skip
}

public enum ImportMembershipAction
{
  None,
  Add,
  Existing,
  Blocked,
  Skip
}

public sealed record ImportPreviewOperation(
    string CandidateId,
    ImportLibraryAction LibraryAction,
    ImportMembershipAction MembershipAction,
    IReadOnlyList<ImportValidationIssue> Issues);
