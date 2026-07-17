namespace Tww3Companion.Application.Common;

public sealed record OperationError(
    string Code,
    string Message,
    bool PersistentChangeCommitted,
    string SafeNextAction);
