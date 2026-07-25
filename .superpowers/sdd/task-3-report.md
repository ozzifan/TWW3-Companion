# Task 3 Report: Add strict text decoding and a real Steam metadata adapter

## Status: DONE

## Commit

- `594d5739eac763287f9b978ef6c6352293fd1201` — feat: add import text decoding and Steam metadata transport

## Summary

Added strict BOM-aware `ImportTextDecoder` for RFC-0004 text imports, moved Steam HTTP transport to Infrastructure as `SteamWebApiMetadataClient` over keyless Workshop POST endpoints, and updated `SteamCollectionImportAdapter` to retain valid member identities when title lookup fails (null `DisplayName`, warning diagnostic).

## TDD Evidence

### RED

Focused tests failed to compile before implementation:

- `ImportTextDecoder` / `ImportTextDecodingException` missing
- `SteamMetadataException` / `IsWarning` missing
- `Tww3Companion.Infrastructure.Importing` namespace missing
- Existing collection adapter omitted member `222` on lookup failure

### GREEN (focused)

```
ImportTextDecoderTests + SteamCollectionImportAdapterTests + DependencyRulesTests: 14 passed
SteamWebApiMetadataClientTests: 8 passed
```

### GREEN (full suites)

```
Application.Tests: 71 passed
Infrastructure.Tests: 82 passed
git diff --check: clean
```

## Implementation

### `ImportTextDecoder`

- Strict UTF-8 (with/without BOM), UTF-16 LE/BE BOM via `throwOnInvalidBytes` encoders
- CRLF/CR normalized to LF
- `ImportTextDecodingException` with code `import.source.encoding.unsupported` for empty, invalid, or ambiguous input

### `SteamWebApiMetadataClient`

- POST `ISteamRemoteStorage/GetCollectionDetails/v1/` and `GetPublishedFileDetails/v1/`
- Form fields: `collectioncount`/`itemcount` + `publishedfileids[0]`
- Private JSON DTOs with `JsonSerializerDefaults.Web`
- Requires `result == 1`, non-empty child IDs, non-empty title
- HTTP/JSON/shape failures → `SteamMetadataException` (no response bodies in messages)
- No API key in query or form body

### `SteamCollectionImportAdapter`

- On per-member title lookup failure: adds candidate with null `DisplayName`, attaches warning diagnostic (`IsLookupFailure` / `IsWarning`)
- Does not substitute Workshop ID as display name

### Dependency direction

- Deleted Application stub `SteamMetadataClient.cs`
- Added `Production_source_avoids_forbidden_namespaces` rule: Domain must not use `System.Net.Http`; Application and Desktop `Services/` must not use Serilog

## Files Changed

| File | Change |
|------|--------|
| `src/Tww3Companion.Application/Importing/ImportTextDecoder.cs` | Created |
| `src/Tww3Companion.Application/Importing/ImportTextDecodingException.cs` | Created |
| `src/Tww3Companion.Application/Importing/SteamMetadataException.cs` | Created |
| `src/Tww3Companion.Application/Importing/SteamCollectionImportAdapter.cs` | Identity retention on lookup failure |
| `src/Tww3Companion.Application/Importing/SteamImportDiagnostic.cs` | Added `IsWarning` alias |
| `src/Tww3Companion.Application/Importing/SteamMetadataClient.cs` | Deleted |
| `src/Tww3Companion.Infrastructure/Importing/SteamWebApiMetadataClient.cs` | Created |
| `tests/.../ImportTextDecoderTests.cs` | Created |
| `tests/.../SteamWebApiMetadataClientTests.cs` | Created |
| `tests/.../SteamCollectionImportAdapterTests.cs` | Updated + new partial-enrichment test |
| `tests/.../DependencyRulesTests.cs` | Source-level namespace guard |

## Self-Review

1. **Strict decoding** — Invalid UTF-8 (`0xC3, 0x28`) throws typed exception; no replacement characters. BOM variants and line-ending normalization covered by theory data.
2. **Identity retention** — Both `ParseAsync_retains_member_identity_when_title_lookup_fails` and updated existing collection tests confirm member `222` remains a candidate with null display name.
3. **Keyless Steam API** — Recording handler verifies exact paths, form encoding, and absence of API key.
4. **Layer boundaries** — HttpClient/JSON in Infrastructure only; Application retains interface and source-neutral records.
5. **Privacy** — No logging of response bodies, display names, or imported prose added.

## Concerns

1. **`Tww3Companion.Infrastructure.csproj` unchanged** — `HttpClient` and `System.Text.Json` are framework-provided on net10; no additional package reference required.
2. **Composition wiring deferred** — `ApplicationComposition` does not yet register `SteamWebApiMetadataClient` (Task 5 per plan).
3. **Single-item adapter unchanged** — `SteamImportAdapter` still omits candidates on lookup failure; collection path only was in Task 3 scope.
