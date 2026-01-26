using Microsoft.AspNetCore.Http;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Upload;

/// <summary>
/// Handler for file upload endpoint.
/// </summary>
internal static class UploadHandler
{
    /// <summary>
    /// Handles file upload requests.
    /// </summary>
    public static async Task<Microsoft.AspNetCore.Http.IResult> HandleAsync(
        UploadRequest request,
        IFileStorage storage,
        string prefix,
        CancellationToken ct)
    {
        await using var stream = request.File.OpenReadStream();
        var result = await storage.SaveAsync(stream, request.File.FileName, request.Folder, request.Metadata, ct);

        if (!result.Success)
        {
            return Results.BadRequest(new UploadResponse
            {
                Success = false,
                ErrorCode = result.ErrorCode,
                ErrorMessage = result.ErrorMessage
            });
        }

        return Results.Created(
            $"{prefix}/{result.Data!.Id}",
            new UploadResponse
            {
                Success = true,
                FileId = result.Data.Id,
                FileName = result.Data.Name,
                SizeBytes = result.Data.SizeBytes,
                ContentType = result.Data.ContentType,
                CreatedAt = result.Data.CreatedAtUtc
            });
    }
}
