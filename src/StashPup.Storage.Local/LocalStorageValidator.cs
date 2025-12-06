using StashPup.Core.Core;
using StashPup.Core.Models;

namespace StashPup.Storage.Local;

public class LocalStorageValidator
{
    public static Result<bool> ValidateLocalFilePath(string fullPath, LocalStorageOptions options)
    {
        if (File.Exists(fullPath) && !options.OverwriteExisting)
            return Result<bool>.Fail(
                FileStorageErrors.FileAlreadyExistsMessage(fullPath),
                FileStorageErrors.FileAlreadyExists
            );

        return Result<bool>.Ok(true);
    }
}