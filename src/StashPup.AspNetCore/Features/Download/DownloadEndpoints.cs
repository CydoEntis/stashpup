using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Download;

/// <summary>
/// Endpoint mapping for file download feature.
/// </summary>
internal static class DownloadEndpoints
{
    /// <summary>
    /// Maps the download endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapDownloadEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (
            Guid id,
            IFileStorage storage,
            CancellationToken ct) =>
            await DownloadHandler.HandleAsync(id, storage, ct));

        return group;
    }
}
