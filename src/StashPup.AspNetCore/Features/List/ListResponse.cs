namespace StashPup.AspNetCore.Features.List;

/// <summary>
/// Response model for file list endpoint.
/// </summary>
public class ListResponse
{
    /// <summary>
    /// Gets or sets whether the request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the list of files (when successful).
    /// </summary>
    public List<FileItemResponse>? Files { get; set; }

    /// <summary>
    /// Gets or sets the total count of files (when successful).
    /// </summary>
    public int? TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page number (when successful).
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// Gets or sets the page size (when successful).
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages (when successful).
    /// </summary>
    public int? TotalPages { get; set; }

    /// <summary>
    /// Gets or sets the error code (when unsuccessful).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message (when unsuccessful).
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// File item in list response.
/// </summary>
public class FileItemResponse
{
    /// <summary>
    /// Gets or sets the file identifier.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original file name.
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the folder/prefix.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
