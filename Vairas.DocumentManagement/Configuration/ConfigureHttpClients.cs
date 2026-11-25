using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Configuration;

public static class ConfigureHttpClients
{
    public static IServiceCollection AddDependencyHttpClients(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<DependencyCorrelationHandler>();
        services.AddHttpClient("dependencies", client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .AddHttpMessageHandler<DependencyCorrelationHandler>();
        services.AddSingleton<IWorkloadTokens, WorkloadTokens>();
        services.AddScoped<IDependencyTransport, DependencyTransport>();
        return services;
    }
}
