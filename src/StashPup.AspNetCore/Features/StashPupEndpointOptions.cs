namespace StashPup.AspNetCore.Features;

/// <summary>
/// Options for configuring StashPup endpoints.
/// </summary>
public class StashPupEndpointOptions
{
    /// <summary>
    /// Gets or sets whether endpoints require authorization.
    /// Default: false
    /// </summary>
    public bool RequireAuthorization { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable the upload endpoint (POST).
    /// Default: true
    /// </summary>
    public bool EnableUpload { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the download endpoint (GET /{id}).
    /// Default: true
    /// </summary>
    public bool EnableDownload { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the delete endpoint (DELETE /{id}).
    /// Default: true
    /// </summary>
    public bool EnableDelete { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the metadata endpoint (GET /{id}/metadata).
    /// Default: true
    /// </summary>
    public bool EnableMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the list endpoint (GET /?folder=...&amp;page=...).
    /// Default: false (disabled by default for security)
    /// </summary>
    public bool EnableList { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable folder listing endpoint (GET /folders).
    /// Default: false (disabled by default for security)
    /// </summary>
    public bool EnableFolderList { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable folder deletion endpoint (DELETE /folders/{path}).
    /// Default: true
    /// </summary>
    public bool EnableFolderDelete { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable bulk move endpoint (POST /bulk-move).
    /// Default: true
    /// </summary>
    public bool EnableBulkMove { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable folder creation endpoint (POST /folders).
    /// Default: true
    /// </summary>
    public bool EnableFolderCreate { get; set; } = true;
}
