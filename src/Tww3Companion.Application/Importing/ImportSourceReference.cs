namespace Tww3Companion.Application.Importing;

public enum ImportSourceType
{
  SteamWorkshop
}

public sealed record ImportSourceReference(
    ImportSourceType SourceType,
    string ExternalId)
{
  public static ImportSourceReference SteamWorkshop(string workshopItemId)
  {
    if (string.IsNullOrWhiteSpace(workshopItemId) ||
        !workshopItemId.All(char.IsDigit))
    {
      throw new ArgumentException(
          "A Steam Workshop source reference requires a numeric item ID.",
          nameof(workshopItemId));
    }

    return new(
        ImportSourceType.SteamWorkshop,
        workshopItemId.Trim());
  }
}
