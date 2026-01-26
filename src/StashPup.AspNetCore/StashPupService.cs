using Microsoft.AspNetCore.Http;
using StashPup.Core.Core;
using StashPup.Core.Interfaces;
using StashPup.Core.Models;

namespace StashPup.AspNetCore;

/// <summary>
/// High-level service facade for simplified usage in ASP.NET Core.
/// Provides convenience methods for common file operations with ASP.NET Core types.
/// </summary>
public class StashPupService
{
    private readonly IFileStorage _storage;

    /// <summary>
    /// Initializes a new instance of the <see cref="StashPupService"/> class.
    /// </summary>
    /// <param name="storage">The file storage provider to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when storage is null.</exception>
    public StashPupService(IFileStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Uploads an IFormFile (convenience method for ASP.NET Core).
    /// </summary>
    /// <param name="file">The uploaded file from an HTTP form.</param>
    /// <param name="folder">Optional folder/prefix for organization.</param>
    /// <param name="metadata">Optional custom metadata key-value pairs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing FileRecord on success.</returns>
    public async Task<Result<FileRecord>> UploadAsync(
        IFormFile file,
        string? folder = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        return await _storage.SaveAsync(stream, file.FileName, folder, metadata, ct);
    }

    /// <summary>
    /// Downloads a file and returns it as a FileStreamResult for ASP.NET Core.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>IResult that can be returned from a controller or minimal API endpoint.</returns>
    public async Task<Microsoft.AspNetCore.Http.IResult> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var metaResult = await _storage.GetMetadataAsync(id, ct);
        if (!metaResult.Success)
            return Results.NotFound(metaResult.ErrorMessage);

        var streamResult = await _storage.GetAsync(id, ct);
        if (!streamResult.Success)
            return Results.NotFound(streamResult.ErrorMessage);

        return Results.File(
            streamResult.Data!,
            metaResult.Data!.ContentType,
            metaResult.Data!.Name);
    }

    /// <summary>
    /// Gets file metadata.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing FileRecord with metadata on success.</returns>
    public async Task<Result<FileRecord>> GetMetadataAsync(Guid id, CancellationToken ct = default)
    {
        return await _storage.GetMetadataAsync(id, ct);
    }

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="id">File identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result with true if deleted, false if not found.</returns>
    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return await _storage.DeleteAsync(id, ct);
    }
}
