using System.Net;
using System.Net.Http.Json;
using Vairas.DocumentManagement.Business;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

[Collection("Postgres"), Trait("Category", "Postgres")]
public sealed class HttpPostgresTests(PostgresFixture fixture)
{
    private static HttpClient Client(DocumentApiFactory host)
    {
        var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-document-test");
        return client;
    }

    [Fact]
    public async Task Template_HTTP_commands_preserve_idempotency_validation_and_revision_conflicts()
    {
        var world = new World(fixture.Store);
        await using var host = new DocumentApiFactory(world);
        using var client = Client(host);
        var path = $"/api/v1/schools/{world.School}/document-templates";
        var operation = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", operation);
        var input = new CreateTemplateRequest("PracticalJournal", "HTTP template");
        using var created = await client.PostAsJsonAsync(path, input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var template = (await created.Content.ReadFromJsonAsync<TemplateResponse>())!;
        Assert.NotNull(created.Headers.Location);
        using var replay = await client.PostAsJsonAsync(path, input);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(template, (await replay.Content.ReadFromJsonAsync<TemplateResponse>())! with { Versions = template.Versions });
        using var reused = await client.PostAsJsonAsync(path, input with { Name = "Different payload" });
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        using var missingKey = await client.PostAsJsonAsync(path, input);
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var renamed = await client.PutAsJsonAsync(path + "/" + template.TemplateId, new RenameTemplateRequest("Renamed", template.Revision));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var stale = await client.PutAsJsonAsync(path + "/" + template.TemplateId, new RenameTemplateRequest("Stale", template.Revision));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var items = await client.GetFromJsonAsync<TemplateResponse[]>(path);
        Assert.Equal("Renamed", Assert.Single(items!).Name);
        Assert.Equal("Renamed", (await client.GetFromJsonAsync<TemplateResponse>(path + "/" + template.TemplateId))!.Name);
    }

    [Fact]
    public async Task Prepared_document_HTTP_review_publication_history_and_download_use_real_persistence()
    {
        var world = new World(fixture.Store);
        var document = await world.Generate();
        await using var host = new DocumentApiFactory(world);
        using var client = Client(host);
        var path = $"/api/v1/documents/{document.DocumentId}";
        var scope = $"?schoolId={world.School}";
        var version = Assert.Single(document.Versions);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var reviewed = await client.PostAsJsonAsync(path + $"/versions/{version.DocumentVersionId}/review" + scope,
            new VersionCommandRequest(document.Revision, version.Revision, "HTTP administrator review"));
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        document = (await reviewed.Content.ReadFromJsonAsync<DocumentResponse>())!;
        version = Assert.Single(document.Versions);
        Assert.True(version.Reviewed);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var published = await client.PostAsJsonAsync(path + $"/versions/{version.DocumentVersionId}/publish" + scope,
            new PublishRequest(document.Revision, version.Revision, "Review", "HTTP review-only publication"));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        document = (await published.Content.ReadFromJsonAsync<DocumentResponse>())!;
        Assert.Equal("PublishedReview", Assert.Single(document.Versions).State);
        var listed = await client.GetFromJsonAsync<DocumentResponse[]>($"/api/v1/enrollments/{world.Enrollment}/documents" + scope);
        Assert.Equal(document.DocumentId, Assert.Single(listed!).DocumentId);
        var history = await client.GetFromJsonAsync<HistoryResponse[]>(path + "/history" + scope);
        Assert.True(history!.Length >= 3);
        var download = (await client.GetFromJsonAsync<DocumentDownloadResponse>(path + "/download" + scope))!;
        using var content = await client.GetAsync(download.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("application/pdf", content.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", Assert.Single(content.Headers.GetValues("X-Content-Type-Options")));
        Assert.StartsWith("%PDF", await content.Content.ReadAsStringAsync());
        using var foreign = await client.GetAsync(path + $"?schoolId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await client.GetAsync(path + scope);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
