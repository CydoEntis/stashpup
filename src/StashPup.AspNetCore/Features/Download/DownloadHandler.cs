using Microsoft.AspNetCore.Http;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Download;

/// <summary>
/// Handler for file download endpoint.
/// </summary>
internal static class DownloadHandler
{
    /// <summary>
    /// Handles file download requests.
    /// </summary>
    public static async Task<Microsoft.AspNetCore.Http.IResult> HandleAsync(
        Guid id,
        IFileStorage storage,
        CancellationToken ct)
    {
        var metaResult = await storage.GetMetadataAsync(id, ct);
        if (!metaResult.Success)
            return Results.NotFound(metaResult.ErrorMessage);

        var streamResult = await storage.GetAsync(id, ct);
        if (!streamResult.Success)
            return Results.NotFound(streamResult.ErrorMessage);

        return Results.File(
            streamResult.Data!,
            metaResult.Data!.ContentType,
            metaResult.Data!.Name);
    }
}
