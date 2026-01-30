using StashPup.Core.Core;
using StashPup.Core.Models;

namespace StashPup.Core.Interfaces;

/// <summary>
/// Main interface for file storage providers. All storage implementations must implement this interface.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "Local", "S3", "AzureBlob").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Saves a file to storage.
    /// </summary>
    /// <param name="content">File content stream (will be read, not disposed)</param>
    /// <param name="fileName">Original file name (used for extension, content-type detection)</param>
    /// <param name="folder">Optional folder/prefix for organization</param>
    /// <param name="metadata">Optional custom metadata key-value pairs</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing FileRecord on success</returns>
    Task<Result<FileRecord>> SaveAsync(
        Stream content,
        string fileName,
        string? folder = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a file's content stream.
    /// </summary>
    /// <param name="id">File identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing readable stream (caller must dispose)</returns>
    Task<Result<Stream>> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves only file metadata without content.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing FileRecord with metadata on success.</returns>
    Task<Result<FileRecord>> GetMetadataAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result with true if deleted, false if not found.</returns>
    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Checks if a file exists in storage.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result with true if file exists, false otherwise.</returns>
    Task<Result<bool>> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Renames a file (changes display name, not the storage key).
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="newName">New display name for the file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated FileRecord on success.</returns>
    Task<Result<FileRecord>> RenameAsync(
        Guid id,
        string newName,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a file to a different folder.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="newFolder">Destination folder path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated FileRecord on success.</returns>
    Task<Result<FileRecord>> MoveAsync(
        Guid id,
        string newFolder,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a copy of a file in a new location.
    /// </summary>
    /// <param name="id">File identifier of the source file.</param>
    /// <param name="newFolder">Destination folder path for the copy.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing FileRecord for the new copy on success.</returns>
    Task<Result<FileRecord>> CopyAsync(
        Guid id,
        string newFolder,
        CancellationToken ct = default);

    /// <summary>
    /// Saves multiple files in a single operation.
    /// </summary>
    /// <param name="files">Collection of files to save.</param>
    /// <param name="folder">Optional folder/prefix for organization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of FileRecords for successfully saved files.</returns>
    Task<Result<IReadOnlyList<FileRecord>>> BulkSaveAsync(
        IEnumerable<BulkSaveItem> files,
        string? folder = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes multiple files in a single operation.
    /// </summary>
    /// <param name="ids">Collection of file identifiers to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of successfully deleted file IDs.</returns>
    Task<Result<IReadOnlyList<Guid>>> BulkDeleteAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default);

    /// <summary>
    /// Moves multiple files to a new folder in a single operation.
    /// </summary>
    /// <param name="ids">Collection of file identifiers to move.</param>
    /// <param name="newFolder">Destination folder path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of successfully moved FileRecords.</returns>
    Task<Result<IReadOnlyList<FileRecord>>> BulkMoveAsync(
        IEnumerable<Guid> ids,
        string newFolder,
        CancellationToken ct = default);

    /// <summary>
    /// Lists files with pagination support.
    /// </summary>
    /// <param name="folder">Optional folder filter (null = all files)</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page (max 1000)</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Result<PaginatedResult<FileRecord>>> ListAsync(
        string? folder = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Searches files with advanced filtering and sorting options.
    /// </summary>
    /// <param name="searchParameters">Search and filter parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing paginated search results.</returns>
    Task<Result<PaginatedResult<FileRecord>>> SearchAsync(
        SearchParameters searchParameters,
        CancellationToken ct = default);

    /// <summary>
    /// Generates and retrieves a thumbnail for an image file.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="size">Thumbnail size to generate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing thumbnail image stream (caller must dispose).</returns>
    Task<Result<Stream>> GetThumbnailAsync(
        Guid id,
        ThumbnailSize size = ThumbnailSize.Medium,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a public URL for a file (if publicly accessible).
    /// Returns null if the provider doesn't support public URLs or file doesn't exist.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <returns>Public URL for the file, or null if not available.</returns>
    string? GetPublicUrl(Guid id);

    /// <summary>
    /// Generates a time-limited signed URL for secure access.
    /// Essential for S3/Azure; local storage may return a simple path or null.
    /// </summary>
    /// <param name="id">File identifier</param>
    /// <param name="expiry">How long the URL should be valid</param>
    /// <param name="ct">Cancellation token</param>
    Task<Result<string>> GetSignedUrlAsync(
        Guid id,
        TimeSpan expiry,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a list of all unique folder paths in storage.
    /// </summary>
    /// <param name="parentFolder">Optional parent folder to filter by (returns immediate children only).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of unique folder paths.</returns>
    Task<Result<IReadOnlyList<string>>> ListFoldersAsync(
        string? parentFolder = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a folder and all files within it.
    /// </summary>
    /// <param name="folder">Folder path to delete.</param>
    /// <param name="recursive">If true, deletes all files in subfolders as well.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing count of deleted files.</returns>
    Task<Result<int>> DeleteFolderAsync(
        string folder,
        bool recursive = true,
        CancellationToken ct = default);

    /// <summary>
    /// Creates an empty folder by placing a hidden placeholder file.
    /// Allows folders to exist without any user files, similar to Google Drive.
    /// </summary>
    /// <param name="folderPath">The folder path to create (e.g., "documents/2024").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the created folder path.</returns>
    Task<Result<string>> CreateFolderAsync(
        string folderPath,
        CancellationToken ct = default);
}

/// <summary>
/// Item for bulk save operations.
/// </summary>
/// <param name="Content">File content stream.</param>
/// <param name="FileName">Original file name.</param>
/// <param name="Metadata">Optional custom metadata key-value pairs.</param>
public record BulkSaveItem(
    Stream Content,
    string FileName,
    Dictionary<string, string>? Metadata = null);
