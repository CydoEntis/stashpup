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

    /// <summary>
    /// Custom S3-compatible service URL (e.g., "http://garage-server:3900").
    /// When set, the SDK connects to this endpoint instead of AWS S3.
    /// Required for S3-compatible services like Garage, MinIO, etc.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Whether to use path-style addressing (e.g., http://host/bucket/key)
    /// instead of virtual-hosted style (e.g., http://bucket.host/key).
    /// Required for most S3-compatible services like Garage.
    /// Default: false
    /// </summary>
    public bool ForcePathStyle { get; set; } = false;
}
