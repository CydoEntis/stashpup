namespace StashPup.AspNetCore.Features.List;

/// <summary>
/// Request model for file list endpoint.
/// </summary>
public class ListRequest
{
    /// <summary>
    /// Gets or sets the folder to filter by.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// Default: 1
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// Default: 20
    /// </summary>
    public int PageSize { get; set; } = 20;
}
