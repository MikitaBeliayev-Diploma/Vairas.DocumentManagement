using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Vairas.DocumentManagement.Persistance;
using Vairas.DocumentManagement.Persistance.Migrations;
using Xunit;

namespace Vairas.DocumentManagement.Tests;
[Collection("Postgres"), Trait("Category","Postgres")]
public sealed class PostgresTests(PostgresFixture fixture)
{
    [Fact] public async Task Full_generation_review_publication_and_invalidation_commit_under_runtime_role()
    {
        var w = new World(fixture.Store); var d = await w.Generate(); d = await w.Review(d); d = await w.Publish(d);
        Assert.Equal("PublishedReady",d.Versions[0].State);
        w.Owners.Revision = 2; await w.Service.ReconcileAsync(w.School,w.Enrollment,default);
        d = await w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default); Assert.Equal("Stale",d.Versions[0].State); Assert.Null(d.CurrentVersionId);
    }
    [Fact] public async Task Runtime_cannot_read_foreign_tenant_even_with_omitted_predicate()
    {
        var w = new World(fixture.Store); var t = await w.Template();
        await using var db = new NpgsqlConnection(fixture.Runtime); await db.OpenAsync();
        await using (var query = new NpgsqlCommand("SELECT count(*) FROM document_template",db)) Assert.Equal(0L,await query.ExecuteScalarAsync());
        await using var tx = await db.BeginTransactionAsync(); await using (var scope = new NpgsqlCommand("SELECT set_config('vairas.school_id',@school,true)",db,tx)) { scope.Parameters.AddWithValue("school",Guid.NewGuid().ToString("D")); await scope.ExecuteScalarAsync(); }
        await using (var query = new NpgsqlCommand("SELECT count(*) FROM document_template",db,tx)) Assert.Equal(0L,await query.ExecuteScalarAsync());
        await tx.RollbackAsync(); Assert.NotEqual(Guid.Empty,t.TemplateId);
    }
    [Fact] public async Task Missing_parent_revision_audit_rolls_back_root_change()
    {
        var w = new World(fixture.Store); var d = await w.Generate();
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var db = new NpgsqlConnection(fixture.Runtime); await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("SELECT set_config('vairas.school_id',@school,true); UPDATE training_document SET revision=revision+1 WHERE document_id=@id",db,tx);
            command.Parameters.AddWithValue("school",w.School.ToString("D")); command.Parameters.AddWithValue("id",d.DocumentId); await command.ExecuteNonQueryAsync(); await tx.CommitAsync();
        });
        Assert.Equal(d.Revision,(await w.Service.DocumentAsync(w.School,d.DocumentId,w.Actor,default)).Revision);
    }
    [Fact] public async Task Concurrent_stale_commands_leave_one_parent_revision_and_one_new_child()
    {
        var w = new World(fixture.Store); var t = await w.Template(); var f = (await w.Service.UploadAsync(w.School,"Template","new bytes"u8.ToArray(),Guid.NewGuid(),w.Actor,default)).Value;
        async Task<bool> Add() { try { await w.Service.AddVersionAsync(w.School,t.TemplateId,f.FileId,t.Revision,Guid.NewGuid(),w.Actor,default); return true; } catch (ConflictException) { return false; } }
        var results = await Task.WhenAll(Add(),Add()); Assert.Single(results,x => x);
        var after = await w.Service.TemplateAsync(w.School,t.TemplateId,w.Actor,default); Assert.Equal(t.Revision+1,after.Revision); Assert.Equal(2,after.Versions.Length);
    }
    [Fact] public async Task Contracts_and_signed_copy_replacements_commit_with_exact_audit()
    {
        var w = new World(fixture.Store); var t = await w.Template("TrainingContract"); var job = (await w.Service.GenerateContractAsync(w.School,new(w.Enrollment,t.TemplateId),Guid.NewGuid(),w.Actor,default)).Value;
        await w.Service.ProcessJobAsync(w.School,job.JobId,default); var completed = await w.Service.JobAsync(w.School,job.JobId,w.Actor,default); Assert.Equal("Completed",completed.Status);
        var c = await w.Service.ContractAsync(w.School,completed.DocumentId!.Value,w.Actor,default);
        var f = (await w.Service.UploadAsync(w.School,"SignedCopy","%PDF-1.7 signed fixture"u8.ToArray(),Guid.NewGuid(),w.Actor,default)).Value;
        c = (await w.Service.AttachAsync(w.School,c.ContractId,new(f.FileId,c.Revision,w.Clock.Time.ToOffset(TimeSpan.FromHours(2)),"External copy."),Guid.NewGuid(),w.Actor,default)).Value;
        Assert.Equal(TimeSpan.Zero,c.SignedCopies[0].DeclaredSignedAt.Offset);
        c = (await w.Service.AttachAsync(w.School,c.ContractId,new(f.FileId,c.Revision,w.Clock.Time,"Replacement copy."),Guid.NewGuid(),w.Actor,default)).Value;
        Assert.Equal(2,c.SignedCopies.Length); Assert.Single(c.SignedCopies,x => x.IsCurrent);
    }
    [Fact] public async Task Delivery_role_can_only_enumerate_recovery_identifiers()
    {
        var w = new World(fixture.Store); await w.Template(); var j = (await w.Service.GenerateAsync(w.School,w.Enrollment,"PracticalJournal",new(w.Owners.Evidence.SourceSnapshotVersion),Guid.NewGuid(),w.Actor,default)).Value;
        var catalogue = new DeliveryCatalogue(fixture.Delivery); Assert.Contains(await catalogue.JobsAsync(default),x => x.School == w.School && x.Id == j.JobId);
        await using var db = new NpgsqlConnection(fixture.Delivery); await db.OpenAsync();
        await Assert.ThrowsAsync<PostgresException>(async () => { await using var query = new NpgsqlCommand("SELECT * FROM document_version",db); await query.ExecuteNonQueryAsync(); });
    }
}
