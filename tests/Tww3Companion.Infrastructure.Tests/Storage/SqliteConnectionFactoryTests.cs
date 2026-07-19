using Tww3Companion.Infrastructure.Storage;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class SqliteConnectionFactoryTests
{
  [Fact]
  public async Task OpenAsync_EnablesForeignKeysAndBusyTimeout()
  {
    var token = TestContext.Current.CancellationToken;
    var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
    try
    {
      var factory = new SqliteConnectionFactory();
      await using var connection = await factory.OpenAsync(path, token);
      await using var command = connection.CreateCommand();
      command.CommandText = "PRAGMA foreign_keys; PRAGMA busy_timeout;";
      await using var reader = await command.ExecuteReaderAsync(token);
      Assert.True(await reader.ReadAsync(token));
      Assert.Equal(1L, reader.GetInt64(0));
      Assert.True(await reader.NextResultAsync(token));
      Assert.True(await reader.ReadAsync(token));
      Assert.Equal(5000L, reader.GetInt64(0));
    }
    finally { File.Delete(path); }
  }
}
