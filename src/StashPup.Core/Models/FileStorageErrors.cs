namespace StashPup.Core.Models;

/// <summary>
/// Centralized error codes and message generators.
/// </summary>
public static class FileStorageErrors
{
    /// <summary>
    /// Error code indicating the requested file was not found.
    /// </summary>
    public const string FileNotFound = "FILE_NOT_FOUND";

    /// <summary>
    /// Error code indicating a file with the same name already exists.
    /// </summary>
    public const string FileAlreadyExists = "FILE_ALREADY_EXISTS";

    /// <summary>
    /// Error code indicating the file size exceeds the maximum allowed size.
    /// </summary>
    public const string MaxFileSizeExceeded = "MAX_FILE_SIZE_EXCEEDED";

    /// <summary>
    /// Error code indicating the file extension is not allowed.
    /// </summary>
    public const string InvalidFileExtension = "INVALID_FILE_EXTENSION";

    /// <summary>
    /// Error code indicating the file content type is not allowed.
    /// </summary>
    public const string InvalidContentType = "INVALID_CONTENT_TYPE";

    /// <summary>
    /// Error code indicating the file type is not supported for the requested operation.
    /// </summary>
    public const string InvalidFileType = "INVALID_FILE_TYPE";

    /// <summary>
    /// Error code indicating the file name is empty or whitespace.
    /// </summary>
    public const string EmptyFileName = "EMPTY_FILE_NAME";

    /// <summary>
    /// Error code indicating the file name contains invalid characters.
    /// </summary>
    public const string InvalidFileName = "INVALID_FILE_NAME";

    /// <summary>
    /// Error code indicating the file content is empty.
    /// </summary>
    public const string EmptyFileContent = "EMPTY_FILE_CONTENT";

    /// <summary>
    /// Error code indicating permission was denied when accessing storage.
    /// </summary>
    public const string PermissionDenied = "PERMISSION_DENIED";

    /// <summary>
    /// Error code indicating the disk or storage is full.
    /// </summary>
    public const string DiskFull = "DISK_FULL";

    /// <summary>
    /// Error code indicating an I/O error occurred during file operations.
    /// </summary>
    public const string IOError = "IO_ERROR";

    /// <summary>
    /// Error code indicating the system ran out of memory.
    /// </summary>
    public const string MemoryError = "MEMORY_ERROR";

    /// <summary>
    /// Error code indicating an unexpected error occurred.
    /// </summary>
    public const string UnexpectedError = "UNEXPECTED_ERROR";

    /// <summary>
    /// Error code indicating a provider-specific error occurred.
    /// </summary>
    public const string ProviderError = "PROVIDER_ERROR";

    /// <summary>
    /// Error code indicating file validation failed.
    /// </summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>
    /// Error code indicating the operation was cancelled.
    /// </summary>
    public const string OperationCancelled = "OPERATION_CANCELLED";

    /// <summary>
    /// Error code indicating signed URLs are not supported by the provider.
    /// </summary>
    public const string SignedUrlNotSupported = "SIGNED_URL_NOT_SUPPORTED";

    /// <summary>
    /// Error code indicating public URLs are not supported by the provider.
    /// </summary>
    public const string PublicUrlNotSupported = "PUBLIC_URL_NOT_SUPPORTED";

    /// <summary>
    /// Generates an error message for a file not found error.
    /// </summary>
    /// <param name="id">The file identifier that was not found.</param>
    /// <returns>Formatted error message.</returns>
    public static string FileNotFoundMessage(Guid id)
        => $"File with ID '{id}' was not found.";

    /// <summary>
    /// Generates an error message for a file already exists error.
    /// </summary>
    /// <param name="fileName">The file name that already exists.</param>
    /// <returns>Formatted error message.</returns>
    public static string FileAlreadyExistsMessage(string fileName)
        => $"A file named '{fileName}' already exists.";

    /// <summary>
    /// Generates an error message for a file size exceeded error.
    /// </summary>
    /// <param name="maxBytes">The maximum allowed file size in bytes.</param>
    /// <returns>Formatted error message with human-readable size.</returns>
    public static string MaxFileSizeExceededMessage(long maxBytes)
        => $"File exceeds maximum allowed size of {FormatBytes(maxBytes)}.";

    /// <summary>
    /// Generates an error message for an invalid file extension error.
    /// </summary>
    /// <param name="extension">The invalid file extension.</param>
    /// <param name="allowed">Array of allowed file extensions.</param>
    /// <returns>Formatted error message listing allowed extensions.</returns>
    public static string InvalidFileExtensionMessage(string extension, string[] allowed)
        => $"File extension '{extension}' is not allowed. Allowed: {string.Join(", ", allowed)}";

    /// <summary>
    /// Generates an error message for an invalid file extension error.
    /// </summary>
    /// <param name="extension">The invalid file extension.</param>
    /// <returns>Formatted error message.</returns>
    public static string InvalidFileExtensionMessage(string extension)
        => $"File extension '{extension}' is not supported.";

    /// <summary>
    /// Generates an error message for an invalid content type error.
    /// </summary>
    /// <param name="contentType">The invalid content type.</param>
    /// <param name="allowed">Array of allowed content types.</param>
    /// <returns>Formatted error message listing allowed content types.</returns>
    public static string InvalidContentTypeMessage(string contentType, string[] allowed)
        => $"Content type '{contentType}' is not allowed. Allowed: {string.Join(", ", allowed)}";

    /// <summary>
    /// Generates an error message for an empty file name error.
    /// </summary>
    /// <returns>Formatted error message.</returns>
    public static string EmptyFileNameMessage()
        => "File name cannot be empty.";

    /// <summary>
    /// Generates an error message for an empty file content error.
    /// </summary>
    /// <returns>Formatted error message.</returns>
    public static string EmptyFileContentMessage()
        => "File content cannot be empty.";

    /// <summary>
    /// Generates an error message for an invalid file name error.
    /// </summary>
    /// <returns>Formatted error message.</returns>
    public static string InvalidFileNameMessage()
        => "Invalid file name.";

    /// <summary>
    /// Generates an error message for a permission denied error.
    /// </summary>
    /// <returns>Formatted error message.</returns>
    public static string PermissionDeniedMessage()
        => "No permission to write file.";

    /// <summary>
    /// Generates an error message for a disk full error.
    /// </summary>
    /// <returns>Formatted error message.</returns>
    public static string DiskFullMessage()
        => "Disk is full.";

    /// <summary>
    /// Generates an error message for an I/O error.
    /// </summary>
    /// <returns>Formatted error message.</returns>
    public static string IOErrorMessage()
        => "An I/O error occurred while saving the file.";

    /// <summary>
    /// Generates an error message for a memory error.
    /// </summary>
    /// <returns>Formatted error message.</returns>
    public static string MemoryErrorMessage()
        => "Server ran out of memory.";

    /// <summary>
    /// Generates an error message for an unexpected error.
    /// </summary>
    /// <returns>Formatted error message.</returns>
    public static string UnexpectedErrorMessage()
        => "An unexpected error occurred while saving the file.";

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
