using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Xunit;
namespace Vairas.DocumentManagement.Tests;

public sealed class BusinessTests
{
    private static async Task Error(int status, Func<Task> action, string? code = null)
    { var e = await Business.ThrowsBusinessAsync(action); Assert.Equal(status,e.HttpStatus()); if (code is not null) Assert.Equal(code,e.Code); }
    [Fact] public async Task Template_commands_replay_original_outcome_after_later_changes()
    {
        var w = new World(); var key = Guid.NewGuid(); var request = new CreateTemplateRequest("PracticalJournal","Original");
        var first = await w.Service.CreateTemplateAsync(w.School,request,key,w.Actor,default);
        var renamed = await w.Service.RenameTemplateAsync(w.School,first.Value.TemplateId,new("Changed",1),Guid.NewGuid(),w.Actor,default);
        var replay = await w.Service.CreateTemplateAsync(w.School,request,key,w.Actor,default);
        Assert.True(replay.Replay); Assert.Equal("Original",replay.Value.Name); Assert.Equal(2,renamed.Value.Revision);
        await Error(409,() => w.Service.CreateTemplateAsync(w.School,request with { Name = "Other" },key,w.Actor,default));
        await Error(409,() => w.Service.CreateTemplateAsync(w.School,request,key,w.Actor with { UserId = Guid.NewGuid() },default));
        Assert.Single(await w.Service.TemplatesAsync(w.School,w.Actor,default));
        Assert.Equal("Changed",(await w.Service.TemplateAsync(w.School,first.Value.TemplateId,w.Actor,default)).Name);
    }
    [Theory] [InlineData("", "Name")] [InlineData("Other","Name")] [InlineData("TrainingContract","")]
    public async Task Invalid_template_configuration_is_rejected(string kind, string name)
    { var w = new World(); await Error(400,() => w.Service.CreateTemplateAsync(w.School,new(kind,name),Guid.NewGuid(),w.Actor,default)); }
    [Fact] public async Task Missing_operation_and_expired_admission_are_rejected()
    {
        var w = new World(); await Error(400,() => w.Service.CreateTemplateAsync(w.School,new("PracticalJournal","A"),Guid.Empty,w.Actor,default));
        await Error(503,() => w.Service.TemplatesAsync(w.School,w.Actor with { ValidUntil = w.Clock.Time },default));
        await Error(400,() => w.Service.TemplatesAsync(Guid.Empty,w.Actor,default));
    }
    [Fact] public async Task Competing_template_updates_leave_no_stale_child_effect()
    {
        var w = new World(); var t = await w.Template();
        await Error(409,() => w.Service.RenameTemplateAsync(w.School,t.TemplateId,new("Wrong",1),Guid.NewGuid(),w.Actor,default));
        var upload = (await w.Service.UploadAsync(w.School,"Template","Another template"u8.ToArray(),Guid.NewGuid(),w.Actor,default)).Value;
        await Error(409,() => w.Service.AddVersionAsync(w.School,t.TemplateId,upload.FileId,1,Guid.NewGuid(),w.Actor,default));
        var after = await w.Service.TemplateAsync(w.School,t.TemplateId,w.Actor,default); Assert.Single(after.Versions); Assert.Equal(t.Revision,after.Revision);
        after = (await w.Service.TemplateLifecycleAsync(w.School,t.TemplateId,t.Versions[0].TemplateVersionId,"archive",new(t.Revision,"Retire."),Guid.NewGuid(),w.Actor,default)).Value;
        Assert.Null(after.ActiveVersionId);
        await Error(422,() => w.Service.TemplateLifecycleAsync(w.School,t.TemplateId,t.Versions[0].TemplateVersionId,"activate",new(after.Revision,"No resurrection."),Guid.NewGuid(),w.Actor,default));
    }
    [Fact] public async Task Activation_archives_previous_version_and_keeps_immutable_files()
    {
        var w = new World(); var t = await w.Template(); var f = (await w.Service.UploadAsync(w.School,"Template","New template"u8.ToArray(),Guid.NewGuid(),w.Actor,default)).Value;
        var key = Guid.NewGuid(); t = (await w.Service.AddVersionAsync(w.School,t.TemplateId,f.FileId,t.Revision,key,w.Actor,default)).Value;
        var replay = await w.Service.AddVersionAsync(w.School,t.TemplateId,f.FileId,t.Revision-1,key,w.Actor,default); Assert.True(replay.Replay);
        t = (await w.Service.TemplateLifecycleAsync(w.School,t.TemplateId,t.Versions[^1].TemplateVersionId,"activate",new(t.Revision,"Replace."),Guid.NewGuid(),w.Actor,default)).Value;
        Assert.Equal("Archived",t.Versions[0].Status); Assert.Equal("Active",t.Versions[1].Status);
        await Error(404,() => w.Service.TemplateLifecycleAsync(w.School,Guid.NewGuid(),t.Versions[1].TemplateVersionId,"archive",new(1,"Wrong owner."),Guid.NewGuid(),w.Actor,default));
    }
    [Fact] public async Task Pending_upload_is_not_usable_and_scan_recovery_is_idempotent()
    {
        var w = new World(); w.Scanner.Status = "Pending"; var key = Guid.NewGuid();
        var f = (await w.Service.UploadAsync(w.School,"Template","DOCX"u8.ToArray(),key,w.Actor,default)).Value;
        Assert.Equal("Pending",f.ScanStatus); Assert.True((await w.Service.UploadAsync(w.School,"Template","DOCX"u8.ToArray(),key,w.Actor,default)).Replay);
        var t = (await w.Service.CreateTemplateAsync(w.School,new("PracticalJournal","A"),Guid.NewGuid(),w.Actor,default)).Value;
        await Error(422,() => w.Service.AddVersionAsync(w.School,t.TemplateId,f.FileId,t.Revision,Guid.NewGuid(),w.Actor,default));
        await w.Service.ScanFileAsync(w.School,f.FileId,default); w.Scanner.Status = "Clean"; await w.Service.ScanFileAsync(w.School,f.FileId,default); await w.Service.ScanFileAsync(w.School,f.FileId,default);
        Assert.Equal("Clean",(await w.Service.FileAsync(w.School,f.FileId,w.Actor,default)).ScanStatus);
    }
    [Fact] public async Task Rejected_and_invalid_files_never_become_artifact_references()
    {
        var w = new World(); w.Scanner.Status = "Rejected";
        var f = (await w.Service.UploadAsync(w.School,"SignedCopy","%PDF-1.7 test"u8.ToArray(),Guid.NewGuid(),w.Actor,default)).Value; Assert.Equal("Rejected",f.ScanStatus);
        await Error(400,() => w.Service.UploadAsync(w.School,"SignedCopy","not pdf"u8.ToArray(),Guid.NewGuid(),w.Actor,default));
        await Error(400,() => w.Service.UploadAsync(w.School,"Rendered",[1],Guid.NewGuid(),w.Actor,default));
        await Error(400,() => w.Service.UploadAsync(w.School,"Template",[],Guid.NewGuid(),w.Actor,default));
    }
    [Fact] public async Task Prepared_document_needs_review_and_ready_publication_is_separate()
    {
        var w = new World(); var d = await w.Generate(); Assert.Equal("Prepared",d.Versions[0].State); Assert.Null(d.CurrentVersionId);
        await Error(422,() => w.Service.PublishAsync(w.School,d.DocumentId,d.Versions[0].DocumentVersionId,new(d.Revision,1,"Ready","Not reviewed."),Guid.NewGuid(),w.Actor,default));
        d = await w.Publish(await w.Review(d)); Assert.Equal("PublishedReady",d.Versions[0].State); Assert.Equal(d.Versions[0].DocumentVersionId,d.CurrentVersionId);
        Assert.Single(w.Owners.Decisions);
        Assert.Single(await w.Service.DocumentsAsync(w.School,w.Enrollment,w.Actor,default));
        Assert.Contains(await w.Service.HistoryAsync(w.School,d.DocumentId,false,w.Actor,default),x => x.Action == "PublishedReady");
    }
    [Fact] public async Task Incomplete_evidence_can_only_publish_review_with_retained_deficits()
    {
        var w = new World(); w.Owners.Evidence = w.Owners.Evidence with { Status = "Incomplete", IsCurrent = false, Checks = [new("CONFIRMATION","Student","Unsatisfied","Student confirmation is pending.")] };
        var d = await w.Review(await w.Generate());
        await Error(422,() => w.Service.PublishAsync(w.School,d.DocumentId,d.Versions[0].DocumentVersionId,new(d.Revision,1,"Ready","Ready requested."),Guid.NewGuid(),w.Actor,default));
        d = await w.Publish(d,"Review"); Assert.Equal("PublishedReview",d.Versions[0].State); Assert.Empty(w.Owners.Decisions);
    }
    [Fact] public async Task Ready_decision_timeout_recovers_original_owner_outcome_without_new_publication()
    {
        var w = new World(); var d = await w.Review(await w.Generate()); var operation = Guid.NewGuid(); var request = new PublishRequest(d.Revision,1,"Ready","Checked.");
        await w.Service.PublishAsync(w.School,d.DocumentId,d.Versions[0].DocumentVersionId,request,operation,w.Actor,default);
        w.Owners.LoseDecisionReply = true; await Error(503,() => w.Service.ProcessPublicationAsync(w.School,operation,default));
        Assert.Equal("Indeterminate",(await w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default)).Versions[0].State);
        await w.Service.ProcessPublicationAsync(w.School,operation,default); await w.Service.ProcessPublicationAsync(w.School,operation,default);
        Assert.Single(w.Owners.Decisions); Assert.Equal("PublishedReady",(await w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default)).Versions[0].State);
        Assert.True((await w.Service.PublishAsync(w.School,d.DocumentId,d.Versions[0].DocumentVersionId,request,operation,w.Actor,default)).Replay);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task Correction_wins_before_or_after_owner_decision_and_clears_selection(bool beforeDecision)
    {
        var w = new World(); var d = await w.Review(await w.Generate()); var operation = Guid.NewGuid();
        await w.Service.PublishAsync(w.School,d.DocumentId,d.Versions[0].DocumentVersionId,new(d.Revision,1,"Ready","Publish."),operation,w.Actor,default);
        if (!beforeDecision) { await w.Service.ProcessPublicationAsync(w.School,operation,default); }
        w.Owners.Revision = 2; await w.Service.ReconcileAsync(w.School,w.Enrollment,default);
        if (beforeDecision) await w.Service.ProcessPublicationAsync(w.School,operation,default);
        d = await w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default); Assert.Null(d.CurrentVersionId); Assert.Equal("Stale",d.Versions[0].State);
        await w.Service.ReconcileAsync(w.School,w.Enrollment,default); Assert.Equal(d.Revision,(await w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default)).Revision);
    }
    [Fact] public async Task Withdrawal_and_selection_use_parent_and_version_CAS_without_fallback()
    {
        var w = new World(); var d = await w.Publish(await w.Review(await w.Generate())); var v = d.Versions[0];
        d = (await w.Service.VersionCommandAsync(w.School,d.DocumentId,v.DocumentVersionId,"clear",new(d.Revision,v.Revision,"Clear."),Guid.NewGuid(),w.Actor,default)).Value; Assert.Null(d.CurrentVersionId);
        d = (await w.Service.VersionCommandAsync(w.School,d.DocumentId,v.DocumentVersionId,"select",new(d.Revision,v.Revision,"Select explicitly."),Guid.NewGuid(),w.Actor,default)).Value;
        await Error(409,() => w.Service.VersionCommandAsync(w.School,d.DocumentId,v.DocumentVersionId,"withdraw",new(1,v.Revision,"Stale caller."),Guid.NewGuid(),w.Actor,default));
        d = (await w.Service.VersionCommandAsync(w.School,d.DocumentId,v.DocumentVersionId,"withdraw",new(d.Revision,v.Revision,"Withdraw."),Guid.NewGuid(),w.Actor,default)).Value;
        Assert.Null(d.CurrentVersionId); Assert.Equal("Withdrawn",d.Versions[0].State);
        await Error(422,() => w.Service.VersionCommandAsync(w.School,d.DocumentId,v.DocumentVersionId,"select",new(d.Revision,d.Versions[0].Revision,"No resurrection."),Guid.NewGuid(),w.Actor,default));
    }
    [Fact] public async Task Generation_replay_and_scope_version_mismatches_are_explicit()
    {
        var w = new World(); var t = await w.Template(); var op = Guid.NewGuid(); var request = new GenerateTrainingDocumentRequest(w.Owners.Evidence.SourceSnapshotVersion);
        var j = await w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",request,op,w.Actor,default);
        Assert.True((await w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",request,op,w.Actor,default)).Replay);
        w.Owners.Evidence = w.Owners.Evidence with { EnrollmentId = Guid.NewGuid() };
        await Error(409,() => w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",request,Guid.NewGuid(),w.Actor,default));
        Assert.Equal("Pending",(await w.Service.JobAsync(w.School,j.Value.JobId,w.Actor,default)).Status);
        Assert.NotEqual(Guid.Empty,t.TemplateId);
    }
    [Fact] public async Task Renderer_failure_retries_same_job_then_completes_without_duplicate_root()
    {
        var w = new World(); await w.Template(); var j = (await w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",new(w.Owners.Evidence.SourceSnapshotVersion),Guid.NewGuid(),w.Actor,default)).Value;
        w.Renderer.Fail = true; await w.Service.ProcessJobAsync(w.School,j.JobId,default); Assert.Equal("Pending",(await w.Service.JobAsync(w.School,j.JobId,w.Actor,default)).Status);
        await w.Service.ProcessJobAsync(w.School,j.JobId,default); // not due
        w.Renderer.Fail = false; w.Clock.Time = w.Clock.Time.AddMinutes(1); w.Actor = await w.Owners.ReadmitAsync(w.Actor,default);
        await w.Service.ProcessJobAsync(w.School,j.JobId,default); await w.Service.ProcessJobAsync(w.School,j.JobId,default);
        Assert.Equal("Completed",(await w.Service.JobAsync(w.School,j.JobId,w.Actor,default)).Status); Assert.Single(await w.Service.DocumentsAsync(w.School,w.Enrollment,w.Actor,default));
    }
    [Fact] public async Task Unknown_template_and_stale_evidence_prevent_generation()
    {
        var w = new World(); await Error(422,() => w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",new(w.Owners.Evidence.SourceSnapshotVersion),Guid.NewGuid(),w.Actor,default));
        await w.Template(); await w.Template(); await Error(422,() => w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",new(w.Owners.Evidence.SourceSnapshotVersion),Guid.NewGuid(),w.Actor,default));
        w.Owners.Evidence = w.Owners.Evidence with { SourcesVerified = false }; await Error(409,() => w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",new(w.Owners.Evidence.SourceSnapshotVersion),Guid.NewGuid(),w.Actor,default));
    }
    [Fact] public async Task Contracts_keep_signed_copy_history_and_replacement_does_not_cancel_predecessor()
    {
        var w = new World(); var t = await w.Template("TrainingContract"); var key = Guid.NewGuid(); var request = new GenerateContractRequest(w.Enrollment,t.TemplateId);
        var j = (await w.Service.GenerateContractAsync(w.School,request,key,w.Actor,default)).Value; Assert.True((await w.Service.GenerateContractAsync(w.School,request,key,w.Actor,default)).Replay);
        await w.Service.ProcessJobAsync(w.School,j.JobId,default); var job = await w.Service.JobAsync(w.School,j.JobId,w.Actor,default); var c = await w.Service.ContractAsync(w.School,job.DocumentId!.Value,w.Actor,default);
        var f = (await w.Service.UploadAsync(w.School,"SignedCopy","%PDF-1.7 signed fixture"u8.ToArray(),Guid.NewGuid(),w.Actor,default)).Value;
        c = (await w.Service.AttachAsync(w.School,c.ContractId,new(f.FileId,c.Revision,w.Clock.Time,"Declared external signing."),Guid.NewGuid(),w.Actor,default)).Value;
        c = (await w.Service.AttachAsync(w.School,c.ContractId,new(f.FileId,c.Revision,w.Clock.Time,"Corrected copy."),Guid.NewGuid(),w.Actor,default)).Value;
        Assert.Equal(2,c.SignedCopies.Length); Assert.False(c.SignedCopies[0].IsCurrent); Assert.True(c.SignedCopies[1].IsCurrent);
        var replaced = (await w.Service.GenerateContractAsync(w.School,request with { ReplacesContractId = c.ContractId },Guid.NewGuid(),w.Actor,default)).Value;
        await w.Service.ProcessJobAsync(w.School,replaced.JobId,default); Assert.Equal("Signed",(await w.Service.ContractAsync(w.School,c.ContractId,w.Actor,default)).Status);
        Assert.NotEmpty(await w.Service.HistoryAsync(w.School,c.ContractId,true,w.Actor,default));
        c = (await w.Service.CancelAsync(w.School,c.ContractId,new(c.Revision,"Cancelled by school."),Guid.NewGuid(),w.Actor,default)).Value; Assert.Equal("Cancelled",c.Status);
        await Error(422,() => w.Service.AttachAsync(w.School,c.ContractId,new(f.FileId,c.Revision,w.Clock.Time,"No."),Guid.NewGuid(),w.Actor,default));
        await Error(400,() => w.Service.AttachAsync(w.School,c.ContractId,new(f.FileId,c.Revision,w.Clock.Time.AddDays(1),"Future."),Guid.NewGuid(),w.Actor,default));
    }
    [Fact] public async Task User_activity_and_school_authority_are_checked_again_on_replay()
    {
        var w = new World(); var op = Guid.NewGuid(); await w.Service.CreateTemplateAsync(w.School,new("PracticalJournal","A"),op,w.Actor,default);
        w.Owners.Active = false; await Error(403,() => w.Service.CreateTemplateAsync(w.School,new("PracticalJournal","A"),op,w.Actor,default));
        w.Owners.Active = true; w.Owners.Admin = false; await Error(403,() => w.Service.TemplatesAsync(w.School,w.Actor,default));
        w.Owners.Available = false; await Error(503,() => w.Service.TemplatesAsync(w.School,w.Actor,default));
    }
    [Fact] public async Task Download_rechecks_owner_and_retains_explicit_history_after_correction()
    {
        var w = new World(); var d = await w.Publish(await w.Review(await w.Generate()));
        var url = await w.Service.DownloadAsync(w.School,d.DocumentId,null,false,"pdf",w.Actor,default); var grant = Guid.Parse(new Uri(url.DownloadUrl).Segments[^1]);
        Assert.Equal("application/pdf",(await w.Service.ContentAsync(w.School,grant,w.Actor,default)).MediaType);
        w.Owners.Revision = 2; await Error(409,() => w.Service.ContentAsync(w.School,grant,w.Actor,default)); await w.Service.ReconcileAsync(w.School,w.Enrollment,default);
        var history = await w.Service.DownloadAsync(w.School,d.DocumentId,d.Versions[0].DocumentVersionId,true,"docx",w.Actor,default);
        var content = await w.Service.ContentAsync(w.School,Guid.Parse(new Uri(history.DownloadUrl).Segments[^1]),w.Actor,default); Assert.Contains("history",content.Name);
        await Error(400,() => w.Service.DownloadAsync(w.School,d.DocumentId,null,true,"html",w.Actor,default));
        await Error(404,() => w.Service.ContentAsync(w.School,grant,w.Actor with { UserId = Guid.NewGuid() },default));
    }
    [Fact] public async Task Private_file_corruption_and_expired_grants_are_not_delivered()
    {
        var w = new World(); var d = await w.Publish(await w.Review(await w.Generate())); var g = await w.Service.DownloadAsync(w.School,d.DocumentId,null,false,"pdf",w.Actor,default);
        var grant = Guid.Parse(new Uri(g.DownloadUrl).Segments[^1]);
        foreach (var key in w.Files.Data.Keys.ToArray()) w.Files.Data[key] = [1];
        await Error(503,() => w.Service.ContentAsync(w.School,grant,w.Actor,default),"FILE_INTEGRITY_FAILURE");
        w.Clock.Time = w.Clock.Time.AddMinutes(2); w.Actor = await w.Owners.ReadmitAsync(w.Actor,default); await Error(404,() => w.Service.ContentAsync(w.School,grant,w.Actor,default));
    }
    [Fact] public async Task Students_and_assigned_teachers_cannot_see_unpublished_versions_or_admin_commands()
    {
        var w = new World(); var d = await w.Generate(); w.Owners.Admin = false; w.Owners.Self = true;
        Assert.Empty(await w.Service.DocumentsAsync(w.School,w.Enrollment,w.Actor,default)); await Error(404,() => w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default));
        await Error(403,() => w.Service.VersionCommandAsync(w.School,d.DocumentId,d.Versions[0].DocumentVersionId,"review",new(d.Revision,1,"Not admin."),Guid.NewGuid(),w.Actor,default));
        w.Owners.Admin = true; d = await w.Publish(await w.Review(d)); w.Owners.Admin = false; w.Owners.Self = false; w.Owners.Teacher = true;
        Assert.Single(await w.Service.DocumentsAsync(w.School,w.Enrollment,w.Actor,default)); w.Owners.Teacher = false; await Error(404,() => w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default));
    }
    [Fact] public async Task Inbox_duplicates_and_old_revision_hints_do_not_rewind_or_duplicate_audit()
    {
        var w = new World(); var d = await w.Publish(await w.Review(await w.Generate())); w.Owners.Revision = 3;
        var m = new SourceEvent(Guid.NewGuid(),"learning","TrainingEvidenceInvalidated",1,w.School,w.Enrollment,w.Enrollment,3,"{}","event-correlation",w.Clock.Time);
        await w.Service.ConsumeAsync(m,default); await w.Service.ConsumeAsync(m,default); var current = await w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default); Assert.Null(current.CurrentVersionId);
        await Error(409,() => w.Service.ConsumeAsync(m with { Payload = "changed" },default));
        await w.Service.ConsumeAsync(m with { EventId = Guid.NewGuid(), AggregateVersion = 1 },default); Assert.Equal(current.Revision,(await w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default)).Revision);
        await Error(400,() => w.Service.ConsumeAsync(m with { Producer = "unknown" },default));
    }
    [Fact] public async Task Foreign_school_cannot_read_document_or_files()
    {
        var w = new World(); var d = await w.Generate(); await Error(404,() => w.Service.DocumentAsync(Guid.NewGuid(),d.DocumentId,w.Actor,default));
        await Error(404,() => w.Service.DownloadAsync(Guid.NewGuid(),d.DocumentId,null,false,"pdf",w.Actor,default));
    }
    [Fact] public async Task Signed_copy_links_are_rechecked_after_replacement_and_contract_cancellation()
    {
        var w = new World(); var t = await w.Template("TrainingContract"); var job = (await w.Service.GenerateContractAsync(w.School,new(w.Enrollment,t.TemplateId),Guid.NewGuid(),w.Actor,default)).Value;
        await w.Service.ProcessJobAsync(w.School,job.JobId,default); var j = await w.Service.JobAsync(w.School,job.JobId,w.Actor,default);
        var c = await w.Service.ContractAsync(w.School,j.DocumentId!.Value,w.Actor,default); var file = (await w.Service.UploadAsync(w.School,"SignedCopy","%PDF-1.7 copy"u8.ToArray(),Guid.NewGuid(),w.Actor,default)).Value;
        c = (await w.Service.AttachAsync(w.School,c.ContractId,new(file.FileId,c.Revision,w.Clock.Time,"First external copy."),Guid.NewGuid(),w.Actor,default)).Value;
        var url = await w.Service.DownloadAsync(w.School,c.ContractId,c.SignedCopies[0].SignedCopyId,false,"pdf",w.Actor,default); var grant = Guid.Parse(new Uri(url.DownloadUrl).Segments[^1]);
        Assert.Equal("application/pdf",(await w.Service.ContentAsync(w.School,grant,w.Actor,default)).MediaType);
        c = (await w.Service.AttachAsync(w.School,c.ContractId,new(file.FileId,c.Revision,w.Clock.Time,"Replacement external copy."),Guid.NewGuid(),w.Actor,default)).Value;
        await Error(409,() => w.Service.ContentAsync(w.School,grant,w.Actor,default));
        Assert.Single(await w.Service.ContractsAsync(w.School,w.Enrollment,w.Actor,default));
        var generated = await w.Service.DownloadAsync(w.School,c.ContractId,null,false,"docx",w.Actor,default);
        await w.Service.CancelAsync(w.School,c.ContractId,new(c.Revision,"Cancel."),Guid.NewGuid(),w.Actor,default);
        await Error(422,() => w.Service.ContentAsync(w.School,Guid.Parse(new Uri(generated.DownloadUrl).Segments[^1]),w.Actor,default));
    }
    [Fact] public async Task Every_job_remains_bound_to_its_own_version_and_incomplete_deficits()
    {
        var w = new World(); var t = await w.Template(); w.Owners.Evidence = w.Owners.Evidence with { Status = "Incomplete", Checks = [new("JOURNAL","Student","Unsatisfied","Pending student confirmation.")] };
        var first = (await w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",new(w.Owners.Evidence.SourceSnapshotVersion,t.TemplateId),Guid.NewGuid(),w.Actor,default)).Value;
        Assert.Single(first.MissingItems); await w.Service.ProcessJobAsync(w.School,first.JobId,default);
        var d = await w.Service.DocumentAsync(w.School,(await w.Service.JobAsync(w.School,first.JobId,w.Actor,default)).DocumentId!.Value,w.Actor,default);
        d = await w.Publish(await w.Review(d),"Review");
        await w.Generate(template:t.TemplateId);
        Assert.Equal("PublishedReview",(await w.Service.JobAsync(w.School,first.JobId,w.Actor,default)).DocumentState);
    }
}
