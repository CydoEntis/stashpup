namespace StashPup.Core.Core;

public class Result<T>
{
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ErrorCode { get; private set; }
    public T? Data { get; private set; }

    private Result(bool success, T? data = default, string? errorMessage = null, string? errorCode = null)
    {
        Success = success;
        Data = data;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    public static Result<T> Ok(T data) => new Result<T>(true, data);

    public static Result<T> Fail(string message, string? code = null)
        => new Result<T>(false, default, message, code);
}