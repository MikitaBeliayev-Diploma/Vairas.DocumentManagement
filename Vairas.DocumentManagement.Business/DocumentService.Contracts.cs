using Vairas.DocumentManagement.Domain;
namespace Vairas.DocumentManagement.Business;

public sealed partial class DocumentService
{
    public async Task<ContractResponse[]> ContractsAsync(Guid school, Guid enrollment, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,enrollment,actor,false,ct); if (!a.Administrator && !a.Self) throw DocumentErrors.Missing();
        await using var read = await store.OpenAsync(school,false,ct); var list = new List<ContractResponse>();
        foreach (var contract in (await read.ListAsync<TrainingContract>(x => x.EnrollmentId == enrollment,ct)).OrderBy(x => x.GeneratedAt).ThenBy(x => x.Id)) list.Add(await ContractDto(read,contract,ct));
        return list.ToArray();
    }
    public async Task<ContractResponse> ContractAsync(Guid school, Guid contract, DocumentActor actor, CancellationToken ct)
    {
        await Authority(school,null,actor,false,ct);
        TrainingContract c; await using (var read = await store.OpenAsync(school,false,ct)) c = await Required<TrainingContract>(read,contract,ct);
        var a = await Authority(school,c.EnrollmentId,actor,false,ct);
        if (!a.Administrator && !a.Self) throw DocumentErrors.Missing();
        await using var s = await store.OpenAsync(school,false,ct); return await ContractDto(s,await Required<TrainingContract>(s,contract,ct),ct);
    }
    public async Task<CommandResult<ContractResponse>> AttachAsync(Guid school, Guid contract, AttachSignedCopyRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,null,actor,true,ct); Text(request.Basis);
        if (request.DeclaredSignedAt > Now) throw DocumentErrors.Invalid("FUTURE_SIGNING_DATE");
        return await Command(school,operation,"AttachSignedCopy",contract,request,actor,a,async (s,r) =>
        {
            var c = await Required<TrainingContract>(s,contract,ct); Cas(c.Revision,request.ExpectedRevision);
            if (c.Status == "Cancelled") throw DocumentErrors.Rule("CONTRACT_CANCELLED");
            Clean(await Required<FileObject>(s,request.FileId,ct),"SignedCopy");
            var copies = await s.ListAsync<ContractSignedCopy>(x => x.ContractId == contract,ct);
            foreach (var old in copies.Where(x => x.IsCurrent)) old.IsCurrent = false;
            await s.FlushAsync(ct);
            var copy = new ContractSignedCopy { Id = Guid.NewGuid(), SchoolId = school, ContractId = contract, CopyNo = copies.Count == 0 ? 1 : copies.Max(x => x.CopyNo)+1,
                FileId = request.FileId, IsCurrent = true, DeclaredSignedAt = request.DeclaredSignedAt.ToUniversalTime(), RecordedAt = Now, RecordedBy = actor.UserId, Basis = request.Basis };
            s.Add(copy); var before = c.Status; c.Status = "Signed"; c.Revision++;
            await Event(s,c,copies.Count == 0 ? "SignedCopyAttached" : "SignedCopyReplaced",before,c.Status,c.Revision,r,request.Basis,ct,copy.Id);
            r.ContractId = contract; return await ContractDto(s,c,ct);
        },ct);
    }
    public async Task<CommandResult<ContractResponse>> CancelAsync(Guid school, Guid contract, RevisionRequest request, Guid operation, DocumentActor actor, CancellationToken ct)
    {
        var a = await Authority(school,null,actor,true,ct); Text(request.Reason);
        return await Command(school,operation,"CancelContract",contract,request,actor,a,async (s,r) =>
        {
            var c = await Required<TrainingContract>(s,contract,ct); Cas(c.Revision,request.ExpectedRevision);
            if (c.Status == "Cancelled") throw DocumentErrors.Rule("CONTRACT_CANCELLED");
            var before = c.Status; c.Status = "Cancelled"; c.Revision++;
            await Event(s,c,"Cancelled",before,c.Status,c.Revision,r,request.Reason,ct); r.ContractId = contract;
            return await ContractDto(s,c,ct);
        },ct);
    }
}
