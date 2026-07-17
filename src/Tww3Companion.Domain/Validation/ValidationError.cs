namespace Tww3Companion.Domain.Validation;

public sealed record ValidationError(string Code, string Message);

public readonly record struct ValidationResult<T>
{
    private ValidationResult(bool isSuccess, T value, ValidationError error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public T Value { get; }

    public ValidationError Error { get; }

    public static ValidationResult<T> Success(T value) =>
        new(true, value, new ValidationError(string.Empty, string.Empty));

    public static ValidationResult<T> Failure(ValidationError error) =>
        new(false, default!, error);
}
