using StashPup.Core.Models;

namespace StashPup.Storage.Local;

/// <summary>
/// Configuration specific to local filesystem storage.
/// </summary>
public class LocalStorageOptions : FileStorageOptions
{
    /// <summary>
    /// Base directory path for file storage.
    /// Can be absolute or relative to application root.
    /// Default: "./uploads"
    /// </summary>
    public string BasePath { get; set; } = "./uploads";

    /// <summary>
    /// Base URL path for serving files via middleware.
    /// Default: "/files"
    /// </summary>
    public string BaseUrl { get; set; } = "/files";

    /// <summary>
    /// Whether to overwrite existing files with same name.
    /// Default: false (will return error)
    /// </summary>
    public bool OverwriteExisting { get; set; } = false;

    /// <summary>
    /// Whether to automatically create directories if they don't exist.
    /// Default: true
    /// </summary>
    public bool AutoCreateDirectories { get; set; } = true;

    /// <summary>
    /// Whether to support signed URLs (requires additional setup).
    /// Default: false
    /// </summary>
    public bool EnableSignedUrls { get; set; } = false;

    /// <summary>
    /// Secret key for signing URLs (required if EnableSignedUrls = true).
    /// </summary>
    public string? SigningKey { get; set; }
}

