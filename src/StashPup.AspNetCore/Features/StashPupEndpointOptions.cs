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
}
