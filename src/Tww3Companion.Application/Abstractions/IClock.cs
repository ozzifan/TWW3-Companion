namespace Tww3Companion.Application.Abstractions;

public interface IClock
{
  DateTimeOffset UtcNow { get; }
}
