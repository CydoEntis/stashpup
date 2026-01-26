using StashPup.Core.Models;

namespace StashPup.Storage.Azure;

/// <summary>
/// Configuration for Azure Blob Storage.
/// </summary>
public class AzureBlobStorageOptions : FileStorageOptions
{
    /// <summary>
    /// Azure Storage connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Blob container name.
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// Optional blob name prefix.
    /// </summary>
    public string? BlobPrefix { get; set; }

    /// <summary>
    /// Default expiry for SAS tokens.
    /// Default: 1 hour
    /// </summary>
    public TimeSpan SasTokenExpiry { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether the container allows public access.
    /// Default: false
    /// </summary>
    public bool PublicAccess { get; set; } = false;

    /// <summary>
    /// Access tier for uploaded blobs.
    /// Default: Hot
    /// </summary>
    public string AccessTier { get; set; } = "Hot";

    /// <summary>
    /// Whether to create container if it doesn't exist.
    /// Default: true
    /// </summary>
    public bool CreateContainerIfNotExists { get; set; } = true;
}
