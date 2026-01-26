using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StashPup.Core.Interfaces;
using StashPup.Storage.Azure;
using StashPup.Storage.Local;
using StashPup.Storage.S3;

namespace StashPup.AspNetCore.Extensions;

/// <summary>
/// DI registration extensions for StashPup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds StashPup services with configuration from appsettings.json.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">Application configuration containing StashPup settings.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an unknown provider is specified.</exception>
    /// <remarks>
    /// Expects configuration section "StashPup" with "Provider" key and provider-specific subsections.
    /// Example: { "StashPup": { "Provider": "Local", "Local": { "BasePath": "./uploads" } } }
    /// </remarks>
    public static IServiceCollection AddStashPup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("StashPup");
        var provider = section.GetValue<string>("Provider") ?? "Local";

        return provider.ToLowerInvariant() switch
        {
            "local" => services.AddStashPupLocal(section.GetSection("Local")),
            "s3" => services.AddStashPupS3(section.GetSection("S3")),
            "azureblob" or "azure" => services.AddStashPupAzure(section.GetSection("AzureBlob")),
            _ => throw new InvalidOperationException($"Unknown StashPup provider: {provider}")
        };
    }

    /// <summary>
    /// Adds StashPup services with fluent configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Action to configure StashPup using the builder.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddStashPup(stash => stash
    ///     .UseLocalStorage(options => options.BasePath = "./uploads"));
    /// </code>
    /// </example>
    public static IServiceCollection AddStashPup(
        this IServiceCollection services,
        Action<StashPupBuilder> configure)
    {
        var builder = new StashPupBuilder(services);
        configure(builder);
        return services;
    }

    /// <summary>
    /// Adds StashPup with local storage provider.
    /// </summary>
    private static IServiceCollection AddStashPupLocal(
        this IServiceCollection services,
        IConfigurationSection? section = null)
    {
        var options = new LocalStorageOptions();
        section?.Bind(options);

        services.AddSingleton(options);
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }

    /// <summary>
    /// Adds StashPup with S3 storage provider.
    /// </summary>
    private static IServiceCollection AddStashPupS3(
        this IServiceCollection services,
        IConfigurationSection? section = null)
    {
        var options = new S3StorageOptions();
        section?.Bind(options);

        services.AddSingleton(options);
        services.AddSingleton<IFileStorage, S3FileStorage>();

        return services;
    }

    /// <summary>
    /// Adds StashPup with Azure Blob storage provider.
    /// </summary>
    private static IServiceCollection AddStashPupAzure(
        this IServiceCollection services,
        IConfigurationSection? section = null)
    {
        var options = new AzureBlobStorageOptions();
        section?.Bind(options);

        services.AddSingleton(options);
        services.AddSingleton<IFileStorage, AzureBlobFileStorage>();

        return services;
    }
}

/// <summary>
/// Builder for fluent configuration of StashPup.
/// </summary>
public class StashPupBuilder
{
    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StashPupBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public StashPupBuilder(IServiceCollection services) => Services = services;

    /// <summary>
    /// Configures local storage provider.
    /// </summary>
    /// <param name="configure">Optional action to configure local storage options.</param>
    /// <returns>The builder for method chaining.</returns>
    public StashPupBuilder UseLocalStorage(Action<LocalStorageOptions>? configure = null)
    {
        var options = new LocalStorageOptions();
        configure?.Invoke(options);

        Services.AddSingleton(options);
        Services.AddSingleton<IFileStorage, LocalFileStorage>();

        return this;
    }

    /// <summary>
    /// Configures S3 storage provider.
    /// </summary>
    /// <param name="configure">Optional action to configure S3 storage options.</param>
    /// <returns>The builder for method chaining.</returns>
    public StashPupBuilder UseS3(Action<S3StorageOptions>? configure = null)
    {
        var options = new S3StorageOptions();
        configure?.Invoke(options);

        Services.AddSingleton(options);
        Services.AddSingleton<IFileStorage, S3FileStorage>();

        return this;
    }

    /// <summary>
    /// Configures Azure Blob storage provider.
    /// </summary>
    /// <param name="configure">Optional action to configure Azure Blob storage options.</param>
    /// <returns>The builder for method chaining.</returns>
    public StashPupBuilder UseAzureBlob(Action<AzureBlobStorageOptions>? configure = null)
    {
        var options = new AzureBlobStorageOptions();
        configure?.Invoke(options);

        Services.AddSingleton(options);
        Services.AddSingleton<IFileStorage, AzureBlobFileStorage>();

        return this;
    }
}
