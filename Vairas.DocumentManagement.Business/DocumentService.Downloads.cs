using Vairas.DocumentManagement.Domain;
namespace Vairas.DocumentManagement.Business;

public sealed partial class DocumentService
{
    public async Task<DocumentDownloadResponse> DownloadAsync(Guid school, Guid document, Guid? version, bool historical, string format, DocumentActor actor, CancellationToken ct)
    {
        await Authority(school,null,actor,false,ct);
        if (format is not ("pdf" or "docx")) throw DocumentErrors.Invalid("INVALID_FORMAT");
        Guid file; Guid? selectedVersion = null; Guid? signedCopyId = null; DateTimeOffset authorityUntil;
        TrainingDocument? d; TrainingContract? c;
        await using (var read = await store.OpenAsync(school,false,ct))
        { d = await read.FindAsync<TrainingDocument>(x => x.Id == document,ct); c = d is null ? await read.FindAsync<TrainingContract>(x => x.Id == document,ct) : null; }
        if (d is not null)
        {
            var a = await Authority(school,d.EnrollmentId,actor,false,ct);
            authorityUntil = a.ValidUntil;
            if (!a.Administrator && !a.Self && !a.AssignedTeacher) throw DocumentErrors.Missing();
            await using var read = await store.OpenAsync(school,false,ct);
            var v = await Required<DocumentVersion>(read,version ?? d.CurrentVersionId ?? throw DocumentErrors.Rule("NO_CURRENT_DOCUMENT"),ct);
            if (v.DocumentId != d.Id) throw DocumentErrors.Missing();
            if (!historical)
            {
                if (d.CurrentVersionId != v.Id || !DocumentRules.Published(v.State)) throw DocumentErrors.Rule("CURRENT_PUBLICATION_REQUIRED");
            }
            else if (!DocumentRules.CanRead(a.Administrator,a.Self,a.AssignedTeacher,d.Kind,v.State is not ("Prepared" or "Indeterminate"))) throw DocumentErrors.Missing();
            var artifact = await read.FindAsync<RenderedArtifact>(x => x.DocumentVersionId == v.Id && x.Format == format,ct) ?? throw DocumentErrors.Missing();
            file = artifact.FileId; selectedVersion = v.Id;
        }
        else if (c is not null)
        {
            var a = await Authority(school,c.EnrollmentId,actor,false,ct);
            authorityUntil = a.ValidUntil;
            if (!a.Administrator && !a.Self) throw DocumentErrors.Missing();
            if (!historical && c.Status == "Cancelled") throw DocumentErrors.Rule("CONTRACT_CANCELLED");
            await using var read = await store.OpenAsync(school,false,ct);
            if (version is Guid copy)
            {
                var signed = await Required<ContractSignedCopy>(read,copy,ct);
                if (signed.ContractId != document || format != "pdf" || !historical && !signed.IsCurrent) throw DocumentErrors.Missing();
                file = signed.FileId; signedCopyId = signed.Id;
            }
            else file = (await read.FindAsync<RenderedArtifact>(x => x.ContractId == document && x.Format == format,ct) ?? throw DocumentErrors.Missing()).FileId;
        }
        else throw DocumentErrors.Missing();
        var grant = new DownloadGrant { Id = Guid.NewGuid(), SchoolId = school, ActorId = actor.UserId, DocumentId = document, FileId = file,
            VersionId = selectedVersion, SignedCopyId = signedCopyId, Historical = historical, ExpiresAt = Now.AddSeconds(60) };
        await using (var write = await store.OpenAsync(school,true,ct)) { Admission(actor); if (authorityUntil <= Now) throw DocumentErrors.Dependency("AUTHORITY_EXPIRED"); write.Add(grant); await write.CommitAsync(ct); }
        return new(document,$"{options.PublicOrigin.TrimEnd('/')}/api/v1/document-downloads/{grant.Id:D}?schoolId={school:D}",grant.ExpiresAt);
    }
    public async Task<DownloadContent> ContentAsync(Guid school, Guid grantId, DocumentActor actor, CancellationToken ct)
    {
        await Authority(school,null,actor,false,ct);
        DownloadGrant grant; FileObject f; DocumentVersion? v = null; TrainingDocument? d = null; TrainingContract? contract = null;
        await using (var s = await store.OpenAsync(school,false,ct))
        {
            grant = await Required<DownloadGrant>(s,grantId,ct);
            if (grant.ActorId != actor.UserId || grant.ExpiresAt <= Now) throw DocumentErrors.Missing();
            f = await Required<FileObject>(s,grant.FileId,ct); if (f.ScanStatus != "Clean") throw DocumentErrors.Rule("CLEAN_FILE_REQUIRED");
            if (grant.VersionId is Guid version)
            { v = await Required<DocumentVersion>(s,version,ct); d = await Required<TrainingDocument>(s,v.DocumentId,ct); }
            else contract = await Required<TrainingContract>(s,grant.DocumentId,ct);
            if (grant.SignedCopyId is Guid copy && !grant.Historical && !(await Required<ContractSignedCopy>(s,copy,ct)).IsCurrent) throw DocumentErrors.Conflict("SIGNED_COPY_CHANGED");
        }
        var a = await Authority(school,d?.EnrollmentId ?? contract!.EnrollmentId,actor,false,ct);
        if (d is not null && v is not null)
        {
            if (!DocumentRules.CanRead(a.Administrator,a.Self,a.AssignedTeacher,d.Kind,v.State is not ("Prepared" or "Indeterminate"))) throw DocumentErrors.Missing();
            if (!grant.Historical)
            {
                if (d.CurrentVersionId != v.Id || !DocumentRules.Published(v.State)) throw DocumentErrors.Rule("CURRENT_PUBLICATION_REQUIRED");
                if (v.State == "PublishedReady") await OwnerCurrent(school,v,actor,Guid.NewGuid(),ct);
            }
        }
        else
        {
            if (!a.Administrator && !a.Self) throw DocumentErrors.Missing();
            if (!grant.Historical && contract!.Status == "Cancelled") throw DocumentErrors.Rule("CONTRACT_CANCELLED");
        }
        var bytes = await Bytes(f,ct);
        Admission(actor); if (grant.ExpiresAt <= Now || a.ValidUntil <= Now) throw DocumentErrors.Dependency("ADMISSION_EXPIRED");
        // Recheck local state after Blob I/O; owner decision defines the remote authorization instant.
        if (!grant.Historical && v is not null)
        {
            await using var read = await store.OpenAsync(school,false,ct);
            var current = await Required<TrainingDocument>(read,v.DocumentId,ct); var child = await Required<DocumentVersion>(read,v.Id,ct);
            if (current.CurrentVersionId != v.Id || !DocumentRules.Published(child.State)) throw DocumentErrors.Conflict("DOCUMENT_CHANGED");
        }
        if (!grant.Historical && contract is not null)
        {
            await using var read = await store.OpenAsync(school,false,ct);
            if ((await Required<TrainingContract>(read,contract.Id,ct)).Status == "Cancelled" || grant.SignedCopyId is Guid copy && !(await Required<ContractSignedCopy>(read,copy,ct)).IsCurrent)
                throw DocumentErrors.Conflict("CONTRACT_CHANGED");
        }
        var extension = f.MediaType == "application/pdf" ? "pdf" : "docx";
        return new(bytes,f.MediaType,$"{grant.DocumentId:D}{(grant.Historical ? "-history" : "")}.{extension}");
    }
}
