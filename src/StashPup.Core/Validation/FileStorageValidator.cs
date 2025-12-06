using StashPup.Core.Core;
using StashPup.Core.Models;

namespace StashPup.Core.Validation;

public class FileStorageValidator
{
    public static Result<bool> ValidateFile(Stream content, string fileName, FileStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result<bool>.Fail(
                FileStorageErrors.EmptyFileNameMessage(),
                FileStorageErrors.EmptyFileName
            );

        var extension = Path.GetExtension(fileName);
        if (options.AllowableExtensions.Any() && !options.AllowableExtensions.Contains(extension))
            return Result<bool>.Fail(
                FileStorageErrors.InvalidFileExtensionMessage(extension),
                FileStorageErrors.InvalidFileExtension
            );

        return Result<bool>.Ok(true);
    }
}