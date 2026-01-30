using System.Text.Json;
using StashPup.Core.Core;
using StashPup.Core.Interfaces;
using StashPup.Core.Models;
using StashPup.Core.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace StashPup.Storage.Local;

/// <summary>
/// Local filesystem provider implementation.
/// Stores files on the local filesystem with metadata companion files.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly LocalStorageOptions _options;
    private const int BufferSize = 81920;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileStorage"/> class.
    /// </summary>
    /// <param name="options">Local storage configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public LocalFileStorage(LocalStorageOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string ProviderName => "Local";

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
            var resolvedFolder = ResolveFolder(folder, fileName, fileId);
            var fullPath = GetFilePath(fileId, resolvedFolder, storageFileName);
            var fullDirectory = Path.GetDirectoryName(fullPath)!;

            var pathValidation = LocalStorageValidator.ValidateLocalFilePath(fullPath, _options);
            if (!pathValidation.Success)
                return Result<FileRecord>.Fail(pathValidation);

            if (_options.AutoCreateDirectories && !Directory.Exists(fullDirectory))
                Directory.CreateDirectory(fullDirectory);

            if (!_options.OverwriteExisting && File.Exists(fullPath))
                return Result<FileRecord>.Fail(
                    FileStorageErrors.FileAlreadyExistsMessage(storageFileName),
                    FileStorageErrors.FileAlreadyExists);

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

            long totalBytes = 0;
            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[BufferSize];
            int bytesRead;

            while ((bytesRead = await content.ReadAsync(buffer, ct)) > 0)
            {
                totalBytes += bytesRead;

                if (_options.MaxFileSizeBytes.HasValue && totalBytes > _options.MaxFileSizeBytes.Value)
                {
                    await fileStream.DisposeAsync();
                    File.Delete(fullPath);
                    return Result<FileRecord>.Fail(
                        FileStorageErrors.MaxFileSizeExceededMessage(_options.MaxFileSizeBytes.Value),
                        FileStorageErrors.MaxFileSizeExceeded);
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            }

            var record = new FileRecord
            {
                Id = fileId,
                Name = fileName,
                OriginalName = fileName,
                Extension = extension,
                ContentType = contentType,
                SizeBytes = totalBytes,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Hash = hash,
                Folder = resolvedFolder,
                StoragePath = fullPath,
                Metadata = metadata
            };

            await SaveMetadataAsync(record, ct);

            return Result<FileRecord>.Ok(record);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (UnauthorizedAccessException)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.PermissionDeniedMessage(),
                FileStorageErrors.PermissionDenied);
        }
        catch (IOException ex) when (ex.Message.Contains("No space left on device") || ex.Message.Contains("disk is full"))
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.DiskFullMessage(),
                FileStorageErrors.DiskFull);
        }
        catch (IOException)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.IOErrorMessage(),
                FileStorageErrors.IOError);
        }
        catch (OutOfMemoryException)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.MemoryErrorMessage(),
                FileStorageErrors.MemoryError);
        }
        catch (ArgumentException)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.InvalidFileNameMessage(),
                FileStorageErrors.InvalidFileName);
        }
        catch (Exception)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
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
            var filePath = record.StoragePath;

            if (!File.Exists(filePath))
                return Result<Stream>.Fail(
                    FileStorageErrors.FileNotFoundMessage(id),
                    FileStorageErrors.FileNotFound);

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Result<Stream>.Ok(stream);
        }
        catch (OperationCanceledException)
        {
            return Result<Stream>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (UnauthorizedAccessException)
        {
            return Result<Stream>.Fail(
                FileStorageErrors.PermissionDeniedMessage(),
                FileStorageErrors.PermissionDenied);
        }
        catch (FileNotFoundException)
        {
            return Result<Stream>.Fail(
                FileStorageErrors.FileNotFoundMessage(id),
                FileStorageErrors.FileNotFound);
        }
        catch (Exception)
        {
            return Result<Stream>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<FileRecord>> GetMetadataAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var metadataPath = GetMetadataPath(id);
            if (File.Exists(metadataPath))
            {
                var json = await File.ReadAllTextAsync(metadataPath, ct);
                var record = JsonSerializer.Deserialize<FileRecord>(json);
                if (record != null)
                    return Result<FileRecord>.Ok(record);
            }

            var foundPath = FindFileById(id);
            if (foundPath == null)
                return Result<FileRecord>.Fail(
                    FileStorageErrors.FileNotFoundMessage(id),
                    FileStorageErrors.FileNotFound);

            var fileInfo = new FileInfo(foundPath);
            var extension = Path.GetExtension(foundPath);
            var fileName = Path.GetFileName(foundPath);

            var fileRecord = new FileRecord
            {
                Id = id,
                Name = fileName,
                OriginalName = fileName,
                Extension = extension,
                ContentType = FileStorageValidator.DetectContentType(File.OpenRead(foundPath), fileName),
                SizeBytes = fileInfo.Length,
                CreatedAtUtc = fileInfo.CreationTimeUtc,
                UpdatedAtUtc = fileInfo.LastWriteTimeUtc,
                Folder = Path.GetRelativePath(_options.BasePath, fileInfo.DirectoryName ?? ""),
                StoragePath = foundPath
            };

            return Result<FileRecord>.Ok(fileRecord);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var metadataResult = await GetMetadataAsync(id, ct);
            if (!metadataResult.Success)
                return Result<bool>.Ok(false);

            var record = metadataResult.Data!;
            var deleted = false;

            if (File.Exists(record.StoragePath))
            {
                File.Delete(record.StoragePath);
                deleted = true;
            }

            var metadataPath = GetMetadataPath(id);
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);

            return Result<bool>.Ok(deleted);
        }
        catch (OperationCanceledException)
        {
            return Result<bool>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception)
        {
            return Result<bool>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
                FileStorageErrors.UnexpectedError);
        }
    }

    public Task<Result<bool>> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var metadataPath = GetMetadataPath(id);
            if (File.Exists(metadataPath))
                return Task.FromResult(Result<bool>.Ok(true));

            var foundPath = FindFileById(id);
            return Task.FromResult(Result<bool>.Ok(foundPath != null));
        }
        catch (Exception)
        {
            return Task.FromResult(Result<bool>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
                FileStorageErrors.UnexpectedError));
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

            await SaveMetadataAsync(record, ct);
            return Result<FileRecord>.Ok(record);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
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
            var oldPath = record.StoragePath;
            var extension = Path.GetExtension(record.StoragePath);
            var storageFileName = $"{id}{extension}";
            var newFolderPath = string.IsNullOrWhiteSpace(newFolder) ? "" : newFolder.Trim('/');
            var newFullPath = Path.Combine(_options.BasePath, newFolderPath, storageFileName);
            var newDirectory = Path.GetDirectoryName(newFullPath)!;

            if (_options.AutoCreateDirectories && !Directory.Exists(newDirectory))
                Directory.CreateDirectory(newDirectory);

            if (File.Exists(oldPath))
                File.Move(oldPath, newFullPath);

            record.Folder = newFolderPath;
            record.StoragePath = newFullPath;
            record.UpdatedAtUtc = DateTime.UtcNow;

            await SaveMetadataAsync(record, ct);
            return Result<FileRecord>.Ok(record);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
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
            var newFolderPath = string.IsNullOrWhiteSpace(newFolder) ? "" : newFolder.Trim('/');
            var newFullPath = Path.Combine(_options.BasePath, newFolderPath, storageFileName);
            var newDirectory = Path.GetDirectoryName(newFullPath)!;

            if (_options.AutoCreateDirectories && !Directory.Exists(newDirectory))
                Directory.CreateDirectory(newDirectory);

            if (File.Exists(record.StoragePath))
                File.Copy(record.StoragePath, newFullPath);

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
                Folder = newFolderPath,
                StoragePath = newFullPath,
                Metadata = record.Metadata
            };

            await SaveMetadataAsync(newRecord, ct);
            return Result<FileRecord>.Ok(newRecord);
        }
        catch (OperationCanceledException)
        {
            return Result<FileRecord>.Fail(
                "Operation was cancelled.",
                FileStorageErrors.OperationCancelled);
        }
        catch (Exception)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
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

            var searchPath = string.IsNullOrWhiteSpace(folder)
                ? _options.BasePath
                : Path.Combine(_options.BasePath, folder.Trim('/'));

            if (!Directory.Exists(searchPath))
                return Result<PaginatedResult<FileRecord>>.Ok(new PaginatedResult<FileRecord>
                {
                    Items = [],
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = 0
                });

            var allFiles = new List<FileRecord>();
            var metadataFiles = Directory.GetFiles(searchPath, "*.meta.json", SearchOption.AllDirectories);

            foreach (var metaFile in metadataFiles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var json = await File.ReadAllTextAsync(metaFile, ct);
                    var record = JsonSerializer.Deserialize<FileRecord>(json);
                    // Filter out placeholder files from list results
                    if (record != null && record.Name != ".stashpup_folder")
                        allFiles.Add(record);
                }
                catch
                {
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
        catch (Exception)
        {
            return Result<PaginatedResult<FileRecord>>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
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

            var searchPath = string.IsNullOrWhiteSpace(searchParameters.Folder)
                ? _options.BasePath
                : Path.Combine(_options.BasePath, searchParameters.Folder.Trim('/'));

            if (!Directory.Exists(searchPath))
                return Result<PaginatedResult<FileRecord>>.Ok(new PaginatedResult<FileRecord>
                {
                    Items = [],
                    Page = searchParameters.Page,
                    PageSize = searchParameters.PageSize,
                    TotalItems = 0
                });

            var allFiles = new List<FileRecord>();
            var metadataFiles = Directory.GetFiles(searchPath, "*.meta.json", SearchOption.AllDirectories);

            foreach (var metaFile in metadataFiles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var json = await File.ReadAllTextAsync(metaFile, ct);
                    var record = JsonSerializer.Deserialize<FileRecord>(json);
                    // Filter out placeholder files from search results
                    if (record != null && 
                        record.Name != ".stashpup_folder" && 
                        MatchesSearchCriteria(record, searchParameters))
                        allFiles.Add(record);
                }
                catch
                {
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
        catch (Exception)
        {
            return Result<PaginatedResult<FileRecord>>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
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

            if (!File.Exists(record.StoragePath))
                return Result<Stream>.Fail(
                    FileStorageErrors.FileNotFoundMessage(id),
                    FileStorageErrors.FileNotFound);

            var thumbnailPath = GetThumbnailPath(id, size);
            var thumbnailDir = Path.GetDirectoryName(thumbnailPath)!;

            if (!Directory.Exists(thumbnailDir))
                Directory.CreateDirectory(thumbnailDir);

            if (File.Exists(thumbnailPath))
            {
                var thumbnailInfo = new FileInfo(thumbnailPath);
                var sourceInfo = new FileInfo(record.StoragePath);

                if (thumbnailInfo.LastWriteTimeUtc >= sourceInfo.LastWriteTimeUtc)
                {
                    var existingStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return Result<Stream>.Ok(existingStream);
                }
            }

            await GenerateThumbnailAsync(record.StoragePath, thumbnailPath, size, ct);

            var thumbnailStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Result<Stream>.Ok(thumbnailStream);
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

    public string? GetPublicUrl(Guid id)
    {
        return $"{_options.BaseUrl.TrimEnd('/')}/{id}";
    }

    public async Task<Result<string>> GetSignedUrlAsync(
        Guid id,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        if (!_options.EnableSignedUrls)
            return Result<string>.Fail(
                "Signed URLs are not enabled for this provider.",
                FileStorageErrors.SignedUrlNotSupported);

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            return Result<string>.Fail(
                "Signing key is not configured.",
                FileStorageErrors.SignedUrlNotSupported);

        // Verify file exists
        var existsResult = await ExistsAsync(id, ct);
        if (!existsResult.Success || !existsResult.Data)
            return Result<string>.Fail(
                FileStorageErrors.FileNotFoundMessage(id),
                FileStorageErrors.FileNotFound);

        // Generate signed URL (simplified - in production use proper HMAC)
        var baseUrl = GetPublicUrl(id);
        var expires = DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeSeconds();
        var signature = ComputeSignature($"{id}:{expires}", _options.SigningKey);
        var signedUrl = $"{baseUrl}?expires={expires}&signature={signature}";

        return Result<string>.Ok(signedUrl);
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
        catch (Exception)
        {
            return Result<IReadOnlyList<FileRecord>>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> ListFoldersAsync(
        string? parentFolder = null,
        CancellationToken ct = default)
    {
        try
        {
            if (!Directory.Exists(_options.BasePath))
                return Result<IReadOnlyList<string>>.Ok(Array.Empty<string>());

            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var metadataFiles = Directory.GetFiles(_options.BasePath, "*.meta.json", SearchOption.AllDirectories);

            foreach (var metaFile in metadataFiles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var json = await File.ReadAllTextAsync(metaFile, ct);
                    var record = JsonSerializer.Deserialize<FileRecord>(json);
                    
                    if (record?.Folder != null)
                    {
                        var folder = record.Folder.Trim('/');
                        
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
                catch
                {
                    // Skip invalid metadata files
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
        catch (Exception)
        {
            return Result<IReadOnlyList<string>>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
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
        catch (Exception)
        {
            return Result<int>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
                FileStorageErrors.UnexpectedError);
        }
    }

    public async Task<Result<string>> CreateFolderAsync(string folderPath, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return Result<string>.Fail(
                    "Folder path cannot be empty.",
                    FileStorageErrors.ValidationFailed);

            var normalizedPath = folderPath.Trim('/');
            
            // Check if folder already exists by looking for any files in it
            var existingFolders = await ListFoldersAsync(null, ct);
            if (existingFolders.Success && existingFolders.Data!.Any(f => f == normalizedPath))
            {
                return Result<string>.Ok(normalizedPath); // Already exists
            }

            // Create placeholder file to make folder exist
            var placeholderName = ".stashpup_folder";
            var placeholderContent = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("placeholder"));
            
            var saveResult = await SaveAsync(placeholderContent, placeholderName, normalizedPath, null, ct);
            
            if (!saveResult.Success)
                return Result<string>.Fail(saveResult);

            return Result<string>.Ok(normalizedPath);
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
                $"Failed to create folder: {ex.Message}",
                FileStorageErrors.UnexpectedError);
        }
    }

    private string GetFilePath(Guid id, string? folder, string fileName)
    {
        var folderPath = string.IsNullOrWhiteSpace(folder) ? "" : folder.Trim('/');
        return Path.Combine(_options.BasePath, folderPath, fileName);
    }

    private string GetMetadataPath(Guid id)
    {
        var metadataDir = Path.Combine(_options.BasePath, ".metadata");
        if (!Directory.Exists(metadataDir))
            Directory.CreateDirectory(metadataDir);

        return Path.Combine(metadataDir, $"{id}.meta.json");
    }

    private async Task SaveMetadataAsync(FileRecord record, CancellationToken ct)
    {
        var metadataPath = GetMetadataPath(record.Id);
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = false });
        await File.WriteAllTextAsync(metadataPath, json, ct);
    }

    private string ResolveFolder(string? folder, string fileName, Guid fileId)
    {
        if (!string.IsNullOrWhiteSpace(folder))
            return folder.Trim('/');

        if (_options.SubfolderStrategy != null)
        {
            var tempRecord = new FileRecord
            {
                Id = fileId,
                Name = fileName,
                CreatedAtUtc = DateTime.UtcNow
            };
            var strategyFolder = _options.SubfolderStrategy(tempRecord);
            if (!string.IsNullOrWhiteSpace(strategyFolder))
                return strategyFolder.Trim('/');
        }

        return "";
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

    private string? FindFileById(Guid id)
    {
        if (!Directory.Exists(_options.BasePath))
            return null;

        var searchPattern = $"{id}.*";
        var files = Directory.GetFiles(_options.BasePath, searchPattern, SearchOption.AllDirectories);
        return files.FirstOrDefault();
    }

    private string ComputeSignature(string data, string key)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(key));
        var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    private string GetThumbnailPath(Guid id, ThumbnailSize size)
    {
        var thumbnailsDir = Path.Combine(_options.BasePath, ".thumbnails", size.ToString().ToLowerInvariant());
        return Path.Combine(thumbnailsDir, $"{id}.jpg");
    }

    private bool IsImageContentType(string contentType)
    {
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
               !contentType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase); // SVG not supported for thumbnails
    }

    private async Task GenerateThumbnailAsync(string sourcePath, string thumbnailPath, ThumbnailSize size, CancellationToken ct)
    {
        try
        {
            using var sourceImage = await Image.LoadAsync(sourcePath, ct);
            var targetSize = (int)size;
            var (width, height) = CalculateThumbnailDimensions(sourceImage.Width, sourceImage.Height, targetSize);

            sourceImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));

            await sourceImage.SaveAsync(thumbnailPath, new JpegEncoder { Quality = 85 }, ct);
        }
        catch (UnknownImageFormatException)
        {
            throw new NotSupportedException("Image format not supported or file corrupted");
        }
        catch (InvalidImageContentException)
        {
            throw new NotSupportedException("Invalid image format");
        }
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

    private bool MatchesSearchCriteria(FileRecord record, SearchParameters parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters.NamePattern))
        {
            var pattern = parameters.NamePattern.Replace("*", ".*").Replace("?", ".");
            var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!regex.IsMatch(record.Name))
                return false;
        }

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

        if (!string.IsNullOrWhiteSpace(parameters.FolderStartsWith))
        {
            var recordFolder = record.Folder ?? "";
            var folderPrefix = parameters.FolderStartsWith.Trim('/');
            if (!recordFolder.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(parameters.Extension))
        {
            if (!record.Extension.Equals(parameters.Extension, StringComparison.OrdinalIgnoreCase))
                return false;
        }

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

        if (parameters.MinSizeBytes.HasValue && record.SizeBytes < parameters.MinSizeBytes.Value)
            return false;

        if (parameters.MaxSizeBytes.HasValue && record.SizeBytes > parameters.MaxSizeBytes.Value)
            return false;

        if (parameters.CreatedAfter.HasValue && record.CreatedAtUtc < parameters.CreatedAfter.Value)
            return false;

        if (parameters.CreatedBefore.HasValue && record.CreatedAtUtc > parameters.CreatedBefore.Value)
            return false;

        if (parameters.UpdatedAfter.HasValue && record.UpdatedAtUtc < parameters.UpdatedAfter.Value)
            return false;

        if (parameters.UpdatedBefore.HasValue && record.UpdatedAtUtc > parameters.UpdatedBefore.Value)
            return false;

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

            _ => files // Default: no sorting
        };
    }
}
