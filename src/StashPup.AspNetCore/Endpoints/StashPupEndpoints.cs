using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StashPup.AspNetCore;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Endpoints;

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
    /// - GET {prefix}?folder=...&page=1&pageSize=20 - List files
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
        {
            group.MapPost("/", async (
                IFormFile file,
                string? folder,
                StashPupService service,
                CancellationToken ct) =>
            {
                var result = await service.UploadAsync(file, folder, ct: ct);
                return result.Success
                    ? Results.Created($"{prefix}/{result.Data!.Id}", result.Data)
                    : Results.BadRequest(new { result.ErrorCode, result.ErrorMessage });
            })
            .DisableAntiforgery();
        }

        if (options.EnableDownload)
        {
            group.MapGet("/{id:guid}", async (
                Guid id,
                StashPupService service,
                CancellationToken ct) =>
            {
                return await service.DownloadAsync(id, ct);
            });
        }

        if (options.EnableDelete)
        {
            group.MapDelete("/{id:guid}", async (
                Guid id,
                IFileStorage storage,
                CancellationToken ct) =>
            {
                var result = await storage.DeleteAsync(id, ct);
                return result.Success && result.Data
                    ? Results.NoContent()
                    : Results.NotFound();
            });
        }

        if (options.EnableMetadata)
        {
            group.MapGet("/{id:guid}/metadata", async (
                Guid id,
                IFileStorage storage,
                CancellationToken ct) =>
            {
                var result = await storage.GetMetadataAsync(id, ct);
                return result.Success
                    ? Results.Ok(result.Data)
                    : Results.NotFound();
            });
        }

        if (options.EnableList)
        {
            group.MapGet("/", async (
                string? folder,
                int page,
                int pageSize,
                IFileStorage storage,
                CancellationToken ct) =>
            {
                var result = await storage.ListAsync(folder, page, pageSize, ct);
                return result.Success
                    ? Results.Ok(result.Data)
                    : Results.BadRequest(new { result.ErrorCode, result.ErrorMessage });
            });
        }

        return endpoints;
    }
}

/// <summary>
/// Options for configuring StashPup endpoints.
/// </summary>
public class StashPupEndpointOptions
{
    /// <summary>
    /// Gets or sets whether endpoints require authorization.
    /// Default: false
    /// </summary>
    public bool RequireAuthorization { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable the upload endpoint (POST).
    /// Default: true
    /// </summary>
    public bool EnableUpload { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the download endpoint (GET /{id}).
    /// Default: true
    /// </summary>
    public bool EnableDownload { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the delete endpoint (DELETE /{id}).
    /// Default: true
    /// </summary>
    public bool EnableDelete { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the metadata endpoint (GET /{id}/metadata).
    /// Default: true
    /// </summary>
    public bool EnableMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable the list endpoint (GET /?folder=...&page=...).
    /// Default: false (disabled by default for security)
    /// </summary>
    public bool EnableList { get; set; } = false;
}
