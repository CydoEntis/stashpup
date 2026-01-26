using Microsoft.AspNetCore.Http;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Delete;

/// <summary>
/// Handler for file delete endpoint.
/// </summary>
internal static class DeleteHandler
{
    /// <summary>
    /// Handles file delete requests.
    /// </summary>
    public static async Task<Microsoft.AspNetCore.Http.IResult> HandleAsync(
        Guid id,
        IFileStorage storage,
        CancellationToken ct)
    {
        var result = await storage.DeleteAsync(id, ct);

        if (!result.Success || !result.Data)
        {
            return Results.NotFound(new DeleteResponse
            {
                Success = false,
                ErrorCode = result.ErrorCode,
                ErrorMessage = result.ErrorMessage ?? "File not found"
            });
        }

        return Results.NoContent();
    }
}
