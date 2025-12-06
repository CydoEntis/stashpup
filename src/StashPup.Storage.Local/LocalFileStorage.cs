using StashPup.Core.Core;
using StashPup.Core.Interfaces;
using StashPup.Core.Models;
using StashPup.Core.Validation;

namespace StashPup.Storage.Local;

public class LocalFileStorage : IFileStorage
{
    private LocalStorageOptions _options { get; set; }

    public LocalFileStorage(LocalStorageOptions options)
    {
        _options = options;
    }


    public async Task<Result<FileRecord>> SaveAsync(Stream content, string fileName, FileStorageOptions? options = null)
    {
        try
        {
            var selectedOptions = (options as LocalStorageOptions) ?? _options;

            var fileValidationResult = FileStorageValidator.ValidateFile(content, fileName, selectedOptions);
            if (!fileValidationResult.Success)
                return Result<FileRecord>.Fail(fileValidationResult.ErrorMessage, fileValidationResult.ErrorCode);

            string finalFileName = ResolveFileName(fileName, selectedOptions);
            string subFolder = ResolveSubFolder(finalFileName, selectedOptions);

            string fullPath = ComputeFullPath(selectedOptions.BasePath, subFolder, finalFileName);
            var filePathValidator = LocalStorageValidator.ValidateLocalFilePath(fullPath, selectedOptions);
            if (!filePathValidator.Success)
                return Result<FileRecord>.Fail(filePathValidator.ErrorMessage, filePathValidator.ErrorCode);

            var request = new FileSaveRequest
            {
                Content = content,
                OriginalFileName = fileName,
                Options = selectedOptions,
                FinalFileName = finalFileName,
                SubFolder = subFolder,
                Hash = null,
                ContentType = null,
                Metadata = null
            };

            return await _SaveStreamAsync(request);
        }
        catch (Exception)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
                FileStorageErrors.UnexpectedError
            );
        }
    }


    public Task<Result<(FileRecord Record, byte[] Content)>> GetAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<Result<bool>> DeleteAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<Result<FileRecord>> RenameAsync(Guid id, string newName)
    {
        throw new NotImplementedException();
    }

    public Task<Result<FileRecord>> MoveAsync(Guid id, string newPath)
    {
        throw new NotImplementedException();
    }

    public Task<Result<FileRecord>> CopyAsync(Guid id, string newPath)
    {
        throw new NotImplementedException();
    }

    public Task<Result<IReadOnlyList<FileRecord>>> BulkSaveAsync(IEnumerable<(Stream Content, string FileName)> files,
        FileStorageOptions? options = null)
    {
        throw new NotImplementedException();
    }


    public Task<Result<IReadOnlyList<FileRecord>>> BulkDeleteAsync(IEnumerable<Guid> ids)
    {
        throw new NotImplementedException();
    }

    public Task<Result<IReadOnlyList<FileRecord>>> BulkMoveAsync(IEnumerable<(Guid Id, string NewPath)> moves)
    {
        throw new NotImplementedException();
    }

    public Task<Result<IReadOnlyList<FileRecord>>> BulkCopyAsync(IEnumerable<(Guid Id, string NewPath)> copies)
    {
        throw new NotImplementedException();
    }

    public Task<Result<PaginatedResult<FileRecord>>> ListAsync(string? folder = null, int page = 1, int pageSize = 100)
    {
        throw new NotImplementedException();
    }


    private async Task<Result<FileRecord>> _SaveStreamAsync(FileSaveRequest request)
    {
        string fullDirectory = Path.Combine(request.Options.BasePath, request.SubFolder);
        string fullPath = Path.Combine(fullDirectory, request.FinalFileName!);

        try
        {
            if (request.Options.AutoCreateDirectories && !Directory.Exists(fullDirectory))
                Directory.CreateDirectory(fullDirectory);

            if (!request.Options.OverwriteExisting && File.Exists(fullPath))
                return Result<FileRecord>.Fail(
                    FileStorageErrors.FileAlreadyExistsMessage(request.FinalFileName!),
                    FileStorageErrors.FileAlreadyExists
                );

            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

            byte[] buffer = new byte[81920];
            int bytesRead;
            long totalBytesRead = 0;

            while ((bytesRead = await request.Content.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalBytesRead += bytesRead;

                if (request.Options.MaxFileSizeBytes.HasValue &&
                    totalBytesRead > request.Options.MaxFileSizeBytes.Value)
                    return Result<FileRecord>.Fail(
                        FileStorageErrors.MaxFileSizeExceededMessage(request.Options.MaxFileSizeBytes.Value),
                        FileStorageErrors.MaxFileSizeExceeded
                    );

                await fileStream.WriteAsync(buffer, 0, bytesRead);
            }

            var savedFileRecord = new FileRecord
            {
                Id = Guid.NewGuid(),
                Name = request.FinalFileName!,
                OriginalName = request.OriginalFileName,
                Extension = Path.GetExtension(request.FinalFileName),
                SizeBytes = totalBytesRead,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Hash = request.Hash,
                FullPath = fullPath,
                SubFolder = request.SubFolder,
                ContentType = request.ContentType,
                Metadata = request.Metadata
            };

            return Result<FileRecord>.Ok(savedFileRecord);
        }
        catch (UnauthorizedAccessException)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.PermissionDeniedMessage(),
                FileStorageErrors.PermissionDenied
            );
        }
        catch (IOException ex) when (ex.Message.Contains("No space left on device"))
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.DiskFullMessage(),
                FileStorageErrors.DiskFull
            );
        }
        catch (IOException)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.IOErrorMessage(),
                FileStorageErrors.IOError
            );
        }
        catch (OutOfMemoryException)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.MemoryErrorMessage(),
                FileStorageErrors.MemoryError
            );
        }
        catch (ArgumentException)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.InvalidFileNameMessage(),
                FileStorageErrors.InvalidFileName
            );
        }
        catch (Exception)
        {
            return Result<FileRecord>.Fail(
                FileStorageErrors.UnexpectedErrorMessage(),
                FileStorageErrors.UnexpectedError
            );
        }
    }

    private string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private string ResolveFileName(string fileName, LocalStorageOptions options)
    {
        var extension = Path.GetExtension(fileName);

        if (options.HashFileName)
            return ComputeHash(fileName) + extension;

        if (options.NamingStrategy is not null)
            return options.NamingStrategy(fileName) + extension;

        return fileName;
    }

    private string ResolveSubFolder(string finalFileName, LocalStorageOptions options)
    {
        if (options.SubFolderStrategy is null)
            return string.Empty;

        var tempRecord = new FileRecord { Name = finalFileName };
        return options.SubFolderStrategy(tempRecord) ?? string.Empty;
    }

    private string ComputeFullPath(string basePath, string subFolder, string finalFileName)
    {
        return Path.Combine(basePath, subFolder, finalFileName);
    }
}