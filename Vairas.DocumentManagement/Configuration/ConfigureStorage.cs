using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Configuration.Options;
using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Configuration;

/// <summary>Private blob storage, malware scanning and document rendering adapters.</summary>
public static class ConfigureStorage
{
    public static IServiceCollection AddDocumentInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(provider => BlobFiles.Create(provider.GetRequiredService<IOptions<BlobOptions>>().Value));
        services.AddSingleton<IPrivateFiles, BlobFiles>();
        services.AddSingleton<IFileScanner>(provider => new ClamScanner(provider.GetRequiredService<IOptions<ScannerOptions>>().Value));
        services.AddSingleton<IDocumentRenderer, DocxRenderer>();
        return services;
    }
}
