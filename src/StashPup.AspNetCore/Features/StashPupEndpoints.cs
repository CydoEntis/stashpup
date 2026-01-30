using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using StashPup.AspNetCore.Features.Delete;
using StashPup.AspNetCore.Features.Download;
using StashPup.AspNetCore.Features.Folders;
using StashPup.AspNetCore.Features.List;
using StashPup.AspNetCore.Features.Metadata;
using StashPup.AspNetCore.Features.Upload;

namespace StashPup.AspNetCore.Features;

/// <summary>
/// Pre-built minimal API endpoints for StashPup.
/// </summary>
public static class StashPupEndpoints
{
    /// <summary>
    /// Maps StashPup endpoints to the route builder.
    /// Creates REST API endpoints for file upload, download, delete, metadata, and listing operations.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">URL prefix for all endpoints (default: "/api/files").</param>
    /// <param name="configure">Optional action to configure endpoint options.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <remarks>
    /// Creates the following endpoints (when enabled):
    /// - POST {prefix}/ - Upload file
    /// - GET {prefix}/{id} - Download file
    /// - DELETE {prefix}/{id} - Delete file
    /// - GET {prefix}/{id}/metadata - Get file metadata
    /// - GET {prefix}?folder=...&amp;page=1&amp;pageSize=20 - List files
    /// - GET {prefix}/folders - List all folder paths
    /// - POST {prefix}/folders - Create new empty folder
    /// - DELETE {prefix}/folders/{path} - Delete folder and contents
    /// - POST {prefix}/bulk-move - Move multiple files to new folder
    /// </remarks>
    public static IEndpointRouteBuilder MapStashPupEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/api/files",
        Action<StashPupEndpointOptions>? configure = null)
    {
        var options = new StashPupEndpointOptions();
        configure?.Invoke(options);

        var group = endpoints.MapGroup(prefix);

        if (options.RequireAuthorization)
            group.RequireAuthorization();

        if (options.EnableUpload)
            group.MapUploadEndpoint(prefix);

        if (options.EnableDownload)
            group.MapDownloadEndpoint();

        if (options.EnableDelete)
            group.MapDeleteEndpoint();

        if (options.EnableMetadata)
            group.MapMetadataEndpoint();

        if (options.EnableList)
            group.MapListEndpoint();

        if (options.EnableFolderList)
            group.MapListFoldersEndpoint();

        if (options.EnableFolderCreate)
            group.MapCreateFolderEndpoint();

        if (options.EnableFolderDelete)
            group.MapDeleteFolderEndpoint();

        if (options.EnableBulkMove)
            group.MapBulkMoveEndpoint();

        return endpoints;
    }
}
