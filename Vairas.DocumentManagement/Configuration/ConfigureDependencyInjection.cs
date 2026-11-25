using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Configuration;

/// <summary>Application services; infrastructure registrations live in the dedicated Configure* classes.</summary>
public static class ConfigureDependencyInjection
{
    public static IServiceCollection AddDocumentServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DocumentService>();
        services.AddScoped<IDocumentService>(provider => provider.GetRequiredService<DocumentService>());
        services.AddScoped<IDocumentOwners, DocumentOwners>();
        services.AddScoped<IDocumentActorFactory, DocumentActorFactory>();
        return services;
    }
}
