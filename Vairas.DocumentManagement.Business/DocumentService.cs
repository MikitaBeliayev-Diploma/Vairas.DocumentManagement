using System.Text;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;
public sealed partial class DocumentService(IDocumentStore store, IDocumentOwners owners, IPrivateFiles files, IFileScanner scanner,
    IDocumentRenderer renderer, DocumentOptions options, TimeProvider clock) : IDocumentService
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private static void Id(Guid id) { if (id == Guid.Empty) throw DocumentErrors.Invalid(); }
    private static void Text(string? text, int max = 1000) { if (string.IsNullOrWhiteSpace(text) || text.Length > max) throw DocumentErrors.Invalid(); }
    private static void Cas(long actual, long expected) { if (actual != expected) throw DocumentErrors.Conflict(); }
    private void Admission(DocumentActor actor) { Id(actor.UserId); if (actor.ValidUntil <= Now) throw DocumentErrors.Dependency("ADMISSION_EXPIRED"); }
    private async Task<DocumentAuthority> Authority(Guid school, Guid? enrollment, DocumentActor actor, bool admin, CancellationToken ct)
    {
        Admission(actor); Id(school);
        var a = await owners.AuthorityAsync(school, enrollment, actor, ct);
        if (a.SchoolId != school || a.ActorUserId != actor.UserId || a.EnrollmentId != enrollment || a.ValidUntil <= Now) throw DocumentErrors.Dependency("INVALID_SCHOOL_EVIDENCE");
        if (admin && !a.Administrator) throw DocumentErrors.Denied();
        Admission(actor); return a;
    }
    private static async Task<T> Required<T>(IDocumentSession s, Guid id, CancellationToken ct) where T : class, ISchoolRow => await s.FindAsync<T>(x => x.Id == id, ct) ?? throw DocumentErrors.Missing();
    private string Fingerprint<T>(string command, Guid? target, T request, DocumentActor actor) => DocumentRules.Digest(Wire.Save(new { command, target, actor.UserId, channel = "Web", request }));
    private async Task<CommandResult<T>?> Replay<T, TRequest>(Guid school, Guid operation, string command, Guid? target, TRequest request, DocumentActor actor, CancellationToken ct)
    {
        Id(operation); await using var s = await store.OpenAsync(school, false, ct);
        var receipt = await s.FindAsync<OperationReceipt>(x => x.Id == operation, ct);
        if (receipt is null) return null;
        if (receipt.ActorId != actor.UserId || receipt.RequestDigest != Fingerprint(command, target, request, actor)) throw DocumentErrors.Conflict("IDEMPOTENCY_KEY_REUSED");
        var outcome = await s.FindAsync<OperationOutcome>(x => x.Id == operation, ct);
        return outcome is null ? null : new(Wire.Read<T>(outcome.ResponseJson), true);
    }
    private async Task<CommandResult<T>> Command<TRequest,T>(Guid school, Guid operation, string command, Guid? target, TRequest request,
        DocumentActor actor, DocumentAuthority authority, Func<IDocumentSession,OperationReceipt,Task<T>> apply, CancellationToken ct, bool pending = false)
    {
        Id(operation); Admission(actor);
        await using var s = await store.OpenAsync(school, true, ct);
        Admission(actor); if (authority.ValidUntil <= Now) throw DocumentErrors.Dependency("AUTHORITY_EXPIRED");
        var digest = Fingerprint(command, target, request, actor);
        var receipt = await s.FindAsync<OperationReceipt>(x => x.Id == operation, ct);
        if (receipt is not null)
        {
            if (receipt.ActorId != actor.UserId || receipt.RequestDigest != digest) throw DocumentErrors.Conflict("IDEMPOTENCY_KEY_REUSED");
            var saved = await s.FindAsync<OperationOutcome>(x => x.Id == operation, ct);
            if (saved is not null) return new(Wire.Read<T>(saved.ResponseJson), true);
            throw DocumentErrors.Conflict("OPERATION_PENDING");
        }
        receipt = new OperationReceipt { Id = operation, SchoolId = school, ActorId = actor.UserId, Operation = command, RequestDigest = digest, Status = "Pending", CreatedAt = Now };
        s.Add(receipt); await s.FlushAsync(ct);
        var result = await apply(s, receipt);
        s.Add(new OperationOutcome { Id = operation, SchoolId = school, ResponseJson = Wire.Save(result) });
        if (!pending) { receipt.Status = "Succeeded"; receipt.CompletedAt = Now; receipt.OutcomeCode = "APPLIED"; }
        await s.CommitAsync(ct); return new(result, false);
    }
    private async Task Event(IDocumentSession s, ISchoolRow target, string action, string? from, string to, long revision, OperationReceipt receipt, string reason, CancellationToken ct, Guid? child = null)
    {
        await s.FlushAsync(ct);
        var e = new StateEvent { Id = Guid.NewGuid(), SchoolId = target.SchoolId, Action = action, FromState = from, ToState = to, SequenceNo = revision,
            ActorId = receipt.ActorId, CommandId = receipt.Id, OccurredAt = Now, Reason = reason };
        switch (target)
        {
            case DocumentTemplate: e.TemplateId = target.Id; e.TemplateVersionId = child; break;
            case TrainingContract: e.ContractId = target.Id; e.SignedCopyId = child; break;
            case TrainingDocument: e.DocumentId = target.Id; break;
            case DocumentVersion: e.DocumentVersionId = target.Id; break;
            default: throw new InvalidOperationException("Unsupported audit root.");
        }
        s.Add(e); await s.FlushAsync(ct);
    }
    private Task RootEvent(IDocumentSession s, TrainingDocument d, Guid? before, bool created, OperationReceipt r, string reason, CancellationToken ct) =>
        Event(s, d, created ? "DocumentCreated" : before == d.CurrentVersionId ? "DocumentChanged" : d.CurrentVersionId is null ? "CurrentVersionCleared" : "CurrentVersionSelected",
            created ? null : DocumentRules.Pointer(before), DocumentRules.Pointer(d.CurrentVersionId), d.Revision, r, reason, ct);
    private async Task<TemplateResponse> TemplateDto(IDocumentSession s, DocumentTemplate t, CancellationToken ct) => new(t.Id,t.SchoolId,t.Kind,t.Name,t.ActiveVersionId,t.Revision,
        (await s.ListAsync<TemplateVersion>(x => x.TemplateId == t.Id, ct)).OrderBy(x => x.VersionNo).Select(x => new TemplateVersionResponse(x.Id,x.VersionNo,x.Status,x.FileId)).ToArray());
    private async Task<ContractResponse> ContractDto(IDocumentSession s, TrainingContract c, CancellationToken ct) => new(c.Id,c.EnrollmentId,c.StudentId,c.TemplateVersionId,c.Status,c.Revision,c.ReplacesContractId,
        (await s.ListAsync<ContractSignedCopy>(x => x.ContractId == c.Id, ct)).OrderBy(x => x.CopyNo).Select(x => new SignedCopyResponse(x.Id,x.CopyNo,x.FileId,x.IsCurrent,x.DeclaredSignedAt,x.RecordedAt,x.Basis)).ToArray());
    private async Task<bool> Reviewed(IDocumentSession s, DocumentVersion v, CancellationToken ct) => (await s.ListAsync<DocumentReview>(x => x.DocumentVersionId == v.Id, ct)).Count > 0;
    private async Task<DocumentResponse> DocumentDto(IDocumentSession s, TrainingDocument d, bool admin, CancellationToken ct)
    {
        var versions = new List<DocumentVersionResponse>();
        foreach (var v in (await s.ListAsync<DocumentVersion>(x => x.DocumentId == d.Id, ct)).OrderBy(x => x.VersionNo))
        {
            if (!admin && v.State is "Prepared" or "Indeterminate") continue;
            versions.Add(new(v.Id,v.VersionNo,v.State,v.Revision,v.EvidenceSnapshotId,v.DependencyRevision,v.EvidenceStatus,v.TemplateVersionId,await Reviewed(s,v,ct)));
        }
        return new(d.Id,d.EnrollmentId,d.Kind,d.Revision,d.CurrentVersionId,versions.ToArray());
    }
    private static FileResponse FileDto(FileObject f) => new(f.Id,f.ScanStatus,f.Sha256,f.ByteLength);
    private async Task<byte[]> Bytes(FileObject f, CancellationToken ct)
    {
        var bytes = await files.GetAsync(f.StorageKey,f.ByteLength,ct);
        if (bytes.LongLength != f.ByteLength || DocumentRules.Digest(bytes) != f.Sha256) throw DocumentErrors.Dependency("FILE_INTEGRITY_FAILURE");
        return bytes;
    }
    private async Task<FileObject> StageFile(Guid school, Guid actor, string purpose, byte[] bytes, string mediaType, CancellationToken ct)
    {
        var id = Guid.NewGuid(); var key = $"{school:D}/{id:D}";
        await files.PutAsync(key,bytes,mediaType,ct);
        var f = new FileObject { Id = id, SchoolId = school, Purpose = purpose, StorageKey = key, Sha256 = DocumentRules.Digest(bytes), ByteLength = bytes.LongLength,
            MediaType = mediaType, ScanStatus = "Pending", UploadedBy = actor, UploadedAt = Now };
        await using (var s = await store.OpenAsync(school,true,ct)) { s.Add(f); await s.CommitAsync(ct); }
        await ScanFileAsync(school,id,ct);
        await using var read = await store.OpenAsync(school,false,ct); return await Required<FileObject>(read,id,ct);
    }
    public async Task ScanFileAsync(Guid school, Guid id, CancellationToken ct)
    {
        FileObject f; await using (var read = await store.OpenAsync(school,false,ct)) f = await Required<FileObject>(read,id,ct);
        if (f.ScanStatus != "Pending") return;
        var status = await scanner.ScanAsync(await Bytes(f,ct),ct);
        if (status == "Pending") return;
        await using var s = await store.OpenAsync(school,true,ct); var current = await Required<FileObject>(s,id,ct);
        if (current.ScanStatus == "Pending") { current.ScanStatus = status; await s.CommitAsync(ct); }
    }
    private static void Clean(FileObject f, string purpose) { if (f.ScanStatus != "Clean" || f.Purpose != purpose) throw DocumentErrors.Rule("CLEAN_FILE_REQUIRED"); }
    public async Task<FileResponse> FileAsync(Guid school, Guid file, DocumentActor actor, CancellationToken ct)
    { await Authority(school,null,actor,true,ct); await using var s = await store.OpenAsync(school,false,ct); return FileDto(await Required<FileObject>(s,file,ct)); }
    public async Task<CommandResult<FileResponse>> UploadAsync(Guid school, string purpose, byte[] bytes, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,null,actor,true,ct);
        if (purpose is not ("Template" or "SignedCopy") || bytes.LongLength is 0 || bytes.LongLength > options.MaxUploadBytes) throw DocumentErrors.Invalid("INVALID_UPLOAD");
        var request = new { purpose, digest = DocumentRules.Digest(bytes) };
        var prior = await Replay<FileResponse,object>(school,operation,"UploadFile",null,request,actor,ct); if (prior is not null) return prior;
        if (purpose == "Template") renderer.ValidateTemplate(bytes,"Any");
        else if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw DocumentErrors.Invalid("PDF_REQUIRED");
        var f = await StageFile(school,actor.UserId,purpose,bytes,purpose == "Template" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/pdf",ct);
        actor = await owners.ReadmitAsync(actor,ct); a = await Authority(school,null,actor,true,ct);
        return await Command(school,operation,"UploadFile",null,request,actor,a,(_,_) => Task.FromResult(FileDto(f)),ct);
    }
}
