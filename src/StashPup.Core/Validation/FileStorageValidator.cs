using StashPup.Core.Core;
using StashPup.Core.Models;

namespace StashPup.Core.Validation;

/// <summary>
/// Core validation logic shared by all providers.
/// </summary>
public static class FileStorageValidator
{
    /// <summary>
    /// Validates a file before saving.
    /// </summary>
    /// <param name="content">File content stream to validate.</param>
    /// <param name="fileName">File name to validate.</param>
    /// <param name="options">Storage options containing validation rules.</param>
    /// <returns>Result indicating whether validation passed or failed with error details.</returns>
    public static Result<bool> Validate(
        Stream content,
        string fileName,
        FileStorageOptions options)
    {
        // 1. File name validation
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result<bool>.Fail(
                FileStorageErrors.EmptyFileNameMessage(),
                FileStorageErrors.EmptyFileName);
        }

        // 2. File name character validation
        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.IndexOfAny(invalidChars) >= 0)
        {
            return Result<bool>.Fail(
                $"File name contains invalid characters.",
                FileStorageErrors.InvalidFileName);
        }

        // 3. Extension validation
        if (options.AllowedExtensions.Count > 0)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (!options.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return Result<bool>.Fail(
                    FileStorageErrors.InvalidFileExtensionMessage(ext, options.AllowedExtensions.ToArray()),
                    FileStorageErrors.InvalidFileExtension);
            }
        }

        // 4. Content validation
        if (content.Length == 0)
        {
            return Result<bool>.Fail(
                FileStorageErrors.EmptyFileContentMessage(),
                FileStorageErrors.EmptyFileContent);
        }

        // 5. Size validation
        if (options.MaxFileSizeBytes.HasValue && content.Length > options.MaxFileSizeBytes.Value)
        {
            return Result<bool>.Fail(
                FileStorageErrors.MaxFileSizeExceededMessage(options.MaxFileSizeBytes.Value),
                FileStorageErrors.MaxFileSizeExceeded);
        }

        // 6. Content type validation (magic bytes) - optional enhancement
        if (options.AllowedContentTypes.Count > 0)
        {
            var detectedType = DetectContentType(content, fileName);
            if (!IsContentTypeAllowed(detectedType, options.AllowedContentTypes))
            {
                return Result<bool>.Fail(
                    FileStorageErrors.InvalidContentTypeMessage(detectedType, options.AllowedContentTypes.ToArray()),
                    FileStorageErrors.InvalidContentType);
            }
        }

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// Detects content type from magic bytes and/or extension.
    /// </summary>
    /// <param name="content">File content stream to analyze.</param>
    /// <param name="fileName">File name used for extension-based fallback detection.</param>
    /// <returns>Detected MIME content type.</returns>
    public static string DetectContentType(Stream content, string fileName)
    {
        // Save position
        var originalPosition = content.Position;
        content.Position = 0;

        try
        {
            // Read first bytes for magic number detection
            var buffer = new byte[Math.Min(512, content.Length)];
            var bytesRead = content.Read(buffer, 0, buffer.Length);

            // Check magic bytes for common types
            if (bytesRead >= 4)
            {
                // PNG
                if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
                    return "image/png";

                // JPEG
                if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
                    return "image/jpeg";

                // GIF
                if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x38)
                    return "image/gif";

                // PDF
                if (buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
                    return "application/pdf";
            }

            // Fallback to extension-based detection
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }
        finally
        {
            // Restore position
            content.Position = originalPosition;
        }
    }

    /// <summary>
    /// Checks if content type matches allowed patterns (supports wildcards).
    /// </summary>
    /// <param name="contentType">Content type to check (e.g., "image/jpeg").</param>
    /// <param name="allowed">List of allowed content type patterns (e.g., ["image/*", "application/pdf"]).</param>
    /// <returns>True if the content type is allowed, false otherwise.</returns>
    public static bool IsContentTypeAllowed(string contentType, List<string> allowed)
    {
        foreach (var pattern in allowed)
        {
            if (pattern.EndsWith("/*"))
            {
                var prefix = pattern[..^2];
                if (contentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (contentType.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
