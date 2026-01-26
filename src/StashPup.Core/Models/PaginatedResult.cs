namespace StashPup.Core.Models;

/// <summary>
/// Container for paginated list results.
/// </summary>
/// <typeparam name="T">The type of items in the paginated result.</typeparam>
public class PaginatedResult<T>
{
    /// <summary>
    /// Gets or sets the items for the current page.
    /// </summary>
    public IReadOnlyList<T> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
