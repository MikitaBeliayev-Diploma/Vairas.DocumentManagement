using Microsoft.AspNetCore.Authorization;
using Vairas.DocumentManagement.Security;

namespace Vairas.DocumentManagement.Configuration;

public static class ConfigureAuthorization
{
    private static readonly string[] WorkloadScopes = ["learning.planning.read", "learning.readiness.read", "learning.programmes.read", "documents.internal", "learning.notifications.read"];

    public static IServiceCollection AddEntryAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("DocumentEntry", policy => policy.AddRequirements(new EntryRequirement()));
            foreach (var scope in WorkloadScopes)
                options.AddPolicy(scope, policy => policy.AddRequirements(new EntryRequirement(scope)));
        });
        services.AddScoped<IAuthorizationHandler, EntryHandler>();
        return services;
    }
}
