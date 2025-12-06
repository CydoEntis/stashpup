using StashPup.Core.Interfaces;

namespace StashPup.Core.Core;

public class Result<T> : IResult
{
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public T? Data { get; private set; }

    private Result(bool success, T? data = default, string? errorMessage = null)
    {
        Success = success;
        Data = data;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Ok(T data) => new Result<T>(true, data);
    public static Result<T> Fail(string message) => new Result<T>(false, default, message);
}