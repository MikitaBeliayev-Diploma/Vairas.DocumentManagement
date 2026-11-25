using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Configuration.Options;

namespace Vairas.DocumentManagement.Configuration;

/// <summary>Binds and validates every strongly typed options section; invalid required settings fail at startup.</summary>
public static class ConfigureOptions
{
    public static IServiceCollection AddDocumentOptions(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidated<DatabaseOptions, DatabaseOptionsValidator>(services, configuration.GetSection("ConnectionStrings"));
        AddValidated<JwtOptions, JwtOptionsValidator>(services, configuration.GetSection("Authentication"));
        AddValidated<DependencyOptions, DependencyOptionsValidator>(services, configuration.GetSection("Dependencies"));
        AddValidated<DocumentOptions, DocumentOptionsValidator>(services, configuration.GetSection("Documents"));
        AddValidated<BlobOptions, BlobOptionsValidator>(services, configuration.GetSection("Blob"));
        AddValidated<DocumentBusOptions, DocumentBusOptionsValidator>(services, configuration.GetSection("ServiceBus"));
        services.AddOptions<ScannerOptions>().Bind(configuration.GetSection("Scanner"));
        services.AddOptions<RendererOptions>().Bind(configuration.GetSection("Renderer"));
        // Infrastructure adapters and the business layer take the plain settings objects.
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<DocumentOptions>>().Value);
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<RendererOptions>>().Value);
        return services;
    }

    private static void AddValidated<TOptions, TValidator>(IServiceCollection services, IConfigurationSection section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddOptions<TOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
    }
}
