using StashPup.Core.Interfaces;

namespace StashPup.Core.Core;

/// <summary>
/// Generic result type for railway-oriented error handling.
/// </summary>
/// <typeparam name="T">The type of data returned on success.</typeparam>
public class Result<T> : IResult
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Gets the error message if the operation failed, or null if it succeeded.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the error code if the operation failed, or null if it succeeded.
    /// </summary>
    public string? ErrorCode { get; private set; }

    /// <summary>
    /// Gets the data returned by the operation if it succeeded, or null if it failed.
    /// </summary>
    public T? Data { get; private set; }

    private Result(bool success, T? data = default, string? errorMessage = null, string? errorCode = null)
    {
        Success = success;
        Data = data;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    /// <param name="data">The data to return.</param>
    /// <returns>A successful result containing the data.</returns>
    public static Result<T> Ok(T data) => new Result<T>(true, data);

    /// <summary>
    /// Creates a failed result with error details.
    /// </summary>
    /// <param name="message">The error message describing what went wrong.</param>
    /// <param name="code">Optional error code for programmatic error handling.</param>
    /// <returns>A failed result with the specified error information.</returns>
    public static Result<T> Fail(string message, string? code = null)
        => new Result<T>(false, default, message, code);

    /// <summary>
    /// Creates a failed result from another failed result (for chaining).
    /// </summary>
    /// <typeparam name="TOther">The type of the other result.</typeparam>
    /// <param name="other">The failed result to copy error information from.</param>
    /// <returns>A failed result with the same error information as the other result.</returns>
    public static Result<T> Fail<TOther>(Result<TOther> other)
        => new Result<T>(false, default, other.ErrorMessage, other.ErrorCode);

    /// <summary>
    /// Implicit conversion to bool for easy if-checking.
    /// </summary>
    public static implicit operator bool(Result<T> result) => result.Success;
}
