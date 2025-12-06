namespace StashPup.Core.Models;

public static class FileStorageErrors
{
    public const string FileAlreadyExists = "FILE_ALREADY_EXISTS";
    public const string MaxFileSizeExceeded = "MAX_FILE_SIZE_EXCEEDED";
    public const string InvalidFileExtension = "INVALID_FILE_EXTENSION";
    public const string EmptyFileName = "EMPTY_FILE_NAME";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string DiskFull = "DISK_FULL";
    public const string IOError = "IO_ERROR";
    public const string MemoryError = "MEMORY_ERROR";
    public const string InvalidFileName = "INVALID_FILE_NAME";

    public static string FileAlreadyExistsMessage(string fileName) => $"File '{fileName}' already exists.";

    public static string MaxFileSizeExceededMessage(long maxBytes) =>
        $"File exceeds max allowed size of {maxBytes} bytes.";

    public static string InvalidFileExtensionMessage(string extension) =>
        $"File extension '{extension}' is not supported.";

    public static string EmptyFileNameMessage() => "File name cannot be empty.";
    public static string UnexpectedErrorMessage() => "An unexpected error occurred while saving the file.";
    public static string PermissionDeniedMessage() => "No permission to write file.";
    public static string DiskFullMessage() => "Disk is full.";
    public static string IOErrorMessage() => "An I/O error occurred while saving the file.";
    public static string MemoryErrorMessage() => "Server ran out of memory.";
    public static string InvalidFileNameMessage() => "Invalid file name.";
}