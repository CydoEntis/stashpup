namespace StashPup.AspNetCore.Features.Upload;

/// <summary>
/// Response model for file upload endpoint.
/// </summary>
public class UploadResponse
{
    /// <summary>
    /// Gets or sets whether the upload was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the file identifier (when successful).
    /// </summary>
    public Guid? FileId { get; set; }

    /// <summary>
    /// Gets or sets the file name (when successful).
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes (when successful).
    /// </summary>
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the content type (when successful).
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp (when successful).
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the error code (when unsuccessful).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message (when unsuccessful).
    /// </summary>
    public string? ErrorMessage { get; set; }
}
