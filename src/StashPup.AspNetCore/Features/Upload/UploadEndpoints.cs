using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StashPup.Core.Interfaces;

namespace StashPup.AspNetCore.Features.Upload;

/// <summary>
/// Endpoint mapping for file upload feature.
/// </summary>
internal static class UploadEndpoints
{
    /// <summary>
    /// Maps the upload endpoint to the route group.
    /// </summary>
    public static RouteGroupBuilder MapUploadEndpoint(this RouteGroupBuilder group, string prefix)
    {
        group.MapPost("/", async (
            HttpContext context,
            IFileStorage storage,
            CancellationToken ct) =>
        {
            var form = await context.Request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { success = false, errorMessage = "No file provided" });
            }

            var folder = form["folder"].ToString();
            
            // Parse metadata from form (if provided as individual fields like metadata[key])
            Dictionary<string, string>? metadata = null;
            var metadataKeys = form.Keys.Where(k => k.StartsWith("metadata[") && k.EndsWith("]"));
            if (metadataKeys.Any())
            {
                metadata = new Dictionary<string, string>();
                foreach (var key in metadataKeys)
                {
                    var metaKey = key.Substring(9, key.Length - 10); // Extract key from "metadata[key]"
                    metadata[metaKey] = form[key].ToString();
                }
            }

            var request = new UploadRequest
            {
                File = file,
                Folder = string.IsNullOrEmpty(folder) ? null : folder,
                Metadata = metadata
            };
            
            return await UploadHandler.HandleAsync(request, storage, prefix, ct);
        })
        .DisableAntiforgery();

        return group;
    }
}
