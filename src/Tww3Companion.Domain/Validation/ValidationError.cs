namespace Tww3Companion.Domain.Validation;

public sealed record ValidationError(string Code, string Message);

public abstract record ValidationResult<T>
{
    private ValidationResult()
    {
    }

    public sealed record Success(T Value) : ValidationResult<T>;

    public sealed record Failure(ValidationError Error) : ValidationResult<T>;
}
