using System.Text;
using Tww3Companion.Application.Importing;
using Xunit;

namespace Tww3Companion.Application.Tests.Importing;

public sealed class ImportTextDecoderTests
{
  public static TheoryData<byte[], string> SupportedDocuments => new()
  {
    { Encoding.UTF8.GetBytes("hello"), "hello" },
    { [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("hello")], "hello" },
    {
      [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("wide")],
      "wide"
    },
    {
      [0xFE, 0xFF, .. new UnicodeEncoding(true, true).GetBytes("big")],
      "big"
    },
    { Encoding.UTF8.GetBytes("a\r\nb"), "a\nb" },
    { Encoding.UTF8.GetBytes("a\rb"), "a\nb" },
  };

  [Theory]
  [MemberData(nameof(SupportedDocuments))]
  public void Decode_accepts_supported_encodings(byte[] bytes, string expected)
  {
    Assert.Equal(expected, ImportTextDecoder.Decode(bytes));
  }

  [Fact]
  public void Decode_rejects_invalid_utf8_without_replacement_characters()
  {
    var exception = Assert.Throws<ImportTextDecodingException>(
        () => ImportTextDecoder.Decode([0xC3, 0x28]));

    Assert.Equal("import.source.encoding.unsupported", exception.Code);
  }

  [Fact]
  public void Decode_rejects_empty_input()
  {
    var exception = Assert.Throws<ImportTextDecodingException>(
        () => ImportTextDecoder.Decode(ReadOnlySpan<byte>.Empty));

    Assert.Equal("import.source.encoding.unsupported", exception.Code);
  }
}
