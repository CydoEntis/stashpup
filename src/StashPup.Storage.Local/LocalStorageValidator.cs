using StashPup.Core.Core;
using StashPup.Core.Models;

namespace StashPup.Storage.Local;

/// <summary>
/// Validator for local filesystem-specific path validation.
/// </summary>
public static class LocalStorageValidator
{
    /// <summary>
    /// Validates a local file path for storage operations.
    /// </summary>
    /// <param name="fullPath">The full file path to validate.</param>
    /// <param name="options">Local storage options containing validation rules.</param>
    /// <returns>Result indicating whether the path is valid for storage operations.</returns>
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
