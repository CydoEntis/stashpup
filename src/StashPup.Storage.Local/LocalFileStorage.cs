using StashPup.Core.Core;
using StashPup.Core.Interfaces;
using StashPup.Core.Models;

namespace StashPup.Storage.Local;

public class LocalFileStorage : IFileStorage
{
    public Task<Result<FileRecord>> SaveAsync(byte[] content, string fileName, FileStorageOptions? options = null)
    {
        throw new NotImplementedException();
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

    public Task<Result<IReadOnlyList<FileRecord>>> BulkSaveAsync(IEnumerable<(byte[] Content, string FileName)> files, FileStorageOptions? options = null)
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
}