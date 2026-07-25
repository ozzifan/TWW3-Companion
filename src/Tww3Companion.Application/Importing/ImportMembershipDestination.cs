namespace Tww3Companion.Application.Importing;

public abstract record ImportMembershipDestination
{
  private ImportMembershipDestination()
  {
  }

  public sealed record LibraryOnly : ImportMembershipDestination;

  public sealed record ExistingCollection(string CollectionId)
      : ImportMembershipDestination;

  public sealed record NewCollection(string DisplayName)
      : ImportMembershipDestination;

  public static ImportMembershipDestination ForLibraryOnly() => new LibraryOnly();

  public static ImportMembershipDestination ForExistingCollection(string collectionId) =>
      new ExistingCollection(collectionId);

  public static ImportMembershipDestination ForNewCollection(string displayName) =>
      new NewCollection(displayName);
}
