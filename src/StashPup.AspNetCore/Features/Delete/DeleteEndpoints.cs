using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Delete;

/// <summary>
/// Endpoint mapping for file delete feature.
/// </summary>
internal static class DeleteEndpoints
{
    /// <summary>
    /// Maps the delete endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapDeleteEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IFileStorage storage,
            CancellationToken ct) =>
            await DeleteHandler.HandleAsync(id, storage, ct));

        return group;
    }
}
