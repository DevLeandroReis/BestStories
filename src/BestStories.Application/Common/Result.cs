namespace BestStories.Application.Common;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public ErrorCode ErrorCode { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        ErrorCode = ErrorCode.None;
    }

    private Result(string error, ErrorCode errorCode)
    {
        IsSuccess = false;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error, ErrorCode errorCode = ErrorCode.Validation) => new(error, errorCode);

    public Result<TOut> Map<TOut>(Func<T, TOut> transform) =>
        IsSuccess ? Result<TOut>.Ok(transform(Value!)) : Result<TOut>.Fail(Error!, ErrorCode);
}
