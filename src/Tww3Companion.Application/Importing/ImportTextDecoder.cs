using System.Text;

namespace Tww3Companion.Application.Importing;

public static class ImportTextDecoder
{
  private const string UnsupportedEncodingCode = "import.source.encoding.unsupported";

  private static readonly UTF8Encoding Utf8 = new(
      encoderShouldEmitUTF8Identifier: false,
      throwOnInvalidBytes: true);
  private static readonly UnicodeEncoding Utf16LittleEndian = new(
      bigEndian: false,
      byteOrderMark: true,
      throwOnInvalidBytes: true);
  private static readonly UnicodeEncoding Utf16BigEndian = new(
      bigEndian: true,
      byteOrderMark: true,
      throwOnInvalidBytes: true);

  public static string Decode(ReadOnlySpan<byte> bytes)
  {
    if (bytes.IsEmpty)
    {
      throw new ImportTextDecodingException(
          UnsupportedEncodingCode,
          "Import source is empty.");
    }

    try
    {
      var decoded = DecodeRecognizedEncoding(bytes);
      return NormalizeLineEndings(decoded);
    }
    catch (DecoderFallbackException)
    {
      throw new ImportTextDecodingException(
          UnsupportedEncodingCode,
          "Import source uses an unsupported encoding.");
    }
  }

  private static string DecodeRecognizedEncoding(ReadOnlySpan<byte> bytes)
  {
    if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
    {
      return Utf8.GetString(bytes[3..]);
    }

    if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
    {
      return Utf16LittleEndian.GetString(bytes[2..]);
    }

    if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
    {
      return Utf16BigEndian.GetString(bytes[2..]);
    }

    return Utf8.GetString(bytes);
  }

  private static string NormalizeLineEndings(string text) =>
      text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}
