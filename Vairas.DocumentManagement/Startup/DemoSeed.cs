using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Startup;

/// <summary>Development-only installation of DEMO templates; requires VAIRAS_DEMO=on.</summary>
public static class DemoSeed
{
    public static async Task RunAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || Environment.GetEnvironmentVariable("VAIRAS_DEMO") != "on") throw new InvalidOperationException("Demo seed requires Development and VAIRAS_DEMO=on.");
        Guid Required(string name) => Guid.Parse(Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing "+name));
        var school = Required("VAIRAS_DEMO_SCHOOL"); var actor = new DocumentActor(Required("VAIRAS_DEMO_IDENTITY"),Required("VAIRAS_DEMO_SESSION"),Required("VAIRAS_DEMO_USER"),null,null,DateTimeOffset.UtcNow.AddSeconds(30),Guid.NewGuid().ToString("D"));
        using var scope = app.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IDocumentService>(); var owners = scope.ServiceProvider.GetRequiredService<IDocumentOwners>();
        foreach (var kind in Vairas.DocumentManagement.Domain.DocumentRules.Kinds)
        {
            Guid Key(string command) => new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{school:D}:{kind}:{command}:v1")).AsSpan(0,16));
            actor = await owners.ReadmitAsync(actor,CancellationToken.None);
            var t = (await service.CreateTemplateAsync(school,new(kind,"DEMO "+kind),Key("template"),actor,CancellationToken.None)).Value;
            var f = (await service.UploadAsync(school,"Template",DocxRenderer.DemoTemplate(kind),Key("upload"),actor,CancellationToken.None)).Value;
            if ((await service.FileAsync(school,f.FileId,actor,CancellationToken.None)).ScanStatus != "Clean") throw new InvalidOperationException("Wait for scanner recovery and rerun the same seed command.");
            var version = (await service.AddVersionAsync(school,t.TemplateId,f.FileId,t.Revision,Key("version"),actor,CancellationToken.None)).Value;
            await service.TemplateLifecycleAsync(school,t.TemplateId,version.Versions[^1].TemplateVersionId,"activate",new(version.Revision,"Explicit DEMO installation."),Key("activate"),actor,CancellationToken.None);
        }
    }
}
