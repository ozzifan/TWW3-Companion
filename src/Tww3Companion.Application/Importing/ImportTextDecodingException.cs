namespace Tww3Companion.Application.Importing;

public sealed class ImportTextDecodingException(string code, string message) : Exception(message)
{
  public string Code { get; } = code;
}
