### Task 3: Add strict text decoding and a real Steam metadata adapter

**Files:**
- Create: `src/Tww3Companion.Application/Importing/ImportTextDecoder.cs`
- Delete: `src/Tww3Companion.Application/Importing/SteamMetadataClient.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamCollectionImportAdapter.cs`
- Create: `src/Tww3Companion.Infrastructure/Importing/SteamWebApiMetadataClient.cs`
- Modify: `src/Tww3Companion.Infrastructure/Tww3Companion.Infrastructure.csproj`
- Create: `tests/Tww3Companion.Application.Tests/Importing/ImportTextDecoderTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/SteamCollectionImportAdapterTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Importing/SteamWebApiMetadataClientTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Architecture/DependencyRulesTests.cs`

**Interfaces:**
- Consumes: existing `ISteamMetadataClient`, `SteamCollectionMetadata`, and `SteamWorkshopItemMetadata`
- Produces: strict `ImportTextDecoder.Decode(ReadOnlySpan<byte>)` and production `SteamWebApiMetadataClient`

- [ ] **Step 1: Write failing decoder tests**

Cover UTF-8 without BOM, UTF-8 BOM, UTF-16 LE BOM, UTF-16 BE BOM, and invalid bytes:

```csharp
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
```

- [ ] **Step 2: Implement strict BOM-aware decoding**

Use strict encoders:

```csharp
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
```

Strip a recognised BOM, decode, normalize CRLF/CR to LF, and throw typed `ImportTextDecodingException` for empty/unsupported data or `DecoderFallbackException`.

- [ ] **Step 3: Write failing Steam HTTP contract tests**

Use a recording `HttpMessageHandler` and exact JSON fixtures:

```csharp
[Fact]
public async Task GetCollectionAsync_posts_documented_form_and_returns_members()
{
  var handler = new RecordingHandler("""
      {"response":{"collectiondetails":[{"publishedfileid":"900","result":1,
      "children":[{"publishedfileid":"111"},{"publishedfileid":"222"}]}]}}
      """);
  var client = new SteamWebApiMetadataClient(new HttpClient(handler)
  {
    BaseAddress = new Uri("https://api.steampowered.com/")
  });

  var result = await client.GetCollectionAsync(
      "900",
      TestContext.Current.CancellationToken);

  Assert.Equal(["111", "222"], result.Members.Select(x => x.WorkshopItemId));
  Assert.Equal(
      "/ISteamRemoteStorage/GetCollectionDetails/v1/",
      handler.LastRequest!.RequestUri!.AbsolutePath);
  Assert.Contains("collectioncount=1", handler.LastBody);
  Assert.Contains("publishedfileids%5B0%5D=900", handler.LastBody);
}
```

Also test item title mapping, HTTP failure, malformed JSON, missing result/title, cancellation, and no API key/query secret.

- [ ] **Step 4: Write the failing Collection partial-enrichment test**

Prove that a valid member identity survives a title lookup failure:

```csharp
[Fact]
public async Task ParseAsync_retains_member_identity_when_title_lookup_fails()
{
  var client = new StubSteamMetadataClient(
      collectionMembers: ["111", "222"],
      itemResults: new Dictionary<string, object>
      {
        ["111"] = new SteamWorkshopItemMetadata("111", "Resolved title"),
        ["222"] = new SteamMetadataException("lookup unavailable")
      });

  var result = await SteamCollectionImportAdapter.ParseAsync(
      "900",
      client,
      TestContext.Current.CancellationToken);

  Assert.Equal(["111", "222"], result.Candidates.Select(x => x.SourceReference));
  Assert.Null(result.Candidates.Single(x => x.SourceReference == "222").DisplayName);
  Assert.Contains(result.Diagnostics, x =>
      x.SourceReference == "222" && x.IsWarning);
}
```

The candidate type must represent a valid Workshop identity without inventing the ID as its display name.

- [ ] **Step 5: Run focused tests and observe RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportTextDecoderTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~SteamCollectionImportAdapterTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SteamWebApiMetadataClientTests" -v minimal
```

Expected: missing decoder and HTTP adapter, plus the existing Collection adapter omits member `222`.

- [ ] **Step 6: Implement strict decoding, identity retention, and the keyless Steam Web API adapter**

Implement `ImportTextDecoder` as specified in Step 2. In `SteamCollectionImportAdapter`, add every valid member as a candidate even when its title lookup fails; attach the warning diagnostic and leave `DisplayName` absent. Do not substitute the Workshop ID into `DisplayName`.

Use Valve's documented public POST endpoints:

- `ISteamRemoteStorage/GetCollectionDetails/v1/`
- `ISteamRemoteStorage/GetPublishedFileDetails/v1/`

Reference: `https://partner.steamgames.com/doc/webapi/isteamremotestorage`.

Constructor:

```csharp
public sealed class SteamWebApiMetadataClient(HttpClient httpClient)
    : ISteamMetadataClient
```

Post `FormUrlEncodedContent` with `collectioncount` / `itemcount` and `publishedfileids[0]`. Deserialize with private DTO records and `JsonSerializerOptions(JsonSerializerDefaults.Web)`. Require result code `1`, non-empty child IDs, and non-empty item title. Convert HTTP, JSON, and response-shape failures to `SteamMetadataException` without including response bodies in messages or logs.

- [ ] **Step 7: Remove the Application stub and verify dependency direction**

Delete `Application/Importing/SteamMetadataClient.cs`. Application retains only `ISteamMetadataClient` and source-neutral records. Infrastructure owns `HttpClient` and JSON transport. Update dependency tests to reject `System.Net.Http` usage from Domain and to keep Serilog out of Application/Desktop coordinator code.

- [ ] **Step 8: Run focused tests and commit Task 3**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportTextDecoderTests|FullyQualifiedName~SteamCollectionImportAdapterTests|FullyQualifiedName~DependencyRulesTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SteamWebApiMetadataClientTests" -v minimal
git diff --check
git add src/Tww3Companion.Application src/Tww3Companion.Infrastructure tests/Tww3Companion.Application.Tests tests/Tww3Companion.Infrastructure.Tests
git commit -m "feat: add import text decoding and Steam metadata transport"
```

---
