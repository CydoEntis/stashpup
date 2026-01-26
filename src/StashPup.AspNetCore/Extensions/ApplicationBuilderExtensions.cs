using Microsoft.AspNetCore.Builder;
using StashPup.AspNetCore.Middleware;

namespace StashPup.AspNetCore.Extensions;

/// <summary>
/// Application builder extensions for StashPup.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds file serving middleware for local storage.
    /// Enables serving files stored via local storage provider through HTTP requests.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// This middleware only works with the local storage provider.
    /// Files are served at the BaseUrl path configured in LocalStorageOptions.
    /// </remarks>
    public static IApplicationBuilder UseStashPup(this IApplicationBuilder app)
    {
        return app.UseMiddleware<FileServingMiddleware>();
    }
}
