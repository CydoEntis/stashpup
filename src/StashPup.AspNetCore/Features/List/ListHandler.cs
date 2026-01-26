using Microsoft.AspNetCore.Http;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.List;

/// <summary>
/// Handler for file list endpoint.
/// </summary>
internal static class ListHandler
{
    /// <summary>
    /// Handles file list requests.
    /// </summary>
    public static async Task<Microsoft.AspNetCore.Http.IResult> HandleAsync(
        ListRequest request,
        IFileStorage storage,
        CancellationToken ct)
    {
        var result = await storage.ListAsync(
            request.Folder,
            request.Page,
            request.PageSize,
            ct);

        if (!result.Success)
        {
            return Results.BadRequest(new ListResponse
            {
                Success = false,
                ErrorCode = result.ErrorCode,
                ErrorMessage = result.ErrorMessage
            });
        }

        var data = result.Data!;
        return Results.Ok(new ListResponse
        {
            Success = true,
            Files = data.Items.Select(f => new FileItemResponse
            {
                FileId = f.Id,
                Name = f.Name,
                OriginalName = f.OriginalName,
                SizeBytes = f.SizeBytes,
                ContentType = f.ContentType,
                Folder = f.Folder,
                CreatedAt = f.CreatedAtUtc,
                UpdatedAt = f.UpdatedAtUtc
            }).ToList(),
            TotalCount = data.TotalItems,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling(data.TotalItems / (double)request.PageSize)
        });
    }
}
