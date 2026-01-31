using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using System.Text.Json;
using System.Text.RegularExpressions;
using StashPup.Core.Core;
using StashPup.Core.Interfaces;
using StashPup.Core.Models;
using StashPup.Core.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace StashPup.Storage.S3;

/// <summary>
/// AWS S3 provider implementation.
/// Stores files in Amazon S3 with support for pre-signed URLs and server-side encryption.
/// </summary>
public class S3FileStorage : IFileStorage
{
    private readonly S3StorageOptions _options;
    private readonly IAmazonS3 _s3Client;
    private const int BufferSize = 81920;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3FileStorage"/> class.
    /// </summary>
    /// <param name="options">S3 storage configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public S3FileStorage(S3StorageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region)
        };

        if (!string.IsNullOrWhiteSpace(options.AccessKeyId) && !string.IsNullOrWhiteSpace(options.SecretAccessKey))
        {
            _s3Client = new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, config);
        }
        else
        {
            _s3Client = new AmazonS3Client(config);
        }

        EnsureBucketExistsAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public string ProviderName => "S3";

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
            var key = BuildS3Key(fileId, folder, storageFileName);
            
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

            var putRequest = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                StorageClass = new S3StorageClass(_options.StorageClass),
                ServerSideEncryptionMethod = _options.EnableEncryption ? ServerSideEncryptionMethod.AES256 : null
            };

            if (_options.PublicRead)
                putRequest.CannedACL = S3CannedACL.PublicRead;

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    putRequest.Metadata.Add($"x-amz-meta-{kvp.Key}", kvp.Value);
                }
            }

            putRequest.Metadata.Add("x-amz-meta-file-id", fileId.ToString());
            putRequest.Metadata.Add("x-amz-meta-original-name", fileName);
            putRequest.Metadata.Add("x-amz-meta-extension", extension);

            await _s3Client.PutObjectAsync(putRequest, ct);

            var getRequest = new GetObjectMetadataRequest
            {
                BucketName = _options.BucketName,
                Key = key
            };
            var metadataResponse = await _s3Client.GetObjectMetadataAsync(getRequest, ct);

            var record = new FileRecord
            {
                Id = fileId,
                Name = fileName,
                OriginalName = fileName,
                Extension = extension,
                ContentType = contentType,
                SizeBytes = metadataResponse.ContentLength,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Hash = hash,
                Folder = folder,
                StoragePath = key,
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
        catch (AmazonS3Exception ex)
        {
            return Result<FileRecord>.Fail(
                $"S3 error: {ex.Message}",
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
            var key = record.StoragePath;

            var request = new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request, ct);
            return Result<Stream>.Ok(response.ResponseStream);
        }
        catch (OperationCanceledException)
        {
            return Result<Stream>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result<Stream>.Fail(
                FileStorageErrors.FileNotFoundMessage(id),
                FileStorageErrors.FileNotFound);
        }
        catch (AmazonS3Exception ex)
        {
            return Result<Stream>.Fail(
                $"S3 error: {ex.Message}",
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
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = _options.KeyPrefix ?? ""
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest, ct);

            foreach (var s3Object in listResponse.S3Objects)
            {
                try
                {
                    var metadataRequest = new GetObjectMetadataRequest
                    {
                        BucketName = _options.BucketName,
                        Key = s3Object.Key
                    };
                    var metadataResponse = await _s3Client.GetObjectMetadataAsync(metadataRequest, ct);

                    var fileIdMeta = metadataResponse.Metadata["x-amz-meta-file-id"];
                    if (fileIdMeta == id.ToString())
                    {
                        var record = new FileRecord
                        {
                            Id = id,
                            Name = metadataResponse.Metadata["x-amz-meta-original-name"] ?? Path.GetFileName(s3Object.Key),
                            OriginalName = metadataResponse.Metadata["x-amz-meta-original-name"] ?? Path.GetFileName(s3Object.Key),
                            Extension = metadataResponse.Metadata["x-amz-meta-extension"] ?? Path.GetExtension(s3Object.Key),
                            ContentType = metadataResponse.Headers.ContentType,
                            SizeBytes = metadataResponse.ContentLength,
                            CreatedAtUtc = s3Object.LastModified.ToUniversalTime(),
                            UpdatedAtUtc = s3Object.LastModified.ToUniversalTime(),
                            Folder = ExtractFolderFromKey(s3Object.Key),
                            StoragePath = s3Object.Key
                        };

                        var customMetadata = new Dictionary<string, string>();
                        var systemKeys = new HashSet<string> { "x-amz-meta-file-id", "x-amz-meta-original-name", "x-amz-meta-extension" };
                        foreach (var key in metadataResponse.Metadata.Keys)
                        {
                            if (key.StartsWith("x-amz-meta-") && !systemKeys.Contains(key))
                            {
                                var metadataKey = key.Substring("x-amz-meta-".Length);
                                customMetadata[metadataKey] = metadataResponse.Metadata[key];
                            }
                        }
                        if (customMetadata.Any())
                            record.Metadata = customMetadata;

                        return Result<FileRecord>.Ok(record);
                    }
                }
                catch
                {
                    // Continue searching
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
            var key = record.StoragePath;

            var request = new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, ct);
            return Result<bool>.Ok(true);
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

            var copyRequest = new CopyObjectRequest
            {
                SourceBucket = _options.BucketName,
                SourceKey = record.StoragePath,
                DestinationBucket = _options.BucketName,
                DestinationKey = record.StoragePath,
                MetadataDirective = S3MetadataDirective.REPLACE
            };

            var getRequest = new GetObjectMetadataRequest
            {
                BucketName = _options.BucketName,
                Key = record.StoragePath
            };
            var metadataResponse = await _s3Client.GetObjectMetadataAsync(getRequest, ct);
            foreach (var key in metadataResponse.Metadata.Keys)
            {
                copyRequest.Metadata.Add(key, metadataResponse.Metadata[key]);
            }
            copyRequest.Metadata["x-amz-meta-original-name"] = newName;

            await _s3Client.CopyObjectAsync(copyRequest, ct);
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
            var oldKey = record.StoragePath;
            var extension = Path.GetExtension(oldKey);
            var storageFileName = $"{id}{extension}";
            var newKey = BuildS3Key(id, newFolder, storageFileName);

            var copyRequest = new CopyObjectRequest
            {
                SourceBucket = _options.BucketName,
                SourceKey = oldKey,
                DestinationBucket = _options.BucketName,
                DestinationKey = newKey,
                MetadataDirective = S3MetadataDirective.COPY
            };
            await _s3Client.CopyObjectAsync(copyRequest, ct);

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = oldKey
            };
            await _s3Client.DeleteObjectAsync(deleteRequest, ct);

            record.Folder = newFolder;
            record.StoragePath = newKey;
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
            var newKey = BuildS3Key(newId, newFolder, storageFileName);

            var copyRequest = new CopyObjectRequest
            {
                SourceBucket = _options.BucketName,
                SourceKey = record.StoragePath,
                DestinationBucket = _options.BucketName,
                DestinationKey = newKey,
                MetadataDirective = S3MetadataDirective.COPY
            };
            await _s3Client.CopyObjectAsync(copyRequest, ct);

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
                StoragePath = newKey,
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

            var prefix = BuildS3Key(Guid.Empty, folder, "");
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = 1000
            };

            var allFiles = new List<FileRecord>();
            ListObjectsV2Response? response;

            do
            {
                response = await _s3Client.ListObjectsV2Async(listRequest, ct);
                foreach (var s3Object in response.S3Objects)
                {
                    try
                    {
                        var metadataRequest = new GetObjectMetadataRequest
                        {
                            BucketName = _options.BucketName,
                            Key = s3Object.Key
                        };
                        var metadataResponse = await _s3Client.GetObjectMetadataAsync(metadataRequest, ct);

                        var fileIdMeta = metadataResponse.Metadata["x-amz-meta-file-id"];
                        if (Guid.TryParse(fileIdMeta, out var fileId))
                        {
                            var record = new FileRecord
                            {
                                Id = fileId,
                                Name = metadataResponse.Metadata["x-amz-meta-original-name"] ?? Path.GetFileName(s3Object.Key),
                                OriginalName = metadataResponse.Metadata["x-amz-meta-original-name"] ?? Path.GetFileName(s3Object.Key),
                                Extension = metadataResponse.Metadata["x-amz-meta-extension"] ?? Path.GetExtension(s3Object.Key),
                                ContentType = metadataResponse.Headers.ContentType,
                                SizeBytes = metadataResponse.ContentLength,
                                CreatedAtUtc = s3Object.LastModified.ToUniversalTime(),
                                UpdatedAtUtc = s3Object.LastModified.ToUniversalTime(),
                                Folder = ExtractFolderFromKey(s3Object.Key),
                                StoragePath = s3Object.Key
                            };

                            var customMetadata = new Dictionary<string, string>();
                            var systemKeys = new HashSet<string> { "x-amz-meta-file-id", "x-amz-meta-original-name", "x-amz-meta-extension" };
                            foreach (var key in metadataResponse.Metadata.Keys)
                            {
                                if (key.StartsWith("x-amz-meta-") && !systemKeys.Contains(key))
                                {
                                    var metadataKey = key.Substring("x-amz-meta-".Length);
                                    customMetadata[metadataKey] = metadataResponse.Metadata[key];
                                }
                            }
                            if (customMetadata.Any())
                                record.Metadata = customMetadata;

                            allFiles.Add(record);
                        }
                    }
                    catch
                    {
                        // Skip invalid objects
                    }
                }

                listRequest.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

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
        if (!_options.PublicRead)
            return null;

        try
        {
            var metadataResult = GetMetadataAsync(id).GetAwaiter().GetResult();
            if (!metadataResult.Success)
                return null;

            var record = metadataResult.Data!;
            var region = Amazon.RegionEndpoint.GetBySystemName(_options.Region);
            return $"https://{_options.BucketName}.s3.{region.SystemName}.amazonaws.com/{record.StoragePath}";
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
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.BucketName,
                Key = record.StoragePath,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiry)
            };

            var url = _s3Client.GetPreSignedURL(request);
            return Result<string>.Ok(url);
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

            var prefix = BuildS3Key(Guid.Empty, searchParameters.Folder, "");
            var allFiles = new List<FileRecord>();

            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                Prefix = prefix,
                MaxKeys = 1000
            };

            ListObjectsV2Response? response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(listRequest, ct);
                
                foreach (var s3Object in response.S3Objects)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        var metadataRequest = new GetObjectMetadataRequest
                        {
                            BucketName = _options.BucketName,
                            Key = s3Object.Key
                        };
                        var metadataResponse = await _s3Client.GetObjectMetadataAsync(metadataRequest, ct);

                        var fileIdMeta = metadataResponse.Metadata["x-amz-meta-file-id"];
                        if (!Guid.TryParse(fileIdMeta, out var fileId))
                            continue;

                        var record = new FileRecord
                        {
                            Id = fileId,
                            Name = metadataResponse.Metadata["x-amz-meta-original-name"] ?? Path.GetFileName(s3Object.Key),
                            OriginalName = metadataResponse.Metadata["x-amz-meta-original-name"] ?? Path.GetFileName(s3Object.Key),
                            Extension = metadataResponse.Metadata["x-amz-meta-extension"] ?? Path.GetExtension(s3Object.Key),
                            ContentType = metadataResponse.Headers.ContentType,
                            SizeBytes = metadataResponse.ContentLength,
                            CreatedAtUtc = s3Object.LastModified.ToUniversalTime(),
                            UpdatedAtUtc = s3Object.LastModified.ToUniversalTime(),
                            Folder = ExtractFolderFromKey(s3Object.Key),
                            StoragePath = s3Object.Key
                        };

                        // Extract custom metadata (all x-amz-meta-* except system ones)
                        var customMetadata = new Dictionary<string, string>();
                        var systemKeys = new HashSet<string> { "x-amz-meta-file-id", "x-amz-meta-original-name", "x-amz-meta-extension" };
                        foreach (var key in metadataResponse.Metadata.Keys)
                        {
                            if (key.StartsWith("x-amz-meta-") && !systemKeys.Contains(key))
                            {
                                var metadataKey = key.Substring("x-amz-meta-".Length);
                                customMetadata[metadataKey] = metadataResponse.Metadata[key];
                            }
                        }
                        if (customMetadata.Any())
                            record.Metadata = customMetadata;

                        if (MatchesSearchCriteria(record, searchParameters))
                            allFiles.Add(record);
                    }
                    catch
                    {
                        // Skip invalid objects
                    }
                }

                listRequest.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

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

            var thumbnailKey = $".thumbnails/{size.ToString().ToLowerInvariant()}/{id}.jpg";

            try
            {
                var thumbnailMetadata = await _s3Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest
                    {
                        BucketName = _options.BucketName,
                        Key = thumbnailKey
                    }, ct);

                var sourceMetadata = await _s3Client.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest
                    {
                        BucketName = _options.BucketName,
                        Key = record.StoragePath
                    }, ct);

                if (thumbnailMetadata.LastModified >= sourceMetadata.LastModified)
                {
                    var thumbnailResponse = await _s3Client.GetObjectAsync(
                        new GetObjectRequest
                        {
                            BucketName = _options.BucketName,
                            Key = thumbnailKey
                        }, ct);
                    return Result<Stream>.Ok(thumbnailResponse.ResponseStream);
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Thumbnail doesn't exist, need to generate it
            }

            var sourceResponse = await _s3Client.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = record.StoragePath
                }, ct);

            using var sourceImage = await Image.LoadAsync(sourceResponse.ResponseStream, ct);
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

            var putRequest = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = thumbnailKey,
                InputStream = thumbnailStream,
                ContentType = "image/jpeg",
                ServerSideEncryptionMethod = _options.EnableEncryption ? ServerSideEncryptionMethod.AES256 : null
            };
            await _s3Client.PutObjectAsync(putRequest, ct);

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

    private string BuildS3Key(Guid id, string? folder, string fileName)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(_options.KeyPrefix))
            parts.Add(_options.KeyPrefix.TrimEnd('/'));

        if (!string.IsNullOrWhiteSpace(folder))
            parts.Add(folder.Trim('/'));

        parts.Add(fileName);

        return string.Join("/", parts);
    }

    private string? ExtractFolderFromKey(string key)
    {
        var prefix = _options.KeyPrefix ?? "";
        if (key.StartsWith(prefix))
            key = key.Substring(prefix.Length).TrimStart('/');

        var lastSlash = key.LastIndexOf('/');
        if (lastSlash > 0)
            return key.Substring(0, lastSlash);

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
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                MaxKeys = 1000
            };

            if (!string.IsNullOrWhiteSpace(parentFolder))
            {
                var parent = parentFolder.Trim('/');
                listRequest.Prefix = BuildS3Key(Guid.Empty, parent, "");
            }

            ListObjectsV2Response? response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(listRequest, ct);

                foreach (var s3Object in response.S3Objects)
                {
                    ct.ThrowIfCancellationRequested();

                    var folder = ExtractFolderFromKey(s3Object.Key);
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

                listRequest.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

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

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var request = new PutBucketRequest
            {
                BucketName = _options.BucketName,
                BucketRegion = _options.Region
            };
            await _s3Client.PutBucketAsync(request);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // Bucket already exists, that's fine
        }
        catch
        {
            // Ignore other errors - bucket might exist or we might not have permissions
        }
    }
}
