namespace Tww3Companion.Application.Common;

public abstract record OperationResult<T>
{
  private OperationResult()
  {
  }

  public sealed record Success(T Value) : OperationResult<T>;

  public sealed record Failure(OperationError Error) : OperationResult<T>;
}
