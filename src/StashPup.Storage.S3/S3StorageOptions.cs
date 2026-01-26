using StashPup.Core.Models;

namespace StashPup.Storage.S3;

/// <summary>
/// Configuration for AWS S3 storage.
/// </summary>
public class S3StorageOptions : FileStorageOptions
{
    /// <summary>
    /// S3 bucket name.
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// AWS region (e.g., "us-east-1").
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// AWS access key ID. If empty, uses default credential chain.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// AWS secret access key. If empty, uses default credential chain.
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// Optional key prefix for all objects.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Default expiry for signed URLs.
    /// Default: 1 hour
    /// </summary>
    public TimeSpan SignedUrlExpiry { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether objects should be publicly readable.
    /// Default: false
    /// </summary>
    public bool PublicRead { get; set; } = false;

    /// <summary>
    /// Storage class for uploaded objects.
    /// Default: STANDARD
    /// </summary>
    public string StorageClass { get; set; } = "STANDARD";

    /// <summary>
    /// Whether to enable server-side encryption.
    /// Default: true
    /// </summary>
    public bool EnableEncryption { get; set; } = true;
}
