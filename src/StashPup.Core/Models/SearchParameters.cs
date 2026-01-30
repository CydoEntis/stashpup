namespace StashPup.Core.Models;

/// <summary>
/// Parameters for advanced file search operations.
/// </summary>
public class SearchParameters
{
    /// <summary>
    /// Filter by file name pattern (supports wildcards like * and ?).
    /// </summary>
    public string? NamePattern { get; set; }

    /// <summary>
    /// Filter by folder path (exact match or starts with).
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Filter by folders starting with this prefix (for nested folder queries).
    /// When set, includes all files in matching folders and subfolders.
    /// </summary>
    public string? FolderStartsWith { get; set; }

    /// <summary>
    /// Include files in subfolders when filtering by Folder or FolderStartsWith.
    /// Default is true.
    /// </summary>
    public bool IncludeSubfolders { get; set; } = true;

    /// <summary>
    /// Filter by file extension (e.g., ".jpg", ".png").
    /// </summary>
    public string? Extension { get; set; }

    /// <summary>
    /// Filter by content type (e.g., "image/*", "application/pdf").
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Filter by minimum file size in bytes.
    /// </summary>
    public long? MinSizeBytes { get; set; }

    /// <summary>
    /// Filter by maximum file size in bytes.
    /// </summary>
    public long? MaxSizeBytes { get; set; }

    /// <summary>
    /// Filter by files created after this date.
    /// </summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>
    /// Filter by files created before this date.
    /// </summary>
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Filter by files updated after this date.
    /// </summary>
    public DateTime? UpdatedAfter { get; set; }

    /// <summary>
    /// Filter by files updated before this date.
    /// </summary>
    public DateTime? UpdatedBefore { get; set; }

    /// <summary>
    /// Filter by custom metadata key-value pairs.
    /// All specified pairs must match.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Sort field for results.
    /// </summary>
    public SearchSortField SortBy { get; set; } = SearchSortField.CreatedAt;

    /// <summary>
    /// Sort direction for results.
    /// </summary>
    public SearchSortDirection SortDirection { get; set; } = SearchSortDirection.Descending;

    /// <summary>
    /// Page number (1-based) for pagination.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page (max 1000).
    /// </summary>
    public int PageSize { get; set; } = 100;
}

/// <summary>
/// Available fields for sorting search results.
/// </summary>
public enum SearchSortField
{
    /// <summary>Sort by file name.</summary>
    Name,

    /// <summary>Sort by file size.</summary>
    Size,

    /// <summary>Sort by creation date.</summary>
    CreatedAt,

    /// <summary>Sort by last updated date.</summary>
    UpdatedAt,

    /// <summary>Sort by file extension.</summary>
    Extension,

    /// <summary>Sort by content type.</summary>
    ContentType
}

/// <summary>
/// Sort direction for search results.
/// </summary>
public enum SearchSortDirection
{
    /// <summary>Sort in ascending order.</summary>
    Ascending,

    /// <summary>Sort in descending order.</summary>
    Descending
}