using System.Text;
using Vairas.DocumentManagement.Domain;
namespace Vairas.DocumentManagement.Business;

public sealed partial class DocumentService
{
    private async Task<TemplateVersion> ActiveTemplate(Guid school, Guid? template, string kind, CancellationToken ct)
    {
        await using var s = await store.OpenAsync(school,false,ct);
        var roots = await s.ListAsync<DocumentTemplate>(x => x.Kind == kind && x.ActiveVersionId != null && (template == null || x.Id == template),ct);
        if (roots.Count != 1) throw DocumentErrors.Rule(roots.Count == 0 ? "ACTIVE_TEMPLATE_REQUIRED" : "TEMPLATE_SELECTION_REQUIRED");
        return await Required<TemplateVersion>(s,roots[0].ActiveVersionId!.Value,ct);
    }
    public async Task<CommandResult<DocumentJobResponse>> GenerateContractAsync(Guid school, GenerateContractRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,request.EnrollmentId,actor,true,ct); Id(request.TemplateId);
        var replay = await Replay<DocumentJobResponse,GenerateContractRequest>(school,operation,"GenerateContract",request.EnrollmentId,request,actor,ct); if (replay is not null) return replay;
        if (a.StudentId is null || a.EnrollmentStatus is "Cancelled" or "Withdrawn") throw DocumentErrors.Rule("ENROLLMENT_NOT_ELIGIBLE");
        var template = await ActiveTemplate(school,request.TemplateId,"TrainingContract",ct);
        return await Command(school,operation,"GenerateContract",request.EnrollmentId,request,actor,a,async (s,r) =>
        {
            if (request.ReplacesContractId is Guid predecessor)
            { var old = await Required<TrainingContract>(s,predecessor,ct); if (old.EnrollmentId != request.EnrollmentId) throw DocumentErrors.Missing(); }
            var job = NewJob(school,request.EnrollmentId,"TrainingContract",template.Id,operation,actor);
            job.ReplacesContractId = request.ReplacesContractId; s.Add(job);
            return await JobDto(s,job,ct);
        },ct,pending:true);
    }
    public async Task<CommandResult<DocumentJobResponse>> GenerateAsync(Guid school, Guid enrollment, string kind, GenerateTrainingDocumentRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,enrollment,actor,true,ct); Text(request.SourceSnapshotVersion,200);
        if (kind is not ("PracticalJournal" or "CompletionReport")) throw DocumentErrors.Invalid();
        var replay = await Replay<DocumentJobResponse,GenerateTrainingDocumentRequest>(school,operation,"Generate:"+kind,enrollment,request,actor,ct); if (replay is not null) return replay;
        var evidence = await owners.EvidenceAsync(school,enrollment,request.SourceSnapshotVersion,actor,ct);
        ValidateEvidence(evidence,school,enrollment,kind,request.SourceSnapshotVersion);
        var template = await ActiveTemplate(school,request.TemplateId,kind,ct);
        return await Command(school,operation,"Generate:"+kind,enrollment,request,actor,a,async (s,r) =>
        {
            var job = NewJob(school,enrollment,kind,template.Id,operation,actor); job.SnapshotId = evidence.SnapshotId; job.SourceSnapshotVersion = evidence.SourceSnapshotVersion;
            job.MissingItemsJson = Wire.Save(evidence.Checks.Where(x => x.Outcome != "Satisfied").Select(x => $"{x.Code}/{x.Field}: {x.Explanation}").ToArray()); s.Add(job);
            return await JobDto(s,job,ct);
        },ct,pending:true);
    }
    private static void ValidateEvidence(EvidencePayload e, Guid school, Guid enrollment, string kind, string version)
    {
        if (e.SchoolId != school || e.EnrollmentId != enrollment || e.Kind != kind || e.SnapshotId == Guid.Empty || e.DependencyRevision < 0 ||
            e.SourceSnapshotVersion != version || version != DocumentRules.SourceVersion(e.SnapshotId,e.DependencyRevision)) throw DocumentErrors.Conflict("EVIDENCE_MISMATCH");
        if (!e.SourcesVerified || e.CurrentDependencyRevision != e.DependencyRevision) throw DocumentErrors.Conflict("EVIDENCE_STALE");
    }
    private DocumentJob NewJob(Guid school, Guid enrollment, string kind, Guid template, Guid id, DocumentActor actor) => new()
    { Id = id, SchoolId = school, EnrollmentId = enrollment, Kind = kind, TemplateVersionId = template, ActorId = actor.UserId, IdentityId = actor.IdentityId,
        SessionId = actor.SessionId, CreatedAt = Now, NextAttemptAt = Now, Status = "Pending" };
    private async Task<DocumentJobResponse> JobDto(IDocumentSession s, DocumentJob job, CancellationToken ct)
    {
        DocumentVersion? v = job.ResultVersionId is Guid version ? await Required<DocumentVersion>(s,version,ct) : null;
        return new(job.Id,job.Status,job.DocumentId,v?.State == "PublishedReady",Wire.Read<string[]>(job.MissingItemsJson),job.SourceSnapshotVersion,v?.State,job.ErrorCode);
    }
    public async Task<DocumentJobResponse> JobAsync(Guid school, Guid job, DocumentActor actor, CancellationToken ct)
    {
        await Authority(school,null,actor,false,ct);
        DocumentJob j; await using (var read = await store.OpenAsync(school,false,ct)) j = await Required<DocumentJob>(read,job,ct);
        var a = await Authority(school,j.EnrollmentId,actor,false,ct);
        if (!a.Administrator || j.ActorId != actor.UserId && !a.Administrator) throw DocumentErrors.Missing();
        await using var s = await store.OpenAsync(school,false,ct); return await JobDto(s,await Required<DocumentJob>(s,job,ct),ct);
    }
    public async Task ProcessJobAsync(Guid school, Guid id, CancellationToken ct)
    {
        DocumentJob job; var lease = Guid.NewGuid();
        await using (var s = await store.OpenAsync(school,true,ct))
        {
            job = await Required<DocumentJob>(s,id,ct);
            if (job.Status is "Completed" or "Failed" || job.LeaseUntil > Now || job.NextAttemptAt > Now) return;
            job.Status = "Generating"; job.Attempts++; job.LeaseId = lease; job.LeaseUntil = Now.AddMinutes(5); await s.CommitAsync(ct);
        }
        try
        {
            var actor = await owners.ReadmitAsync(new(job.IdentityId,job.SessionId,job.ActorId,null,null,Now,id.ToString("D")),ct);
            var a = await Authority(school,job.EnrollmentId,actor,true,ct);
            if (a.StudentId is not Guid student) throw DocumentErrors.Rule("STUDENT_BINDING_REQUIRED");
            var profile = await owners.ProfileAsync(student,ct);
            EvidencePayload? evidence = null;
            if (job.Kind != "TrainingContract")
            { evidence = await owners.EvidenceAsync(school,job.EnrollmentId,job.SourceSnapshotVersion,actor,ct); ValidateEvidence(evidence,school,job.EnrollmentId,job.Kind,job.SourceSnapshotVersion); }
            FileObject templateFile;
            await using (var s = await store.OpenAsync(school,false,ct))
            {
                var template = await Required<TemplateVersion>(s,job.TemplateVersionId,ct);
                if (template.Status != "Active") throw DocumentErrors.Rule("TEMPLATE_NO_LONGER_ACTIVE");
                templateFile = await Required<FileObject>(s,template.FileId,ct); Clean(templateFile,"Template");
            }
            var basis = new RenderBasis("document-basis-v1",job.Kind,school,job.EnrollmentId,student,a.SchoolName,a.Category,$"{profile.FirstName} {profile.LastName}",
                a.SchoolRevision,a.EnrollmentRevision,profile.Revision,job.TemplateVersionId,job.CreatedAt,evidence,job.Id);
            var rendered = await renderer.RenderAsync(await Bytes(templateFile,ct),basis,ct);
            var docx = await StageFile(school,actor.UserId,"Rendered",rendered.Docx,"application/vnd.openxmlformats-officedocument.wordprocessingml.document",ct);
            var pdf = await StageFile(school,actor.UserId,"Rendered",rendered.Pdf,"application/pdf",ct);
            var frozen = await StageFile(school,actor.UserId,"SourceBasis",Encoding.UTF8.GetBytes(Wire.Save(basis)),"application/json",ct);
            Clean(docx,"Rendered"); Clean(pdf,"Rendered"); Clean(frozen,"SourceBasis");
            actor = await owners.ReadmitAsync(actor,ct); a = await Authority(school,job.EnrollmentId,actor,true,ct);
            if (a.SchoolRevision != basis.SchoolRevision || a.EnrollmentRevision != basis.EnrollmentRevision || a.StudentId != student || (await owners.ProfileAsync(student,ct)).Revision != profile.Revision)
                throw DocumentErrors.Conflict("RENDER_BASIS_CHANGED");
            var currentRevision = evidence is null ? 0 : await owners.RevisionAsync(school,job.EnrollmentId,ct);
            if (evidence is not null && currentRevision != evidence.DependencyRevision) throw DocumentErrors.Conflict("EVIDENCE_STALE");
            await using var write = await store.OpenAsync(school,true,ct); Admission(actor); if (a.ValidUntil <= Now) throw DocumentErrors.Dependency("AUTHORITY_EXPIRED");
            var currentJob = await Required<DocumentJob>(write,id,ct);
            if (currentJob.Status != "Generating" || currentJob.LeaseId != lease || currentJob.LeaseUntil <= Now) return;
            var templateNow = await Required<TemplateVersion>(write,job.TemplateVersionId,ct);
            if (templateNow.Status != "Active") throw DocumentErrors.Conflict("TEMPLATE_NO_LONGER_ACTIVE");
            var receipt = await Required<OperationReceipt>(write,id,ct);
            if (job.Kind == "TrainingContract")
            {
                var contract = new TrainingContract { Id = Guid.NewGuid(), SchoolId = school, EnrollmentId = job.EnrollmentId, StudentId = student,
                    TemplateVersionId = job.TemplateVersionId, GeneratedFileId = pdf.Id, BasisFileId = frozen.Id, BasisSchemaVersion = basis.SchemaVersion,
                    Status = "Generated", ReplacesContractId = job.ReplacesContractId, GeneratedBy = actor.UserId, GeneratedAt = Now, Revision = 1 };
                write.Add(contract); await Event(write,contract,"Generated",null,"Generated",1,receipt,"Contract generated from frozen owner projections.",ct);
                receipt.ContractId = contract.Id; currentJob.DocumentId = contract.Id;
                AddArtifacts(write,school,contract.Id,null,pdf.Id,docx.Id);
            }
            else
            {
                var checkpoint = await write.FindAsync<EvidenceCheckpoint>(x => x.Id == job.EnrollmentId,ct);
                if (checkpoint is null) { checkpoint = new() { Id = job.EnrollmentId, SchoolId = school, DependencyRevision = currentRevision, LastChangeId = id, ObservedAt = Now }; write.Add(checkpoint); await write.FlushAsync(ct); }
                if (checkpoint.DependencyRevision > currentRevision) throw DocumentErrors.Conflict("EVIDENCE_STALE");
                if (checkpoint.DependencyRevision < currentRevision) await Invalidate(write,school,job.EnrollmentId,currentRevision,id,receipt,ct);
                var d = await write.FindAsync<TrainingDocument>(x => x.EnrollmentId == job.EnrollmentId && x.Kind == job.Kind,ct);
                var created = d is null;
                d ??= new TrainingDocument { Id = Guid.NewGuid(), SchoolId = school, EnrollmentId = job.EnrollmentId, Kind = job.Kind, Revision = 1 };
                var before = d.CurrentVersionId; if (created) write.Add(d); else d.Revision++;
                await RootEvent(write,d,before,created,receipt,"New immutable document version prepared.",ct);
                var existing = await write.ListAsync<DocumentVersion>(x => x.DocumentId == d.Id,ct);
                var v = new DocumentVersion { Id = Guid.NewGuid(), SchoolId = school, DocumentId = d.Id, EnrollmentId = job.EnrollmentId,
                    VersionNo = existing.Count == 0 ? 1 : existing.Max(x => x.VersionNo)+1, TemplateVersionId = job.TemplateVersionId, EvidenceSnapshotId = evidence!.SnapshotId,
                    DependencyRevision = evidence.DependencyRevision, EvidenceStatus = evidence.Status, RenderedFileId = pdf.Id, BasisFileId = frozen.Id,
                    BasisSchemaVersion = basis.SchemaVersion, RendererVersion = rendered.RendererVersion, State = "Prepared", Revision = 1, GeneratedBy = actor.UserId, GeneratedAt = Now };
                write.Add(v); await Event(write,v,"Prepared",null,"Prepared",1,receipt,"Exact evidence snapshot rendered.",ct);
                AddArtifacts(write,school,null,v.Id,pdf.Id,docx.Id); receipt.DocumentId = d.Id; currentJob.DocumentId = d.Id; currentJob.ResultVersionId = v.Id;
            }
            receipt.Status = "Succeeded"; receipt.OutcomeCode = "GENERATED"; receipt.CompletedAt = Now;
            currentJob.Status = "Completed"; currentJob.LeaseUntil = null; currentJob.LeaseId = null; await write.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            await using var s = await store.OpenAsync(school,true,ct); var j = await Required<DocumentJob>(s,id,ct);
            if (j.LeaseId != lease || j.Status != "Generating") return;
            var transient = e is not BusinessException || e is ServiceUnavailableException;
            j.Status = transient && j.Attempts < 5 ? "Pending" : "Failed"; j.NextAttemptAt = Now.AddSeconds(Math.Min(300,Math.Pow(2,j.Attempts)*5));
            j.ErrorCode = e is BusinessException dex ? dex.Code : "GENERATION_UNAVAILABLE"; j.LeaseUntil = null; j.LeaseId = null;
            if (j.Status == "Failed") { var r = await Required<OperationReceipt>(s,id,ct); r.Status = "Rejected"; r.CompletedAt = Now; r.OutcomeCode = j.ErrorCode; }
            await s.CommitAsync(ct);
        }
    }
    private static void AddArtifacts(IDocumentSession s, Guid school, Guid? contract, Guid? version, Guid pdf, Guid docx)
    {
        foreach (var pair in new[] { ("pdf",pdf), ("docx",docx) }) s.Add(new RenderedArtifact { Id = Guid.NewGuid(), SchoolId = school, ContractId = contract, DocumentVersionId = version, Format = pair.Item1, FileId = pair.Item2 });
    }
}
