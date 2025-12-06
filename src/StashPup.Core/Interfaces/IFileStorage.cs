using StashPup.Core.Core;
using StashPup.Core.Models;

namespace StashPup.Core.Interfaces;

public interface IFileStorage
{
    Task<Result<FileRecord>> SaveAsync(Stream content, string fileName, FileStorageOptions? options = null);


    Task<Result<(FileRecord Record, byte[] Content)>> GetAsync(Guid id);
    Task<Result<bool>> DeleteAsync(Guid id);
    Task<Result<FileRecord>> RenameAsync(Guid id, string newName);
    Task<Result<FileRecord>> MoveAsync(Guid id, string newPath);
    Task<Result<FileRecord>> CopyAsync(Guid id, string newPath);

    Task<Result<IReadOnlyList<FileRecord>>> BulkSaveAsync(IEnumerable<(Stream Content, string FileName)> files,
        FileStorageOptions? options = null);

    Task<Result<IReadOnlyList<FileRecord>>> BulkDeleteAsync(IEnumerable<Guid> ids);
    Task<Result<IReadOnlyList<FileRecord>>> BulkMoveAsync(IEnumerable<(Guid Id, string NewPath)> moves);
    Task<Result<IReadOnlyList<FileRecord>>> BulkCopyAsync(IEnumerable<(Guid Id, string NewPath)> copies);

    Task<Result<PaginatedResult<FileRecord>>> ListAsync(string? folder = null, int page = 1, int pageSize = 100);
}