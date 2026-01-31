using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Text.Json;
using System.Text.RegularExpressions;
using StashPup.Core.Core;
using StashPup.Core.Interfaces;
using StashPup.Core.Models;
using StashPup.Core.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace StashPup.Storage.Azure;

/// <summary>
/// Azure Blob Storage provider implementation.
/// Stores files in Azure Blob Storage with support for SAS tokens and access tiers.
/// </summary>
public class AzureBlobFileStorage : IFileStorage
{
    private readonly AzureBlobStorageOptions _options;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private const int BufferSize = 81920;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobFileStorage"/> class.
    /// </summary>
    /// <param name="options">Azure Blob Storage configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public AzureBlobFileStorage(AzureBlobStorageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        _containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);

        if (_options.CreateContainerIfNotExists)
        {
            EnsureContainerExistsAsync().GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc/>
    public string ProviderName => "AzureBlob";

    public async Task<Result<FileRecord>> SaveAsync(
        Stream content,
        string fileName,
        string? folder = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            if (content.CanSeek && content.Position > 0)
                content.Position = 0;

            var validation = FileStorageValidator.Validate(content, fileName, _options);
            if (!validation.Success)
                return Result<FileRecord>.Fail(validation);

            var fileId = Guid.NewGuid();
            var extension = Path.GetExtension(fileName);
            var storageFileName = $"{fileId}{extension}";
            var blobName = BuildBlobName(fileId, folder, storageFileName);
            
            var contentType = FileStorageValidator.DetectContentType(content, fileName);
            if (content.CanSeek)
                content.Position = 0;

            string? hash = null;
            if (_options.ComputeHash)
            {
                hash = await ComputeFileHashAsync(content, ct);
                if (content.CanSeek)
                    content.Position = 0;
            }

            var blobClient = _containerClient.GetBlobClient(blobName);

            var blobMetadata = new Dictionary<string, string>
            {
                ["file-id"] = fileId.ToString(),
                ["original-name"] = fileName,
                ["extension"] = extension
            };

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    blobMetadata[$"custom-{kvp.Key}"] = kvp.Value;
                }
            }

            var uploadOptions = new BlobUploadOptions
            {
                Metadata = blobMetadata,
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            };

            if (!string.IsNullOrWhiteSpace(_options.AccessTier))
            {
                uploadOptions.AccessTier = new AccessTier(_options.AccessTier);
            }

            await blobClient.UploadAsync(content, uploadOptions, ct);

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);

            var record = new FileRecord
            {
                Id = fileId,
                Name = fileName,
                OriginalName = fileName,
                Extension = extension,
                ContentType = contentType,
                SizeBytes = properties.Value.ContentLength,
                CreatedAtUtc = properties.Value.CreatedOn.UtcDateTime,
                UpdatedAtUtc = properties.Value.LastModified.UtcDateTime,
                Hash = hash,
                Folder = folder,
                StoragePath = blobName,
                Metadata = metadata
            };

            return Result<FileRecord>.Ok(record);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (RequestFailedException ex)
        {
            return Result<FileRecord>.Fail(
                $"Azure error: {ex.Message}",
                FileStorageErrors.ProviderError);
        }
        catch (Exception ex)
        {
            return Result<FileRecord>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<Stream>> GetAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var metadataResult = await GetMetadataAsync(id, ct);
            if (!metadataResult.Success)
                return Result<Stream>.Fail(metadataResult);

            var record = metadataResult.Data!;
            var blobName = record.StoragePath;

            var blobClient = _containerClient.GetBlobClient(blobName);
            var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);

            return Result<Stream>.Ok(response.Value.Content);
        }
        catch (OperationCanceledException)
        {
            return Result<Stream>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Result<Stream>.Fail(
                FileStorageErrors.FileNotFoundMessage(id),
                FileStorageErrors.FileNotFound);
        }
        catch (RequestFailedException ex)
        {
            return Result<Stream>.Fail(
                $"Azure error: {ex.Message}",
                FileStorageErrors.ProviderError);
        }
        catch (Exception ex)
        {
            return Result<Stream>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<FileRecord>> GetMetadataAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            await foreach (var blobItem in _containerClient.GetBlobsAsync(BlobTraits.Metadata, cancellationToken: ct))
            {
                if (blobItem.Metadata.TryGetValue("file-id", out var fileIdStr) && fileIdStr == id.ToString())
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);

                    var record = new FileRecord
                    {
                        Id = id,
                        Name = blobItem.Metadata.TryGetValue("original-name", out var origName) ? origName : Path.GetFileName(blobItem.Name),
                        OriginalName = blobItem.Metadata.TryGetValue("original-name", out var origName2) ? origName2 : Path.GetFileName(blobItem.Name),
                        Extension = blobItem.Metadata.TryGetValue("extension", out var ext) ? ext : Path.GetExtension(blobItem.Name),
                        ContentType = properties.Value.ContentType,
                        SizeBytes = properties.Value.ContentLength,
                        CreatedAtUtc = properties.Value.CreatedOn.UtcDateTime,
                        UpdatedAtUtc = properties.Value.LastModified.UtcDateTime,
                        Folder = ExtractFolderFromBlobName(blobItem.Name),
                        StoragePath = blobItem.Name
                    };

                    var customMetadata = new Dictionary<string, string>();
                    foreach (var kvp in blobItem.Metadata)
                    {
                        if (kvp.Key.StartsWith("custom-"))
                        {
                            customMetadata[kvp.Key.Substring(7)] = kvp.Value;
                        }
                    }
                    if (customMetadata.Any())
                        record.Metadata = customMetadata;

                    return Result<FileRecord>.Ok(record);
                }
            }

            return Result<FileRecord>.Fail(
                FileStorageErrors.FileNotFoundMessage(id),
                FileStorageErrors.FileNotFound);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<FileRecord>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var metadataResult = await GetMetadataAsync(id, ct);
            if (!metadataResult.Success)
                return Result<bool>.Ok(false); // Not found = already deleted

            var record = metadataResult.Data!;
            var blobName = record.StoragePath;

            var blobClient = _containerClient.GetBlobClient(blobName);
            var result = await blobClient.DeleteIfExistsAsync(cancellationToken: ct);

            return Result<bool>.Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<bool>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<bool>> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await GetMetadataAsync(id, ct);
            return Result<bool>.Ok(result.Success);
        }
        catch
        {
            return Result<bool>.Ok(false);
        }
    }


    public async Task<Result<FileRecord>> RenameAsync(Guid id, string newName, CancellationToken ct = default)
    {
        try
        {
            var metadataResult = await GetMetadataAsync(id, ct);
            if (!metadataResult.Success)
                return Result<FileRecord>.Fail(metadataResult);

            var record = metadataResult.Data!;
            record.Name = newName;
            record.UpdatedAtUtc = DateTime.UtcNow;

            var blobClient = _containerClient.GetBlobClient(record.StoragePath);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);

            var metadata = new Dictionary<string, string>(properties.Value.Metadata);
            metadata["original-name"] = newName;

            await blobClient.SetMetadataAsync(metadata, cancellationToken: ct);
            return Result<FileRecord>.Ok(record);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<FileRecord>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<FileRecord>> MoveAsync(Guid id, string newFolder, CancellationToken ct = default)
    {
        try
        {
            var metadataResult = await GetMetadataAsync(id, ct);
            if (!metadataResult.Success)
                return Result<FileRecord>.Fail(metadataResult);

            var record = metadataResult.Data!;
            var oldBlobName = record.StoragePath;
            var extension = Path.GetExtension(oldBlobName);
            var storageFileName = $"{id}{extension}";
            var newBlobName = BuildBlobName(id, newFolder, storageFileName);

            var sourceBlob = _containerClient.GetBlobClient(oldBlobName);
            var destBlob = _containerClient.GetBlobClient(newBlobName);
            await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: ct);

            var properties = await destBlob.GetPropertiesAsync(cancellationToken: ct);
            while (properties.Value.CopyStatus == CopyStatus.Pending)
            {
                await Task.Delay(100, ct);
                properties = await destBlob.GetPropertiesAsync(cancellationToken: ct);
            }

            await sourceBlob.DeleteIfExistsAsync(cancellationToken: ct);

            record.Folder = newFolder;
            record.StoragePath = newBlobName;
            record.UpdatedAtUtc = DateTime.UtcNow;

            return Result<FileRecord>.Ok(record);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<FileRecord>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<FileRecord>> CopyAsync(Guid id, string newFolder, CancellationToken ct = default)
    {
        try
        {
            var metadataResult = await GetMetadataAsync(id, ct);
            if (!metadataResult.Success)
                return Result<FileRecord>.Fail(metadataResult);

            var record = metadataResult.Data!;
            var newId = Guid.NewGuid();
            var extension = Path.GetExtension(record.StoragePath);
            var storageFileName = $"{newId}{extension}";
            var newBlobName = BuildBlobName(newId, newFolder, storageFileName);

            var sourceBlob = _containerClient.GetBlobClient(record.StoragePath);
            var destBlob = _containerClient.GetBlobClient(newBlobName);
            await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: ct);

            var properties = await destBlob.GetPropertiesAsync(cancellationToken: ct);
            while (properties.Value.CopyStatus == CopyStatus.Pending)
            {
                await Task.Delay(100, ct);
                properties = await destBlob.GetPropertiesAsync(cancellationToken: ct);
            }

            var newMetadata = new Dictionary<string, string>
            {
                ["file-id"] = newId.ToString(),
                ["original-name"] = record.OriginalName,
                ["extension"] = record.Extension
            };
            if (record.Metadata != null)
            {
                foreach (var kvp in record.Metadata)
                {
                    newMetadata[$"custom-{kvp.Key}"] = kvp.Value;
                }
            }
            await destBlob.SetMetadataAsync(newMetadata, cancellationToken: ct);

            var newRecord = new FileRecord
            {
                Id = newId,
                Name = record.Name,
                OriginalName = record.OriginalName,
                Extension = record.Extension,
                ContentType = record.ContentType,
                SizeBytes = record.SizeBytes,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Hash = record.Hash,
                Folder = newFolder,
                StoragePath = newBlobName,
                Metadata = record.Metadata
            };

            return Result<FileRecord>.Ok(newRecord);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<FileRecord>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }


    public async Task<Result<IReadOnlyList<FileRecord>>> BulkSaveAsync(
        IEnumerable<BulkSaveItem> files,
        string? folder = null,
        CancellationToken ct = default)
    {
        var results = new List<FileRecord>();
        var errors = new List<string>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var result = await SaveAsync(file.Content, file.FileName, folder, file.Metadata, ct);
            if (result.Success)
                results.Add(result.Data!);
            else
                errors.Add(result.ErrorMessage ?? "Unknown error");
        }

        if (errors.Any())
            return Result<IReadOnlyList<FileRecord>>.Fail(
                $"Some files failed to save: {string.Join("; ", errors)}",
                FileStorageErrors.ValidationFailed);

        return Result<IReadOnlyList<FileRecord>>.Ok(results);
    }

    public async Task<Result<IReadOnlyList<Guid>>> BulkDeleteAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default)
    {
        var deleted = new List<Guid>();

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();

            var result = await DeleteAsync(id, ct);
            if (result.Success && result.Data)
                deleted.Add(id);
        }

        return Result<IReadOnlyList<Guid>>.Ok(deleted);
    }


    public async Task<Result<PaginatedResult<FileRecord>>> ListAsync(
        string? folder = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 100;
            if (pageSize > 1000) pageSize = 1000;

            var prefix = BuildBlobName(Guid.Empty, folder, "");
            var allFiles = new List<FileRecord>();

            await foreach (var blobItem in _containerClient.GetBlobsAsync(
                BlobTraits.Metadata,
                BlobStates.None,
                prefix,
                cancellationToken: ct))
            {
                if (blobItem.Metadata.TryGetValue("file-id", out var fileIdStr) && Guid.TryParse(fileIdStr, out var fileId))
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);

                            var record = new FileRecord
                            {
                                Id = fileId,
                                Name = blobItem.Metadata.TryGetValue("original-name", out var origName3) ? origName3 : Path.GetFileName(blobItem.Name),
                                OriginalName = blobItem.Metadata.TryGetValue("original-name", out var origName4) ? origName4 : Path.GetFileName(blobItem.Name),
                                Extension = blobItem.Metadata.TryGetValue("extension", out var ext2) ? ext2 : Path.GetExtension(blobItem.Name),
                        ContentType = properties.Value.ContentType,
                        SizeBytes = properties.Value.ContentLength,
                        CreatedAtUtc = properties.Value.CreatedOn.UtcDateTime,
                        UpdatedAtUtc = properties.Value.LastModified.UtcDateTime,
                        Folder = ExtractFolderFromBlobName(blobItem.Name),
                        StoragePath = blobItem.Name
                    };

                    var customMetadata = new Dictionary<string, string>();
                    foreach (var kvp in blobItem.Metadata)
                    {
                        if (kvp.Key.StartsWith("custom-"))
                        {
                            customMetadata[kvp.Key.Substring(7)] = kvp.Value;
                        }
                    }
                    if (customMetadata.Any())
                        record.Metadata = customMetadata;

                    allFiles.Add(record);
                }
            }

            var totalItems = allFiles.Count;
            var skip = (page - 1) * pageSize;
            var items = allFiles.Skip(skip).Take(pageSize).ToList();

            return Result<PaginatedResult<FileRecord>>.Ok(new PaginatedResult<FileRecord>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            });
        }
        catch (OperationCanceledException)
        {
            return Result<PaginatedResult<FileRecord>>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<PaginatedResult<FileRecord>>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }


    public string? GetPublicUrl(Guid id)
    {
        if (!_options.PublicAccess)
            return null;

        try
        {
            var metadataResult = GetMetadataAsync(id).GetAwaiter().GetResult();
            if (!metadataResult.Success)
                return null;

            var record = metadataResult.Data!;
            var blobClient = _containerClient.GetBlobClient(record.StoragePath);
            return blobClient.Uri.ToString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<Result<string>> GetSignedUrlAsync(
        Guid id,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        try
        {
            var metadataResult = await GetMetadataAsync(id, ct);
            if (!metadataResult.Success)
                return Result<string>.Fail(metadataResult);

            var record = metadataResult.Data!;
            var blobClient = _containerClient.GetBlobClient(record.StoragePath);

            // Generate SAS token
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerClient.Name,
                BlobName = record.StoragePath,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return Result<string>.Ok(sasUri.ToString());
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<PaginatedResult<FileRecord>>> SearchAsync(
        SearchParameters searchParameters,
        CancellationToken ct = default)
    {
        try
        {
            if (searchParameters.Page < 1) searchParameters.Page = 1;
            if (searchParameters.PageSize < 1) searchParameters.PageSize = 100;
            if (searchParameters.PageSize > 1000) searchParameters.PageSize = 1000;

            var prefix = BuildBlobName(Guid.Empty, searchParameters.Folder, "");
            var allFiles = new List<FileRecord>();

            await foreach (var blobItem in _containerClient.GetBlobsAsync(
                BlobTraits.Metadata,
                BlobStates.None,
                prefix,
                cancellationToken: ct))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (!blobItem.Metadata.TryGetValue("file-id", out var fileIdStr) ||
                        !Guid.TryParse(fileIdStr, out var fileId))
                        continue;

                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);

                    var record = new FileRecord
                    {
                        Id = fileId,
                        Name = blobItem.Metadata.TryGetValue("original-name", out var origName) ? origName : Path.GetFileName(blobItem.Name),
                        OriginalName = blobItem.Metadata.TryGetValue("original-name", out var origName2) ? origName2 : Path.GetFileName(blobItem.Name),
                        Extension = blobItem.Metadata.TryGetValue("extension", out var ext) ? ext : Path.GetExtension(blobItem.Name),
                        ContentType = properties.Value.ContentType,
                        SizeBytes = properties.Value.ContentLength,
                        CreatedAtUtc = properties.Value.CreatedOn.UtcDateTime,
                        UpdatedAtUtc = properties.Value.LastModified.UtcDateTime,
                        Folder = ExtractFolderFromBlobName(blobItem.Name),
                        StoragePath = blobItem.Name
                    };

                    var customMetadata = new Dictionary<string, string>();
                    foreach (var kvp in blobItem.Metadata)
                    {
                        if (kvp.Key.StartsWith("custom-"))
                        {
                            customMetadata[kvp.Key.Substring(7)] = kvp.Value;
                        }
                    }
                    if (customMetadata.Any())
                        record.Metadata = customMetadata;

                    if (MatchesSearchCriteria(record, searchParameters))
                        allFiles.Add(record);
                }
                catch
                {
                    // Skip invalid blobs
                }
            }

            allFiles = ApplySorting(allFiles, searchParameters.SortBy, searchParameters.SortDirection);

            var totalItems = allFiles.Count;
            var skip = (searchParameters.Page - 1) * searchParameters.PageSize;
            var items = allFiles.Skip(skip).Take(searchParameters.PageSize).ToList();

            return Result<PaginatedResult<FileRecord>>.Ok(new PaginatedResult<FileRecord>
            {
                Items = items,
                Page = searchParameters.Page,
                PageSize = searchParameters.PageSize,
                TotalItems = totalItems
            });
        }
        catch (OperationCanceledException)
        {
            return Result<PaginatedResult<FileRecord>>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<PaginatedResult<FileRecord>>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<Stream>> GetThumbnailAsync(
        Guid id,
        ThumbnailSize size = ThumbnailSize.Medium,
        CancellationToken ct = default)
    {
        try
        {
            var metadataResult = await GetMetadataAsync(id, ct);
            if (!metadataResult.Success)
                return Result<Stream>.Fail(metadataResult);

            var record = metadataResult.Data!;

            if (!IsImageContentType(record.ContentType))
                return Result<Stream>.Fail(
                    $"File {id} is not an image. Content type: {record.ContentType}",
                    FileStorageErrors.InvalidFileType);

            var thumbnailBlobName = $".thumbnails/{size.ToString().ToLowerInvariant()}/{id}.jpg";
            var thumbnailClient = _containerClient.GetBlobClient(thumbnailBlobName);
            var sourceClient = _containerClient.GetBlobClient(record.StoragePath);

            try
            {
                var thumbnailProperties = await thumbnailClient.GetPropertiesAsync(cancellationToken: ct);
                var sourceProperties = await sourceClient.GetPropertiesAsync(cancellationToken: ct);

                if (thumbnailProperties.Value.LastModified >= sourceProperties.Value.LastModified)
                {
                    var thumbnailResponse = await thumbnailClient.DownloadStreamingAsync(cancellationToken: ct);
                    return Result<Stream>.Ok(thumbnailResponse.Value.Content);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Thumbnail doesn't exist, need to generate it
            }

            var sourceResponse = await sourceClient.DownloadStreamingAsync(cancellationToken: ct);

            using var sourceImage = await Image.LoadAsync(sourceResponse.Value.Content, ct);
            var targetSize = (int)size;
            var (width, height) = CalculateThumbnailDimensions(sourceImage.Width, sourceImage.Height, targetSize);

            sourceImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));

            using var thumbnailStream = new MemoryStream();
            await sourceImage.SaveAsync(thumbnailStream, new JpegEncoder { Quality = 85 }, ct);
            thumbnailStream.Position = 0;

            await thumbnailClient.UploadAsync(thumbnailStream, overwrite: true, cancellationToken: ct);

            thumbnailStream.Position = 0;
            var resultStream = new MemoryStream();
            await thumbnailStream.CopyToAsync(resultStream, ct);
            resultStream.Position = 0;
            return Result<Stream>.Ok(resultStream);
        }
        catch (OperationCanceledException)
        {
            return Result<Stream>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (NotSupportedException ex)
        {
            return Result<Stream>.Fail(
                $"Image format not supported: {ex.Message}",
                FileStorageErrors.InvalidFileType);
        }
        catch (Exception ex)
        {
            return Result<Stream>.Fail(
                $"Failed to generate thumbnail: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    private string BuildBlobName(Guid id, string? folder, string fileName)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(_options.BlobPrefix))
            parts.Add(_options.BlobPrefix.TrimEnd('/'));

        if (!string.IsNullOrWhiteSpace(folder))
            parts.Add(folder.Trim('/'));

        parts.Add(fileName);

        return string.Join("/", parts);
    }

    private string? ExtractFolderFromBlobName(string blobName)
    {
        var prefix = _options.BlobPrefix ?? "";
        if (blobName.StartsWith(prefix))
            blobName = blobName.Substring(prefix.Length).TrimStart('/');

        var lastSlash = blobName.LastIndexOf('/');
        if (lastSlash > 0)
            return blobName.Substring(0, lastSlash);

        return null;
    }

    public async Task<Result<IReadOnlyList<FileRecord>>> BulkMoveAsync(
        IEnumerable<Guid> ids,
        string newFolder,
        CancellationToken ct = default)
    {
        try
        {
            var movedRecords = new List<FileRecord>();
            var idsList = ids.ToList();

            foreach (var id in idsList)
            {
                ct.ThrowIfCancellationRequested();

                var moveResult = await MoveAsync(id, newFolder, ct);
                if (moveResult.Success)
                    movedRecords.Add(moveResult.Data!);
            }

            return Result<IReadOnlyList<FileRecord>>.Ok(movedRecords);
        }
        catch (OperationCanceledException)
        {
            return Result<IReadOnlyList<FileRecord>>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<FileRecord>>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> ListFoldersAsync(
        string? parentFolder = null,
        CancellationToken ct = default)
    {
        try
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefix = string.IsNullOrWhiteSpace(parentFolder) 
                ? "" 
                : BuildBlobName(Guid.Empty, parentFolder.Trim('/'), "");

            await foreach (var blobItem in _containerClient.GetBlobsAsync(
                BlobTraits.Metadata,
                BlobStates.None,
                prefix,
                cancellationToken: ct))
            {
                ct.ThrowIfCancellationRequested();

                var folder = ExtractFolderFromBlobName(blobItem.Name);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    folder = folder.Trim('/');
                    
                    if (string.IsNullOrWhiteSpace(parentFolder))
                    {
                        // No parent filter - add all unique folders
                        folders.Add(folder);
                    }
                    else
                    {
                        // Filter by parent folder - return immediate children only
                        var parent = parentFolder.Trim('/');
                        if (folder.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            var relative = folder.Substring(parent.Length + 1);
                            var firstSegment = relative.Split('/')[0];
                            folders.Add($"{parent}/{firstSegment}");
                        }
                    }
                }
            }

            return Result<IReadOnlyList<string>>.Ok(folders.OrderBy(f => f).ToList());
        }
        catch (OperationCanceledException)
        {
            return Result<IReadOnlyList<string>>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<string>>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<int>> DeleteFolderAsync(
        string folder,
        bool recursive = true,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder))
                return Result<int>.Fail(
                    "Folder path cannot be empty.",
                    FileStorageErrors.ValidationFailed);

            var folderPath = folder.Trim('/');
            var searchParams = new SearchParameters
            {
                FolderStartsWith = recursive ? folderPath : null,
                Folder = recursive ? null : folderPath,
                IncludeSubfolders = false,
                PageSize = 10000
            };

            var searchResult = await SearchAsync(searchParams, ct);
            if (!searchResult.Success)
                return Result<int>.Fail(searchResult);

            var fileIds = searchResult.Data!.Items.Select(f => f.Id).ToList();
            
            if (fileIds.Count == 0)
                return Result<int>.Ok(0);

            var deleteResult = await BulkDeleteAsync(fileIds, ct);
            if (!deleteResult.Success)
                return Result<int>.Fail(deleteResult);

            return Result<int>.Ok(deleteResult.Data!.Count);
        }
        catch (OperationCanceledException)
        {
            return Result<int>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            return Result<int>.Fail(
                $"Unexpected error: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    private async Task<string> ComputeFileHashAsync(Stream content, CancellationToken ct)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var buffer = new byte[BufferSize];
        int bytesRead;
        var originalPosition = content.Position;
        content.Position = 0;

        try
        {
            while ((bytesRead = await content.ReadAsync(buffer, ct)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hashBytes = sha256.Hash ?? throw new InvalidOperationException("Hash computation failed");
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        finally
        {
            content.Position = originalPosition;
        }
    }

    private bool MatchesSearchCriteria(FileRecord record, SearchParameters parameters)
    {
        // Name pattern matching (supports * and ? wildcards)
        if (!string.IsNullOrWhiteSpace(parameters.NamePattern))
        {
            var pattern = parameters.NamePattern.Replace("*", ".*").Replace("?", ".");
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            if (!regex.IsMatch(record.Name))
                return false;
        }

        // Folder filtering (exact match or starts with)
        if (!string.IsNullOrWhiteSpace(parameters.Folder))
        {
            var recordFolder = record.Folder ?? "";
            var searchFolder = parameters.Folder.Trim('/');
            
            if (parameters.IncludeSubfolders)
            {
                // Match exact folder or subfolders
                if (!recordFolder.Equals(searchFolder, StringComparison.OrdinalIgnoreCase) &&
                    !recordFolder.StartsWith(searchFolder + "/", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
            {
                // Match exact folder only
                if (!recordFolder.Equals(searchFolder, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        // FolderStartsWith filtering (for nested folder queries)
        if (!string.IsNullOrWhiteSpace(parameters.FolderStartsWith))
        {
            var recordFolder = record.Folder ?? "";
            var folderPrefix = parameters.FolderStartsWith.Trim('/');
            if (!recordFolder.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Extension filtering
        if (!string.IsNullOrWhiteSpace(parameters.Extension))
        {
            if (!record.Extension.Equals(parameters.Extension, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Content type filtering (supports wildcards like image/*)
        if (!string.IsNullOrWhiteSpace(parameters.ContentType))
        {
            if (parameters.ContentType.EndsWith("/*"))
            {
                var contentTypePrefix = parameters.ContentType[..^1];
                if (!record.ContentType.StartsWith(contentTypePrefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else if (!record.ContentType.Equals(parameters.ContentType, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Size filtering
        if (parameters.MinSizeBytes.HasValue && record.SizeBytes < parameters.MinSizeBytes.Value)
            return false;

        if (parameters.MaxSizeBytes.HasValue && record.SizeBytes > parameters.MaxSizeBytes.Value)
            return false;

        // Date filtering
        if (parameters.CreatedAfter.HasValue && record.CreatedAtUtc < parameters.CreatedAfter.Value)
            return false;

        if (parameters.CreatedBefore.HasValue && record.CreatedAtUtc > parameters.CreatedBefore.Value)
            return false;

        if (parameters.UpdatedAfter.HasValue && record.UpdatedAtUtc < parameters.UpdatedAfter.Value)
            return false;

        if (parameters.UpdatedBefore.HasValue && record.UpdatedAtUtc > parameters.UpdatedBefore.Value)
            return false;

        // Metadata filtering (all specified key-value pairs must match)
        if (parameters.Metadata != null && parameters.Metadata.Any())
        {
            if (record.Metadata == null)
                return false;

            foreach (var kvp in parameters.Metadata)
            {
                if (!record.Metadata.TryGetValue(kvp.Key, out var recordValue) ||
                    !recordValue.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    private List<FileRecord> ApplySorting(List<FileRecord> files, SearchSortField sortBy, SearchSortDirection direction)
    {
        return sortBy switch
        {
            SearchSortField.Name => direction == SearchSortDirection.Ascending
                ? files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList()
                : files.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList(),

            SearchSortField.Size => direction == SearchSortDirection.Ascending
                ? files.OrderBy(f => f.SizeBytes).ToList()
                : files.OrderByDescending(f => f.SizeBytes).ToList(),

            SearchSortField.CreatedAt => direction == SearchSortDirection.Ascending
                ? files.OrderBy(f => f.CreatedAtUtc).ToList()
                : files.OrderByDescending(f => f.CreatedAtUtc).ToList(),

            SearchSortField.UpdatedAt => direction == SearchSortDirection.Ascending
                ? files.OrderBy(f => f.UpdatedAtUtc).ToList()
                : files.OrderByDescending(f => f.UpdatedAtUtc).ToList(),

            SearchSortField.Extension => direction == SearchSortDirection.Ascending
                ? files.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase).ToList()
                : files.OrderByDescending(f => f.Extension, StringComparer.OrdinalIgnoreCase).ToList(),

            SearchSortField.ContentType => direction == SearchSortDirection.Ascending
                ? files.OrderBy(f => f.ContentType, StringComparer.OrdinalIgnoreCase).ToList()
                : files.OrderByDescending(f => f.ContentType, StringComparer.OrdinalIgnoreCase).ToList(),

            _ => files
        };
    }

    private bool IsImageContentType(string contentType)
    {
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
               !contentType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase);
    }

    private (int width, int height) CalculateThumbnailDimensions(int originalWidth, int originalHeight, int maxSize)
    {
        if (originalWidth <= maxSize && originalHeight <= maxSize)
            return (originalWidth, originalHeight);

        var aspectRatio = (double)originalWidth / originalHeight;
        
        if (originalWidth > originalHeight)
        {
            return (maxSize, (int)(maxSize / aspectRatio));
        }
        else
        {
            return ((int)(maxSize * aspectRatio), maxSize);
        }
    }

    private async Task EnsureContainerExistsAsync()
    {
        try
        {
            await _containerClient.CreateIfNotExistsAsync(
                _options.PublicAccess ? PublicAccessType.Blob : PublicAccessType.None);
        }
        catch
        {
            // Ignore errors - container might exist or we might not have permissions
        }
    }
}
