namespace StashPup.Core.Models;

/// <summary>
/// Represents metadata for a stored file.
/// </summary>
public class FileRecord
{
    /// <summary>
    /// Unique identifier for the file.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Current display name of the file.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Original name when the file was uploaded.
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    /// File extension (including dot, e.g., ".jpg").
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// MIME content type (e.g., "image/jpeg").
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// When the file was originally uploaded (UTC).
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the file was last modified (UTC).
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// SHA-256 hash of file contents (optional).
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Folder/prefix where the file is stored.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Full storage path/key (provider-specific).
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Custom metadata key-value pairs.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
