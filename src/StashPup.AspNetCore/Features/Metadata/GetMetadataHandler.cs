using Microsoft.AspNetCore.Http;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Metadata;

/// <summary>
/// Handler for file metadata endpoint.
/// </summary>
internal static class GetMetadataHandler
{
    /// <summary>
    /// Handles file metadata requests.
    /// </summary>
    public static async Task<Microsoft.AspNetCore.Http.IResult> HandleAsync(
        Guid id,
        IFileStorage storage,
        CancellationToken ct)
    {
        var result = await storage.GetMetadataAsync(id, ct);

        if (!result.Success)
        {
            return Results.NotFound(new MetadataResponse
            {
                Success = false,
                ErrorCode = result.ErrorCode,
                ErrorMessage = result.ErrorMessage
            });
        }

        var file = result.Data!;
        return Results.Ok(new MetadataResponse
        {
            Success = true,
            FileId = file.Id,
            FileName = file.Name,
            OriginalName = file.OriginalName,
            SizeBytes = file.SizeBytes,
            ContentType = file.ContentType,
            Folder = file.Folder,
            CreatedAt = file.CreatedAtUtc,
            UpdatedAt = file.UpdatedAtUtc,
            Hash = file.Hash,
            Metadata = file.Metadata
        });
    }
}
