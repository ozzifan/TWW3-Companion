using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting;
using Tww3Companion.Infrastructure.Paths;

namespace Tww3Companion.Infrastructure.Logging;

public static class LoggingConfiguration
{
  private const string OperationTemplate = "Operation {OperationName} {WorkspacePathId}";
  private const string FailedOperationTemplate =
      "Operation {OperationName} {WorkspacePathId} failed {FailureCategory}";

  /// <summary>
  /// Creates the default privacy-bounded file provider. It accepts only the fixed operation
  /// templates and constrained safe properties; free-form events, extra properties, and
  /// invalid values are excluded. Exceptions retain only their type and method identities;
  /// messages, source locations, line data, and arguments are never rendered.
  /// </summary>
  public static ILoggerProvider CreateProvider(ManagedPaths paths)
  {
    var logger = new LoggerConfiguration()
        .Filter.ByIncludingOnly(IsPrivacySafe)
        .WriteTo.File(
            formatter: PrivacySafeFormatter.Instance,
            path: Path.Combine(paths.LogsDirectory, "tww3-companion-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: 7,
            shared: false)
        .CreateLogger();

    return new SerilogLoggerProvider(logger, dispose: true);
  }

  private static bool IsPrivacySafe(LogEvent logEvent)
  {
    if (logEvent.MessageTemplate.Text is not (OperationTemplate or FailedOperationTemplate) ||
        logEvent.Properties.Keys.Any(property => property is not
            ("OperationName" or "WorkspacePathId" or "FailureCategory" or "SourceContext" or "EventId")))
    {
      return false;
    }

    return HasSafeString(logEvent, "OperationName", IsApprovedOperation) &&
           HasSafeString(logEvent, "WorkspacePathId", IsOpaquePathIdentifier) &&
           (!logEvent.Properties.ContainsKey("FailureCategory") ||
            HasSafeString(logEvent, "FailureCategory", IsApprovedFailureCategory)) &&
           (!logEvent.Properties.ContainsKey("SourceContext") ||
            HasSafeString(logEvent, "SourceContext", IsSourceContext));
  }

  private static bool HasSafeString(
      LogEvent logEvent,
      string propertyName,
      Func<string, bool> validator) =>
      logEvent.Properties.TryGetValue(propertyName, out var value) &&
      value is ScalarValue { Value: string text } &&
      validator(text);

  private static bool IsApprovedOperation(string value) => value is
      "startup.initialize" or
      "settings.load" or
      "settings.save" or
      "workspace.create" or
      "workspace.open";

  private static bool IsApprovedFailureCategory(string value) => value is
      "database" or "invalid-data" or "io" or "permissions" or "unexpected";

  private static bool IsOpaquePathIdentifier(string value) =>
      value.Length is >= 9 and <= 69 &&
      value.StartsWith("path-", StringComparison.Ordinal) &&
      value[5..].All(character => char.IsAsciiHexDigit(character));

  private static bool IsSourceContext(string value) =>
      value.Length is > 0 and <= 128 &&
      value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_');

  private sealed class PrivacySafeFormatter : ITextFormatter
  {
    public static PrivacySafeFormatter Instance { get; } = new();

    public void Format(LogEvent logEvent, TextWriter output)
    {
      output.Write(logEvent.Timestamp.UtcDateTime.ToString(
          "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
          CultureInfo.InvariantCulture));
      output.Write(" [");
      output.Write(logEvent.Level);
      output.Write("]");
      if (TryGetNumericEventId(logEvent, out var numericEventId))
      {
        output.Write(" [");
        output.Write(numericEventId.ToString(CultureInfo.InvariantCulture));
        output.Write("]");
      }

      output.Write(' ');
      output.Write(logEvent.RenderMessage(CultureInfo.InvariantCulture));
      if (logEvent.Exception is not null)
      {
        output.Write(" [");
        output.Write(logEvent.Exception.GetType().FullName);
        output.Write(']');
        foreach (var frame in new StackTrace(logEvent.Exception, fNeedFileInfo: false)
                     .GetFrames()
                     .Take(32))
        {
          var method = frame.GetMethod();
          if (method is null)
          {
            continue;
          }

          output.Write(" at ");
          output.Write(method.DeclaringType?.FullName);
          output.Write('.');
          output.Write(method.Name);
        }
      }

      output.WriteLine();
    }

    private static bool TryGetNumericEventId(LogEvent logEvent, out int eventId)
    {
      eventId = 0;
      if (!logEvent.Properties.TryGetValue("EventId", out var value))
      {
        return false;
      }

      if (value is ScalarValue { Value: int scalarEventId })
      {
        eventId = scalarEventId;
        return true;
      }

      var idProperty = (value as StructureValue)?.Properties
          .SingleOrDefault(property => property.Name == "Id");
      if (idProperty?.Value is not ScalarValue { Value: int structuredEventId })
      {
        return false;
      }

      eventId = structuredEventId;
      return true;
    }
  }
}
