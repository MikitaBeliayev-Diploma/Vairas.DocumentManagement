using Vairas.DocumentManagement.Domain;
namespace Vairas.DocumentManagement.Business;

public sealed partial class DocumentService
{
    public async Task<DocumentResponse[]> DocumentsAsync(Guid school, Guid enrollment, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,enrollment,actor,false,ct);
        if (!a.Administrator && !a.Self && !a.AssignedTeacher) throw DocumentErrors.Missing();
        await using var s = await store.OpenAsync(school,false,ct); var list = new List<DocumentResponse>();
        foreach (var d in (await s.ListAsync<TrainingDocument>(x => x.EnrollmentId == enrollment,ct)).OrderBy(x => x.Kind))
        { var dto = await DocumentDto(s,d,a.Administrator,ct); if (a.Administrator || dto.Versions.Length > 0) list.Add(dto); }
        return list.ToArray();
    }
    public async Task<DocumentResponse> DocumentAsync(Guid school, Guid document, DocumentActor actor, CancellationToken ct)
    {
        await Authority(school,null,actor,false,ct);
        TrainingDocument d; await using (var s = await store.OpenAsync(school,false,ct)) d = await Required<TrainingDocument>(s,document,ct);
        var a = await Authority(school,d.EnrollmentId,actor,false,ct);
        if (!a.Administrator && !a.Self && !a.AssignedTeacher) throw DocumentErrors.Missing();
        await using var read = await store.OpenAsync(school,false,ct); var dto = await DocumentDto(read,await Required<TrainingDocument>(read,document,ct),a.Administrator,ct);
        if (!a.Administrator && dto.Versions.Length == 0) throw DocumentErrors.Missing(); return dto;
    }
    public async Task<CommandResult<DocumentResponse>> VersionCommandAsync(Guid school, Guid document, Guid version, string action, VersionCommandRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        await Authority(school,null,actor,false,ct);
        TrainingDocument root; await using (var s = await store.OpenAsync(school,false,ct)) root = await Required<TrainingDocument>(s,document,ct);
        var a = await Authority(school,root.EnrollmentId,actor,true,ct); Text(request.Reason);
        if (action is not ("review" or "withdraw" or "select" or "clear")) throw DocumentErrors.Invalid();
        var replay = await Replay<DocumentResponse,object>(school,operation,"Version:"+action,document,new { version, request },actor,ct); if (replay is not null) return replay;
        // Selection is an explicit publication-like current-use decision, never a historical fallback.
        if (action == "select")
        {
            DocumentVersion candidate; await using (var read = await store.OpenAsync(school,false,ct)) candidate = await Required<DocumentVersion>(read,version,ct);
            if (candidate.DocumentId != document || !DocumentRules.Published(candidate.State)) throw DocumentErrors.Rule("PUBLISHED_VERSION_REQUIRED");
            if (candidate.State == "PublishedReady") await OwnerCurrent(school,candidate,actor,Guid.NewGuid(),ct);
        }
        return await Command(school,operation,"Version:"+action,document,new { version, request },actor,a,async (s,r) =>
        {
            var d = await Required<TrainingDocument>(s,document,ct); var v = await Required<DocumentVersion>(s,version,ct);
            if (v.DocumentId != document) throw DocumentErrors.Missing(); Cas(d.Revision,request.ExpectedRevision); Cas(v.Revision,request.ExpectedVersionRevision);
            var before = d.CurrentVersionId;
            if (action == "review")
            {
                if (v.State != "Prepared") throw DocumentErrors.Rule("PREPARED_VERSION_REQUIRED");
                s.Add(new DocumentReview { Id = Guid.NewGuid(), SchoolId = school, DocumentVersionId = version, ActorId = actor.UserId, ReviewedAt = Now, Reason = request.Reason });
            }
            else if (action == "withdraw")
            {
                if (!DocumentRules.CanTransition(v.State,"Withdrawn")) throw DocumentErrors.Rule("INVALID_DOCUMENT_TRANSITION");
                var from = v.State; v.State = "Withdrawn"; v.Revision++;
                await Event(s,v,"Withdrawn",from,v.State,v.Revision,r,request.Reason,ct);
                if (d.CurrentVersionId == version) d.CurrentVersionId = null;
            }
            else if (action == "select")
            {
                var checkpoint = await Required<EvidenceCheckpoint>(s,d.EnrollmentId,ct);
                if (!DocumentRules.Published(v.State) || v.DependencyRevision < checkpoint.DependencyRevision) throw DocumentErrors.Rule("CURRENT_PUBLICATION_REQUIRED");
                d.CurrentVersionId = version;
            }
            else { if (d.CurrentVersionId != version) throw DocumentErrors.Conflict(); d.CurrentVersionId = null; }
            d.Revision++; await RootEvent(s,d,before,false,r,action == "review" ? "Reviewed: "+request.Reason : request.Reason,ct); r.DocumentId = document;
            return await DocumentDto(s,d,true,ct);
        },ct);
    }
    public async Task<CommandResult<DocumentResponse>> PublishAsync(Guid school, Guid document, Guid version, PublishRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        await Authority(school,null,actor,false,ct);
        TrainingDocument root; await using (var read = await store.OpenAsync(school,false,ct)) root = await Required<TrainingDocument>(read,document,ct);
        var a = await Authority(school,root.EnrollmentId,actor,true,ct); Text(request.Reason);
        if (request.Mode is not ("Ready" or "Review")) throw DocumentErrors.Invalid();
        return await Command(school,operation,"Publish",document,new { version, request },actor,a,async (s,r) =>
        {
            var d = await Required<TrainingDocument>(s,document,ct); var v = await Required<DocumentVersion>(s,version,ct);
            if (v.DocumentId != document) throw DocumentErrors.Missing(); Cas(d.Revision,request.ExpectedRevision); Cas(v.Revision,request.ExpectedVersionRevision);
            if (v.State != "Prepared" || !await Reviewed(s,v,ct)) throw DocumentErrors.Rule("REVIEWED_PREPARED_VERSION_REQUIRED");
            var checkpoint = await Required<EvidenceCheckpoint>(s,d.EnrollmentId,ct);
            if (checkpoint.DependencyRevision > v.DependencyRevision) throw DocumentErrors.Conflict("EVIDENCE_STALE");
            var before = d.CurrentVersionId;
            var old = v.State; v.Revision++;
            if (request.Mode == "Ready")
            {
                if (v.EvidenceStatus != "Ready") throw DocumentErrors.Rule("READY_EVIDENCE_REQUIRED");
                var artifact = await ArtifactDigest(s,v,ct); v.State = "Indeterminate";
                s.Add(new PublicationDecision { Id = operation, SchoolId = school, DocumentVersionId = version, ActorId = actor.UserId, IdentityId = actor.IdentityId, SessionId = actor.SessionId,
                    ExpectedRevision = d.Revision+1, ExpectedVersionRevision = v.Revision, ArtifactDigest = artifact, Reason = request.Reason, Status = "Pending", CreatedAt = Now });
                r.Status = "Indeterminate";
            }
            else { v.State = "PublishedReview"; }
            await Event(s,v,request.Mode == "Ready" ? "PublicationRequested" : "PublishedReview",old,v.State,v.Revision,r,request.Reason,ct);
            // Event flushes tracked changes. Change the root pointer only together with its next revision,
            // after persisting the child transition; both changes and their audits commit in this transaction.
            if (request.Mode != "Ready") d.CurrentVersionId = version;
            d.Revision++; await RootEvent(s,d,before,false,r,request.Reason,ct); r.DocumentId = document;
            return await DocumentDto(s,d,true,ct);
        },ct,pending:request.Mode == "Ready");
    }
    private async Task<string> ArtifactDigest(IDocumentSession s, DocumentVersion v, CancellationToken ct)
    {
        var artifacts = await s.ListAsync<RenderedArtifact>(x => x.DocumentVersionId == v.Id,ct);
        var hashes = new List<string>(); foreach (var a in artifacts.OrderBy(x => x.Format)) hashes.Add(a.Format+":"+(await Required<FileObject>(s,a.FileId,ct)).Sha256);
        if (hashes.Count != 2) throw DocumentErrors.Dependency("ARTIFACTS_INCOMPLETE"); return DocumentRules.Digest(string.Join(";",hashes));
    }
    private async Task<OwnerDecisionRequest> OwnerRequest(Guid school, DocumentVersion v, DocumentActor actor, string purpose, CancellationToken ct)
    { await using var s = await store.OpenAsync(school,false,ct); var d = await Required<TrainingDocument>(s,v.DocumentId,ct); return new(school,v.EnrollmentId,v.EvidenceSnapshotId,v.DependencyRevision,v.Id,actor.UserId,d.Kind,await ArtifactDigest(s,v,ct),purpose,actor.TeacherId); }
    private async Task OwnerCurrent(Guid school, DocumentVersion v, DocumentActor actor, Guid operation, CancellationToken ct)
    {
        var result = await owners.DecideAsync(operation,await OwnerRequest(school,v,actor,"CurrentUse",ct),ct);
        if (result.Status != "Accepted" || result.EvaluatedRevision != v.DependencyRevision) throw DocumentErrors.Conflict("EVIDENCE_NOT_CURRENT");
    }
    public async Task ProcessPublicationAsync(Guid school, Guid operation, CancellationToken ct)
    {
        PublicationDecision p; DocumentVersion v;
        await using (var read = await store.OpenAsync(school,false,ct))
        { p = await Required<PublicationDecision>(read,operation,ct); if (p.Status != "Pending") return; v = await Required<DocumentVersion>(read,p.DocumentVersionId,ct); }
        if (v.State is "Stale" or "Withdrawn")
        {
            await using var abandoned = await store.OpenAsync(school,true,ct); var candidate = await Required<DocumentVersion>(abandoned,v.Id,ct);
            var pending = await Required<PublicationDecision>(abandoned,operation,ct);
            if (pending.Status == "Pending" && candidate.State is "Stale" or "Withdrawn")
            { pending.Status = "Stale"; var receipt = await Required<OperationReceipt>(abandoned,operation,ct); receipt.Status = "Rejected"; receipt.CompletedAt = Now; receipt.OutcomeCode = "CANDIDATE_NO_LONGER_PUBLISHABLE"; await abandoned.CommitAsync(ct); }
            return;
        }
        var actor = await owners.ReadmitAsync(new(p.IdentityId,p.SessionId,p.ActorId,null,null,Now,operation.ToString("D")),ct);
        var a = await Authority(school,v.EnrollmentId,actor,true,ct);
        var result = await owners.DecisionAsync(school,operation,actor.UserId,ct) ?? await owners.DecideAsync(operation,await OwnerRequest(school,v,actor,"Publication",ct),ct);
        var currentRevision = await owners.RevisionAsync(school,v.EnrollmentId,ct);
        actor = await owners.ReadmitAsync(actor,ct); a = await Authority(school,v.EnrollmentId,actor,true,ct);
        await using var s = await store.OpenAsync(school,true,ct); Admission(actor); if (a.ValidUntil <= Now) throw DocumentErrors.Dependency("AUTHORITY_EXPIRED");
        p = await Required<PublicationDecision>(s,operation,ct); if (p.Status != "Pending") return;
        v = await Required<DocumentVersion>(s,p.DocumentVersionId,ct); var d = await Required<TrainingDocument>(s,v.DocumentId,ct); var r = await Required<OperationReceipt>(s,operation,ct);
        var checkpoint = await Required<EvidenceCheckpoint>(s,v.EnrollmentId,ct); var before = d.CurrentVersionId;
        var eligible = result.Status == "Accepted" && result.OperationId == operation && result.EvaluatedRevision == v.DependencyRevision &&
            currentRevision == v.DependencyRevision && checkpoint.DependencyRevision <= v.DependencyRevision && d.Revision == p.ExpectedRevision && v.Revision == p.ExpectedVersionRevision && v.State == "Indeterminate";
        if (v.State == "Indeterminate")
        {
            var from = v.State; v.State = eligible ? "PublishedReady" : "Stale"; v.Revision++;
            await Event(s,v,eligible ? "PublishedReady" : "PublicationRejected",from,v.State,v.Revision,r,p.Reason,ct);
            if (eligible) d.CurrentVersionId = v.Id; else if (d.CurrentVersionId == v.Id) d.CurrentVersionId = null;
            d.Revision++; await RootEvent(s,d,before,false,r,p.Reason,ct);
        }
        p.Status = eligible ? "Accepted" : result.Status == "Rejected" ? "Rejected" : "Stale"; p.DecidedAt = result.DecidedAt;
        r.Status = eligible ? "Succeeded" : "Rejected"; r.CompletedAt = Now; r.OutcomeCode = eligible ? "PUBLISHED_READY" : "PUBLICATION_REJECTED";
        await s.CommitAsync(ct);
    }
    public async Task ReconcileAsync(Guid school, Guid enrollment, CancellationToken ct)
    {
        var revision = await owners.RevisionAsync(school,enrollment,ct);
        await using var s = await store.OpenAsync(school,true,ct); var checkpoint = await s.FindAsync<EvidenceCheckpoint>(x => x.Id == enrollment,ct);
        if (checkpoint is not null && checkpoint.DependencyRevision >= revision) return;
        var operation = Guid.NewGuid(); var receipt = SystemReceipt(s,school,operation,"ReconcileEvidence"); await s.FlushAsync(ct);
        await Invalidate(s,school,enrollment,revision,operation,receipt,ct); CompleteSystem(receipt); await s.CommitAsync(ct);
    }
    private OperationReceipt SystemReceipt(IDocumentSession s, Guid school, Guid operation, string command)
    { var r = new OperationReceipt { Id = operation, SchoolId = school, ActorId = Guid.Empty, Operation = command, RequestDigest = DocumentRules.Digest(command+operation), Status = "Pending", CreatedAt = Now }; s.Add(r); return r; }
    private void CompleteSystem(OperationReceipt r) { r.Status = "Succeeded"; r.OutcomeCode = "RECONCILED"; r.CompletedAt = Now; }
    private async Task Invalidate(IDocumentSession s, Guid school, Guid enrollment, long revision, Guid change, OperationReceipt r, CancellationToken ct)
    {
        var checkpoint = await s.FindAsync<EvidenceCheckpoint>(x => x.Id == enrollment,ct);
        if (checkpoint is null) { s.Add(new EvidenceCheckpoint { Id = enrollment, SchoolId = school, DependencyRevision = revision, LastChangeId = change, ObservedAt = Now }); return; }
        if (checkpoint.DependencyRevision >= revision) return;
        checkpoint.DependencyRevision = revision; checkpoint.LastChangeId = change; checkpoint.ObservedAt = Now;
        foreach (var d in (await s.ListAsync<TrainingDocument>(x => x.EnrollmentId == enrollment,ct)).OrderBy(x => x.Id))
        {
            var changed = false; var clearCurrent = false; var before = d.CurrentVersionId;
            foreach (var v in (await s.ListAsync<DocumentVersion>(x => x.DocumentId == d.Id && x.DependencyRevision < revision,ct)).OrderBy(x => x.Id))
            {
                if (v.State is "Stale" or "Withdrawn") continue;
                var from = v.State; v.State = "Stale"; v.Revision++; changed = true;
                await Event(s,v,"EvidenceInvalidated",from,v.State,v.Revision,r,"Owner dependency revision advanced.",ct);
                // Event flushes: the root pointer must change in the same flush as its revision (DB guard), not mid-loop.
                if (d.CurrentVersionId == v.Id) clearCurrent = true;
            }
            if (changed) { if (clearCurrent) d.CurrentVersionId = null; d.Revision++; await RootEvent(s,d,before,false,r,"Owner dependency revision advanced.",ct); }
        }
    }
    public async Task ConsumeAsync(SourceEvent message, CancellationToken ct)
    {
        Id(message.EventId); Id(message.SchoolId); Id(message.EnrollmentId);
        if (message.EventVersion != 1 || message.AggregateVersion < 1 || message.OccurredAt == default ||
            !(message.Producer == "learning" && message.EventType is "TrainingEvidenceInvalidated" or "PracticalRecordConfirmed" or "PracticalRecordRevised" ||
              message.Producer == "school" && message.EventType is "StudentEnrollmentStarted" or "StudentEnrollmentCompleted" or "StudentEnrollmentEnded")) throw DocumentErrors.Invalid("INVALID_EVENT");
        var fingerprint = DocumentRules.Digest(Wire.Save(message with { CorrelationId = "" }));
        await using (var read = await store.OpenAsync(message.SchoolId,false,ct))
        { var prior = await read.FindAsync<InboxMessage>(x => x.Id == message.EventId,ct); if (prior is not null) { if (prior.Fingerprint != fingerprint) throw DocumentErrors.Conflict("EVENT_ID_REUSED"); return; } }
        var revision = await owners.RevisionAsync(message.SchoolId,message.EnrollmentId,ct);
        await using var s = await store.OpenAsync(message.SchoolId,true,ct);
        var seen = await s.FindAsync<InboxMessage>(x => x.Id == message.EventId,ct);
        if (seen is not null) { if (seen.Fingerprint != fingerprint) throw DocumentErrors.Conflict("EVENT_ID_REUSED"); return; }
        var r = SystemReceipt(s,message.SchoolId,Guid.NewGuid(),"ReconcileEvent"); await s.FlushAsync(ct);
        await Invalidate(s,message.SchoolId,message.EnrollmentId,revision,message.EventId,r,ct);
        s.Add(new InboxMessage { Id = message.EventId, SchoolId = message.SchoolId, Fingerprint = fingerprint, ProcessedAt = Now }); CompleteSystem(r); await s.CommitAsync(ct);
    }
    public async Task<HistoryResponse[]> HistoryAsync(Guid school, Guid document, bool contract, DocumentActor actor, CancellationToken ct)
    {
        Guid enrollment;
        if (contract) enrollment = (await ContractAsync(school,document,actor,ct)).EnrollmentId; else enrollment = (await DocumentAsync(school,document,actor,ct)).EnrollmentId;
        var authority = await Authority(school,enrollment,actor,false,ct);
        await using var s = await store.OpenAsync(school,false,ct);
        var versions = contract ? [] : (await s.ListAsync<DocumentVersion>(x => x.DocumentId == document && (authority.Administrator || x.State != "Prepared" && x.State != "Indeterminate"),ct)).Select(x => x.Id).ToArray();
        var visiblePointers = versions.Select(x => x.ToString("D")).ToArray();
        return (await s.ListAsync<StateEvent>(x => contract ? x.ContractId == document : x.DocumentVersionId != null && versions.Contains(x.DocumentVersionId.Value) || x.DocumentId == document &&
            (authority.Administrator || visiblePointers.Contains(x.ToState) || x.FromState != null && visiblePointers.Contains(x.FromState)),ct))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Select(x => new HistoryResponse(x.Id,x.Action,x.SequenceNo,x.FromState,x.ToState,x.ActorId,x.OccurredAt,x.Reason)).ToArray();
    }
}
