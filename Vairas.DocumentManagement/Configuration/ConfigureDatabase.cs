using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Configuration.Options;
using Vairas.DocumentManagement.Persistance;

namespace Vairas.DocumentManagement.Configuration;

public static class ConfigureDatabase
{
    public static IServiceCollection AddDocumentDatabase(this IServiceCollection services)
    {
        services.AddDbContextFactory<DocumentDbContext>((provider, builder) =>
            builder.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.DocumentManagement));
        services.AddScoped<IDocumentStore, DocumentStore>();
        // Workers and the delivery catalogue connect with their own, separately granted database role.
        services.AddScoped<IDeliveryCatalogue>(provider => new DeliveryCatalogue(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Delivery));
        return services;
    }
}
