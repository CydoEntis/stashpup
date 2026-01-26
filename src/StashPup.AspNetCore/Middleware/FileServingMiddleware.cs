using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StashPup.Core.Interfaces;
using StashPup.Storage.Local;

namespace StashPup.AspNetCore.Middleware;

/// <summary>
/// Middleware for serving local files stored via the local storage provider.
/// </summary>
public class FileServingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LocalStorageOptions _options;
    private readonly ILogger<FileServingMiddleware>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileServingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">Local storage options containing BaseUrl configuration.</param>
    /// <param name="logger">Optional logger for middleware operations.</param>
    public FileServingMiddleware(
        RequestDelegate next,
        LocalStorageOptions options,
        ILogger<FileServingMiddleware>? logger = null)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to serve files or pass to the next middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="storage">The file storage provider (injected from DI).</param>
    public async Task InvokeAsync(HttpContext context, IFileStorage storage)
    {
        if (!context.Request.Path.StartsWithSegments(_options.BaseUrl, out var remaining))
        {
            await _next(context);
            return;
        }

        // Extract file ID from path
        var fileIdString = remaining.Value?.TrimStart('/');
        if (string.IsNullOrWhiteSpace(fileIdString) || !Guid.TryParse(fileIdString, out var fileId))
        {
            context.Response.StatusCode = 400;
            return;
        }

        // Validate signed URL if enabled
        if (_options.EnableSignedUrls)
        {
            if (!ValidateSignature(context.Request))
            {
                context.Response.StatusCode = 403;
                return;
            }
        }

        // Get and serve file
        var result = await storage.GetAsync(fileId);
        if (!result.Success)
        {
            context.Response.StatusCode = 404;
            return;
        }

        // Get metadata for content type
        var metadata = await storage.GetMetadataAsync(fileId);
        context.Response.ContentType = metadata.Data?.ContentType ?? "application/octet-stream";
        if (metadata.Data != null)
            context.Response.ContentLength = metadata.Data.SizeBytes;

        await using var stream = result.Data!;
        await stream.CopyToAsync(context.Response.Body);
    }

    private bool ValidateSignature(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            return false;

        if (!request.Query.TryGetValue("expires", out var expiresStr) ||
            !request.Query.TryGetValue("signature", out var signature))
            return false;

        if (!long.TryParse(expiresStr, out var expires))
            return false;

        // Check expiry
        var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expires);
        if (expiryTime < DateTimeOffset.UtcNow)
            return false;

        // Extract file ID from path
        var fileIdString = request.Path.Value?.Split('/').LastOrDefault();
        if (!Guid.TryParse(fileIdString, out var fileId))
            return false;

        // Verify signature
        var expectedSignature = ComputeSignature($"{fileId}:{expires}", _options.SigningKey);
        return signature == expectedSignature;
    }

    private string ComputeSignature(string data, string key)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(key));
        var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }
}
