using Microsoft.AspNetCore.Http;

namespace StashPup.AspNetCore.Features.Upload;

/// <summary>
/// Request model for file upload endpoint.
/// </summary>
public class UploadRequest
{
    /// <summary>
    /// Gets or sets the file to upload.
    /// </summary>
    public required IFormFile File { get; set; }

    /// <summary>
    /// Gets or sets the optional folder/prefix for organization.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets or sets optional custom metadata key-value pairs.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
