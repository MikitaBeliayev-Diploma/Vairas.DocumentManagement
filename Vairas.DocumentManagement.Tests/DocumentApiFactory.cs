using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Tests;

/// <summary>Real HTTP pipeline, business service and PostgreSQL; external authority/rendering are explicit doubles.</summary>
public sealed class DocumentApiFactory(World world) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<DocumentOptions>(options => options.WorkersEnabled = false);
            services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();
            services.AddAuthentication("User").AddScheme<AuthenticationSchemeOptions, DocumentTestAuthentication>("User", _ => { });
            services.RemoveAll<IDocumentService>();
            services.AddSingleton<IDocumentService>(world.Service);
            services.RemoveAll<IDocumentActorFactory>();
            services.AddSingleton<IDocumentActorFactory>(new DocumentTestActorFactory(world));
        });
    }
}
