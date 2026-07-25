using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tww3Companion.Application.Importing;

namespace Tww3Companion.Infrastructure.Importing;

public sealed class SteamWebApiMetadataClient(HttpClient httpClient) : ISteamMetadataClient
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  public async Task<SteamCollectionMetadata> GetCollectionAsync(
      string collectionId,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
    cancellationToken.ThrowIfCancellationRequested();

    using var content = CreatePublishedFileIdsForm("collectioncount", collectionId);
    using var response = await httpClient.PostAsync(
        "ISteamRemoteStorage/GetCollectionDetails/v1/",
        content,
        cancellationToken).ConfigureAwait(false);

    EnsureSuccessStatusCode(response.StatusCode);

    var payload = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
    CollectionDetailsEnvelope envelope;
    try
    {
      envelope = JsonSerializer.Deserialize<CollectionDetailsEnvelope>(payload, JsonOptions)
          ?? throw CreateMetadataException("Steam collection metadata response was incomplete.");
    }
    catch (JsonException exception)
    {
      throw CreateMetadataException("Steam collection metadata response was malformed.", exception);
    }

    var detail = envelope.Response?.CollectionDetails?.FirstOrDefault()
        ?? throw CreateMetadataException("Steam collection metadata response was incomplete.");

    if (detail.Result != 1)
    {
      throw CreateMetadataException("Steam collection metadata response was incomplete.");
    }

    var members = detail.Children?
        .Select(child => child.PublishedFileId)
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => new SteamWorkshopItemReference(id!))
        .ToArray()
        ?? [];

    if (members.Length == 0)
    {
      throw CreateMetadataException("Steam collection metadata response contained no members.");
    }

    return new SteamCollectionMetadata(collectionId, members);
  }

  public async Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(
      string workshopItemId,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(workshopItemId);
    cancellationToken.ThrowIfCancellationRequested();

    using var content = CreatePublishedFileIdsForm("itemcount", workshopItemId);
    using var response = await httpClient.PostAsync(
        "ISteamRemoteStorage/GetPublishedFileDetails/v1/",
        content,
        cancellationToken).ConfigureAwait(false);

    EnsureSuccessStatusCode(response.StatusCode);

    var payload = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
    PublishedFileDetailsEnvelope envelope;
    try
    {
      envelope = JsonSerializer.Deserialize<PublishedFileDetailsEnvelope>(payload, JsonOptions)
          ?? throw CreateMetadataException("Steam workshop item metadata response was incomplete.");
    }
    catch (JsonException exception)
    {
      throw CreateMetadataException("Steam workshop item metadata response was malformed.", exception);
    }

    var detail = envelope.Response?.PublishedFileDetails?.FirstOrDefault()
        ?? throw CreateMetadataException("Steam workshop item metadata response was incomplete.");

    if (detail.Result != 1 || string.IsNullOrWhiteSpace(detail.Title))
    {
      throw CreateMetadataException("Steam workshop item metadata response was incomplete.");
    }

    return new SteamWorkshopItemMetadata(workshopItemId, detail.Title);
  }

  private static FormUrlEncodedContent CreatePublishedFileIdsForm(string countFieldName, string publishedFileId) =>
      new([
          new KeyValuePair<string, string>(countFieldName, "1"),
          new KeyValuePair<string, string>("publishedfileids[0]", publishedFileId)
      ]);

  private static async Task<string> ReadResponseBodyAsync(
      HttpResponseMessage response,
      CancellationToken cancellationToken)
  {
    try
    {
      return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      throw CreateMetadataException("Steam metadata response could not be read.", exception);
    }
  }

  private static void EnsureSuccessStatusCode(HttpStatusCode statusCode)
  {
    if ((int)statusCode >= 200 && (int)statusCode <= 299)
    {
      return;
    }

    throw CreateMetadataException("Steam metadata request failed.");
  }

  private static SteamMetadataException CreateMetadataException(string message, Exception? innerException = null) =>
      innerException is null
          ? new SteamMetadataException(message)
          : new SteamMetadataException(message, innerException);

  private sealed record CollectionDetailsEnvelope(CollectionDetailsResponse? Response);

  private sealed record CollectionDetailsResponse(CollectionDetail[]? CollectionDetails);

  private sealed record CollectionDetail(
      [property: JsonPropertyName("publishedfileid")] string? PublishedFileId,
      [property: JsonPropertyName("result")] int? Result,
      [property: JsonPropertyName("children")] CollectionChild[]? Children);

  private sealed record CollectionChild(
      [property: JsonPropertyName("publishedfileid")] string? PublishedFileId);

  private sealed record PublishedFileDetailsEnvelope(PublishedFileDetailsResponse? Response);

  private sealed record PublishedFileDetailsResponse(PublishedFileDetail[]? PublishedFileDetails);

  private sealed record PublishedFileDetail(
      [property: JsonPropertyName("publishedfileid")] string? PublishedFileId,
      [property: JsonPropertyName("result")] int? Result,
      [property: JsonPropertyName("title")] string? Title);
}
