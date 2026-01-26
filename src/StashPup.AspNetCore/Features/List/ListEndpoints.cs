using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.List;

/// <summary>
/// Endpoint mapping for file list feature.
/// </summary>
internal static class ListEndpoints
{
    /// <summary>
    /// Maps the list endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapListEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            IFileStorage storage,
            [FromQuery] string? folder = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var request = new ListRequest
            {
                Folder = folder,
                Page = page,
                PageSize = pageSize
            };
            return await ListHandler.HandleAsync(request, storage, ct);
        });

        return group;
    }
}
