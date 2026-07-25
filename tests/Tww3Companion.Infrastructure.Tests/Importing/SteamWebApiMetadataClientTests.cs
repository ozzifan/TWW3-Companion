using System.Net;
using System.Text;
using Tww3Companion.Application.Importing;
using Tww3Companion.Infrastructure.Importing;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Importing;

public sealed class SteamWebApiMetadataClientTests
{
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

  [Fact]
  public async Task GetWorkshopItemAsync_posts_documented_form_and_returns_title()
  {
    var handler = new RecordingHandler("""
        {"response":{"publishedfiledetails":[{"publishedfileid":"111","result":1,"title":"Resolved title"}]}}
        """);
    var client = new SteamWebApiMetadataClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.steampowered.com/")
    });

    var result = await client.GetWorkshopItemAsync(
        "111",
        TestContext.Current.CancellationToken);

    Assert.Equal("111", result.WorkshopItemId);
    Assert.Equal("Resolved title", result.DisplayName);
    Assert.Equal(
        "/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
        handler.LastRequest!.RequestUri!.AbsolutePath);
    Assert.Contains("itemcount=1", handler.LastBody);
    Assert.Contains("publishedfileids%5B0%5D=111", handler.LastBody);
  }

  [Fact]
  public async Task GetCollectionAsync_throws_when_http_request_fails()
  {
    var handler = new RecordingHandler("", HttpStatusCode.InternalServerError);
    var client = new SteamWebApiMetadataClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.steampowered.com/")
    });

    await Assert.ThrowsAsync<SteamMetadataException>(() =>
        client.GetCollectionAsync("900", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetWorkshopItemAsync_throws_when_response_is_malformed_json()
  {
    var handler = new RecordingHandler("{not-json");
    var client = new SteamWebApiMetadataClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.steampowered.com/")
    });

    await Assert.ThrowsAsync<SteamMetadataException>(() =>
        client.GetWorkshopItemAsync("111", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetCollectionAsync_throws_when_result_code_is_not_success()
  {
    var handler = new RecordingHandler("""
        {"response":{"collectiondetails":[{"publishedfileid":"900","result":0}]}}
        """);
    var client = new SteamWebApiMetadataClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.steampowered.com/")
    });

    await Assert.ThrowsAsync<SteamMetadataException>(() =>
        client.GetCollectionAsync("900", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetWorkshopItemAsync_throws_when_title_is_missing()
  {
    var handler = new RecordingHandler("""
        {"response":{"publishedfiledetails":[{"publishedfileid":"111","result":1,"title":""}]}}
        """);
    var client = new SteamWebApiMetadataClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.steampowered.com/")
    });

    await Assert.ThrowsAsync<SteamMetadataException>(() =>
        client.GetWorkshopItemAsync("111", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetCollectionAsync_honors_cancellation()
  {
    using var source = new CancellationTokenSource();
    source.Cancel();
    var handler = new RecordingHandler("""
        {"response":{"collectiondetails":[{"publishedfileid":"900","result":1,
        "children":[{"publishedfileid":"111"}]}]}}
        """);
    var client = new SteamWebApiMetadataClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.steampowered.com/")
    });

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        client.GetCollectionAsync("900", source.Token));
  }

  [Fact]
  public async Task GetCollectionAsync_does_not_send_api_key()
  {
    var handler = new RecordingHandler("""
        {"response":{"collectiondetails":[{"publishedfileid":"900","result":1,
        "children":[{"publishedfileid":"111"}]}]}}
        """);
    var client = new SteamWebApiMetadataClient(new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.steampowered.com/")
    });

    await client.GetCollectionAsync("900", TestContext.Current.CancellationToken);

    var query = handler.LastRequest!.RequestUri!.Query;
    Assert.DoesNotContain("key=", query, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("api_key", handler.LastBody ?? string.Empty, StringComparison.OrdinalIgnoreCase);
  }

  private sealed class RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
      : HttpMessageHandler
  {
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      LastRequest = request;
      LastBody = request.Content is null
          ? null
          : await request.Content.ReadAsStringAsync(cancellationToken);
      return new HttpResponseMessage(statusCode)
      {
        Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
      };
    }
  }
}
