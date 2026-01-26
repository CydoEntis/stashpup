namespace StashPup.Core.Interfaces;

/// <summary>
/// Base interface for operation results.
/// </summary>
public interface IResult
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Gets the error message if the operation failed, or null if it succeeded.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the error code if the operation failed, or null if it succeeded.
    /// </summary>
    string? ErrorCode { get; }
}
