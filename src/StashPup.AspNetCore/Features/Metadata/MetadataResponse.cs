namespace StashPup.AspNetCore.Features.Metadata;

/// <summary>
/// Response model for file metadata endpoint.
/// </summary>
public class MetadataResponse
{
    /// <summary>
    /// Gets or sets whether the request was successful.
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
    /// Gets or sets the original file name (when successful).
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes (when successful).
    /// </summary>
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the content type (when successful).
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the folder/prefix (when successful).
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp (when successful).
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp (when successful).
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash (when successful).
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Gets or sets the custom metadata (when successful).
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the error code (when unsuccessful).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message (when unsuccessful).
    /// </summary>
    public string? ErrorMessage { get; set; }
}
