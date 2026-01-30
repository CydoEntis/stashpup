using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Folders;

/// <summary>
/// Endpoints for folder operations.
/// </summary>
public static class FoldersEndpoints
{
    /// <summary>
    /// Maps the folder listing endpoint.
    /// GET /folders - List all unique folder paths
    /// GET /folders?parent=path - List immediate children of parent folder
    /// </summary>
    public static RouteGroupBuilder MapListFoldersEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/folders", async (
            [FromServices] IFileStorage storage,
            [FromQuery] string? parent,
            CancellationToken ct) =>
        {
            var response = await FoldersHandler.HandleListFolders(storage, parent, ct);
            return response.Success
                ? Results.Ok(response)
                : Results.BadRequest(response);
        })
        .WithName("ListFolders")
        .WithSummary("List all unique folder paths")
        .WithDescription("Returns a list of all unique folder paths. Optionally filter by parent folder to get immediate children.")
        .Produces<FoldersListResponse>(StatusCodes.Status200OK)
        .Produces<FoldersListResponse>(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// Maps the folder deletion endpoint.
    /// DELETE /folders/{*folderPath} - Delete folder and its contents
    /// </summary>
    public static RouteGroupBuilder MapDeleteFolderEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/folders/{*folderPath}", async (
            [FromServices] IFileStorage storage,
            [FromRoute] string folderPath,
            [FromQuery] bool recursive = true,
            CancellationToken ct = default) =>
        {
            var response = await FoldersHandler.HandleDeleteFolder(storage, folderPath, recursive, ct);
            return response.Success
                ? Results.Ok(response)
                : Results.BadRequest(response);
        })
        .WithName("DeleteFolder")
        .WithSummary("Delete a folder and all its contents")
        .WithDescription("Deletes all files in the specified folder. Set recursive=true to include subfolders.")
        .Produces<FolderDeleteResponse>(StatusCodes.Status200OK)
        .Produces<FolderDeleteResponse>(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// Maps the bulk move endpoint.
    /// POST /bulk-move - Move multiple files to a new folder
    /// </summary>
    public static RouteGroupBuilder MapBulkMoveEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/bulk-move", async (
            [FromServices] IFileStorage storage,
            [FromBody] BulkMoveRequest request,
            CancellationToken ct) =>
        {
            if (request.FileIds == null || request.FileIds.Length == 0)
                return Results.BadRequest(new BulkMoveResponse
                {
                    Success = false,
                    ErrorMessage = "FileIds cannot be empty",
                    ErrorCode = "ValidationError"
                });

            if (string.IsNullOrWhiteSpace(request.NewFolder))
                return Results.BadRequest(new BulkMoveResponse
                {
                    Success = false,
                    ErrorMessage = "NewFolder cannot be empty",
                    ErrorCode = "ValidationError"
                });

            var response = await FoldersHandler.HandleBulkMove(
                storage,
                request.FileIds,
                request.NewFolder,
                ct);

            return response.Success
                ? Results.Ok(response)
                : Results.BadRequest(response);
        })
        .WithName("BulkMoveFiles")
        .WithSummary("Move multiple files to a new folder")
        .WithDescription("Moves multiple files to the specified destination folder in a single operation.")
        .Produces<BulkMoveResponse>(StatusCodes.Status200OK)
        .Produces<BulkMoveResponse>(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// Maps the create folder endpoint.
    /// POST /folders - Create a new empty folder
    /// </summary>
    public static RouteGroupBuilder MapCreateFolderEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/folders", async (
            [FromServices] IFileStorage storage,
            [FromBody] CreateFolderRequest request,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.FolderPath))
                return Results.BadRequest(new { error = "Folder path cannot be empty" });

            var response = await FoldersHandler.HandleCreateFolder(storage, request.FolderPath, ct);
            return Results.Ok(response);
        })
        .WithName("CreateFolder")
        .WithSummary("Create a new empty folder")
        .WithDescription("Creates an empty folder that persists even without files, similar to Google Drive.")
        .Produces<CreateFolderResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        return group;
    }
}

/// <summary>
/// Request for bulk moving files.
/// </summary>
public class BulkMoveRequest
{
    public Guid[] FileIds { get; set; } = Array.Empty<Guid>();
    public string NewFolder { get; set; } = string.Empty;
}
