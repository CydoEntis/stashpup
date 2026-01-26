namespace StashPup.Core.Models;

/// <summary>
/// Base configuration shared by all providers.
/// </summary>
public class FileStorageOptions
{
    /// <summary>
    /// Maximum allowed file size in bytes. Null = unlimited.
    /// Default: 10MB
    /// </summary>
    public long? MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Allowed file extensions (including dot). Empty = all allowed.
    /// Example: [".jpg", ".png", ".pdf"]
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = [];

    /// <summary>
    /// Allowed MIME content types. Empty = all allowed.
    /// Supports wildcards: ["image/*", "application/pdf"]
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = [];

    /// <summary>
    /// Whether to compute and store SHA-256 hash of file contents.
    /// Default: false (for performance)
    /// </summary>
    public bool ComputeHash { get; set; } = false;

    /// <summary>
    /// Custom naming strategy. Null = use GUID prefix.
    /// Input: original filename, Output: storage filename
    /// </summary>
    public Func<string, string>? NamingStrategy { get; set; }

    /// <summary>
    /// Strategy for organizing files into subfolders.
    /// Input: FileRecord (partial, before save), Output: subfolder path
    /// </summary>
    public Func<FileRecord, string>? SubfolderStrategy { get; set; }
}
