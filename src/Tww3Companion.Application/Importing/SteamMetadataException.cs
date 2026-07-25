namespace Tww3Companion.Application.Importing;

public sealed class SteamMetadataException : Exception
{
  public SteamMetadataException(string message)
      : base(message)
  {
  }

  public SteamMetadataException(string message, Exception innerException)
      : base(message, innerException)
  {
  }
}
