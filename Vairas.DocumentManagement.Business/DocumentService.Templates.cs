using Vairas.DocumentManagement.Domain;
namespace Vairas.DocumentManagement.Business;

public sealed partial class DocumentService
{
    public async Task<TemplateResponse[]> TemplatesAsync(Guid school, DocumentActor actor, CancellationToken ct)
    {
        await Authority(school,null,actor,true,ct); await using var s = await store.OpenAsync(school,false,ct);
        var list = new List<TemplateResponse>();
        foreach (var t in (await s.ListAsync<DocumentTemplate>(x => true,ct)).OrderBy(x => x.Name).ThenBy(x => x.Id)) list.Add(await TemplateDto(s,t,ct));
        return list.ToArray();
    }
    public async Task<TemplateResponse> TemplateAsync(Guid school, Guid template, DocumentActor actor, CancellationToken ct)
    { await Authority(school,null,actor,true,ct); await using var s = await store.OpenAsync(school,false,ct); return await TemplateDto(s,await Required<DocumentTemplate>(s,template,ct),ct); }
    public async Task<CommandResult<TemplateResponse>> CreateTemplateAsync(Guid school, CreateTemplateRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,null,actor,true,ct); Text(request.Name,200);
        if (!DocumentRules.Kinds.Contains(request.Kind)) throw DocumentErrors.Invalid();
        return await Command(school,operation,"CreateTemplate",null,request,actor,a,async (s,r) =>
        {
            var t = new DocumentTemplate { Id = Guid.NewGuid(), SchoolId = school, Kind = request.Kind, Name = request.Name, Revision = 1 };
            s.Add(t); await Event(s,t,"Created",null,"NoActiveVersion",1,r,"Template created.",ct); r.TemplateId = t.Id;
            return await TemplateDto(s,t,ct);
        },ct);
    }
    public async Task<CommandResult<TemplateResponse>> RenameTemplateAsync(Guid school, Guid template, RenameTemplateRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,null,actor,true,ct); Text(request.Name,200);
        return await Command(school,operation,"RenameTemplate",template,request,actor,a,async (s,r) =>
        {
            var t = await Required<DocumentTemplate>(s,template,ct); Cas(t.Revision,request.ExpectedRevision);
            t.Name = request.Name; t.Revision++; await Event(s,t,"Renamed",null,t.ActiveVersionId is null ? "NoActiveVersion" : "Active",t.Revision,r,"Template renamed.",ct); r.TemplateId = t.Id;
            return await TemplateDto(s,t,ct);
        },ct);
    }
    public async Task<CommandResult<TemplateResponse>> AddVersionAsync(Guid school, Guid template, Guid file, long revision, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,null,actor,true,ct); var request = new { fileId = file, expectedRevision = revision };
        var replay = await Replay<TemplateResponse,object>(school,operation,"AddTemplateVersion",template,request,actor,ct); if (replay is not null) return replay;
        FileObject f; DocumentTemplate root;
        await using (var read = await store.OpenAsync(school,false,ct)) { f = await Required<FileObject>(read,file,ct); root = await Required<DocumentTemplate>(read,template,ct); }
        Clean(f,"Template"); renderer.ValidateTemplate(await Bytes(f,ct),root.Kind);
        return await Command(school,operation,"AddTemplateVersion",template,request,actor,a,async (s,r) =>
        {
            var t = await Required<DocumentTemplate>(s,template,ct); Cas(t.Revision,revision); Clean(await Required<FileObject>(s,file,ct),"Template");
            var versions = await s.ListAsync<TemplateVersion>(x => x.TemplateId == template,ct);
            var v = new TemplateVersion { Id = Guid.NewGuid(), SchoolId = school, TemplateId = template, VersionNo = versions.Count == 0 ? 1 : versions.Max(x => x.VersionNo)+1,
                Status = "Draft", FileId = file, CreatedBy = actor.UserId, CreatedAt = Now };
            t.Revision++; s.Add(v); await Event(s,t,"VersionAdded",null,t.ActiveVersionId is null ? "NoActiveVersion" : "Active",t.Revision,r,"Immutable template version uploaded.",ct,v.Id);
            r.TemplateId = t.Id; r.TemplateVersionId = v.Id; return await TemplateDto(s,t,ct);
        },ct);
    }
    public async Task<CommandResult<TemplateResponse>> TemplateLifecycleAsync(Guid school, Guid template, Guid version, string action, RevisionRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,null,actor,true,ct); Text(request.Reason); if (action is not ("activate" or "archive")) throw DocumentErrors.Invalid();
        return await Command(school,operation,"Template:"+action,template,new { version, request },actor,a,async (s,r) =>
        {
            var t = await Required<DocumentTemplate>(s,template,ct); var v = await Required<TemplateVersion>(s,version,ct);
            if (v.TemplateId != template) throw DocumentErrors.Missing(); Cas(t.Revision,request.ExpectedRevision);
            if (v.Status == "Archived" || action == "activate" && v.Status != "Draft") throw DocumentErrors.Rule("INVALID_TEMPLATE_TRANSITION");
            if (action == "activate")
            {
                if (t.ActiveVersionId is Guid old) { (await Required<TemplateVersion>(s,old,ct)).Status = "Archived"; await s.FlushAsync(ct); }
                v.Status = "Active"; t.ActiveVersionId = v.Id;
            }
            else { v.Status = "Archived"; if (t.ActiveVersionId == v.Id) t.ActiveVersionId = null; }
            t.Revision++; await Event(s,t,action == "activate" ? "VersionActivated" : "VersionArchived",null,t.ActiveVersionId is null ? "NoActiveVersion" : "Active",t.Revision,r,request.Reason,ct,v.Id);
            r.TemplateId = t.Id; r.TemplateVersionId = v.Id; return await TemplateDto(s,t,ct);
        },ct);
    }
}
