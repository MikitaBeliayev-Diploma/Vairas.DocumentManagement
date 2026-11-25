using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Configuration;

public static class ConfigureMessageBroker
{
    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services)
    {
        services.AddHostedService<DocumentWorker>();
        services.AddHostedService<DocumentInboxWorker>();
        return services;
    }
}
