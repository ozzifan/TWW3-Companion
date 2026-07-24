using Tww3Companion.Application.Common;

namespace Tww3Companion.Application.Importing;

public sealed class ImportPersistenceException(OperationError error)
    : Exception(error.Message)
{
  public OperationError Error { get; } = error;
}
