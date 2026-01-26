using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Metadata;

/// <summary>
/// Endpoint mapping for file metadata feature.
/// </summary>
internal static class MetadataEndpoints
{
    /// <summary>
    /// Maps the metadata endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapMetadataEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/metadata", async (
            Guid id,
            IFileStorage storage,
            CancellationToken ct) =>
            await GetMetadataHandler.HandleAsync(id, storage, ct));

        return group;
    }
}
