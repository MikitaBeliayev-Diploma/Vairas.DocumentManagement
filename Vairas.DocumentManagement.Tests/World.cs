using System.Linq.Expressions;
using System.Text.Json;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class World
{
    public Guid School { get; } = Guid.NewGuid(); public Guid Enrollment { get; } = Guid.NewGuid();
    public TestClock Clock { get; } = new(); public TestFiles Files { get; } = new(); public TestScanner Scanner { get; } = new(); public TestRenderer Renderer { get; } = new();
    public IDocumentStore Store { get; } public TestOwners Owners { get; } public DocumentService Service { get; }
    public DocumentActor Actor { get; set; }
    public World(IDocumentStore? store = null, IDocumentRenderer? renderer = null)
    {
        Store = store ?? new MemoryStore(); Owners = new(Clock); Actor = new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),null,null,Clock.Time.AddHours(1),"unit-correlation");
        var snapshot = Guid.NewGuid(); Owners.Evidence = new(School,Enrollment,snapshot,"PracticalJournal",1,1,"Ready",true,true,DocumentRules.SourceVersion(snapshot,1),"fixture-v1",Clock.Time,[],[]);
        Service = new(Store,Owners,Files,Scanner,renderer ?? Renderer,new(),Clock);
    }
    public async Task<TemplateResponse> Template(string kind = "PracticalJournal", byte[]? bytes = null)
    {
        var root = (await Service.CreateTemplateAsync(School,new(kind,"Template "+kind),Guid.NewGuid(),Actor,default)).Value;
        var upload = (await Service.UploadAsync(School,"Template",bytes ?? "DOCX template fixture"u8.ToArray(),Guid.NewGuid(),Actor,default)).Value;
        root = (await Service.AddVersionAsync(School,root.TemplateId,upload.FileId,root.Revision,Guid.NewGuid(),Actor,default)).Value;
        return (await Service.TemplateLifecycleAsync(School,root.TemplateId,root.Versions[0].TemplateVersionId,"activate",new(root.Revision,"Activate fixture."),Guid.NewGuid(),Actor,default)).Value;
    }
    public async Task<DocumentResponse> Generate(string kind = "PracticalJournal", Guid? template = null)
    {
        Owners.Evidence = Owners.Evidence with { Kind = kind };
        if (template is null) await Template(kind);
        var r = (await Service.GenerateAsync(School,Enrollment,kind,new(Owners.Evidence.SourceSnapshotVersion,template),Guid.NewGuid(),Actor,default)).Value;
        await Service.ProcessJobAsync(School,r.JobId,default);
        var j = await Service.JobAsync(School,r.JobId,Actor,default); Assert.Equal("Completed",j.Status);
        return await Service.DocumentAsync(School,j.DocumentId!.Value,Actor,default);
    }
    public async Task<DocumentResponse> Review(DocumentResponse d) => (await Service.VersionCommandAsync(School,d.DocumentId,d.Versions[^1].DocumentVersionId,"review",new(d.Revision,d.Versions[^1].Revision,"Checked fixture."),Guid.NewGuid(),Actor,default)).Value;
    public async Task<DocumentResponse> Publish(DocumentResponse d, string mode = "Ready")
    {
        var operation = Guid.NewGuid(); await Service.PublishAsync(School,d.DocumentId,d.Versions[^1].DocumentVersionId,new(d.Revision,d.Versions[^1].Revision,mode,"Human reviewed fixture."),operation,Actor,default);
        if (mode == "Ready") await Service.ProcessPublicationAsync(School,operation,default);
        return await Service.DocumentAsync(School,d.DocumentId,Actor,default);
    }
}
