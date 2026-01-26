namespace StashPup.AspNetCore.Features.Delete;

/// <summary>
/// Response model for file delete endpoint.
/// </summary>
public class DeleteResponse
{
    /// <summary>
    /// Gets or sets whether the delete was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error code (when unsuccessful).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message (when unsuccessful).
    /// </summary>
    public string? ErrorMessage { get; set; }
}
