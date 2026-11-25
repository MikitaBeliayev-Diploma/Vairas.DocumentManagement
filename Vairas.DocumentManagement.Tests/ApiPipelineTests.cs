using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

/// <summary>
/// Real ASP.NET Core pipeline (Program, Configure*, JWT validation, authorization, controllers, exception handling, rate limiter,
/// OpenAPI, health) against PostgreSQL with the restricted runtime role. Only the outbound owner calls, blob storage, virus scanner and
/// DOCX renderer are replaced by deterministic test doubles. Covers US-015/US-016 API behaviour and NFR-SEC-02 (identity/permission).
/// </summary>
[Collection("Postgres"), Trait("Category", "Postgres")]
public sealed class ApiPipelineTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Issuer = "https://issuer.example.test";
    private readonly RSA signing = RSA.Create(2048);
    private readonly TestClock clock = new();
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient api = null!;
    private readonly Guid user = Guid.NewGuid(), session = Guid.NewGuid(), school = Guid.NewGuid();

    public Task InitializeAsync()
    {
        // Owner authority is valid until the clock plus 30 s; keep it ahead of the real clock the pipeline runs on.
        clock.Time = DateTimeOffset.UtcNow.AddHours(1);
        var owners = new TestOwners(clock) { Evidence = null! };
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Development");
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DocumentManagement"] = fixture.Runtime,
                ["ConnectionStrings:Delivery"] = fixture.Delivery,
                ["Blob:ConnectionString"] = "UseDevelopmentStorage=true",
                ["Documents:WorkersEnabled"] = "false",
                ["Dependencies:ClientSecret"] = Guid.NewGuid().ToString("N"),
                ["Authentication:Issuer"] = Issuer,
                ["Authentication:Authority"] = Issuer
            }));
            host.ConfigureServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>("User", options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters.IssuerSigningKey = new RsaSecurityKey(signing.ExportParameters(false)) { KeyId = "test" };
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration { Issuer = Issuer });
                });
                services.RemoveAll<IDependencyTransport>(); services.AddScoped<IDependencyTransport>(_ => new FakeTransport(user, session));
                services.RemoveAll<IDocumentOwners>(); services.AddSingleton<IDocumentOwners>(owners);
                services.RemoveAll<IPrivateFiles>(); services.AddSingleton<IPrivateFiles>(new TestFiles());
                services.RemoveAll<IFileScanner>(); services.AddSingleton<IFileScanner>(new TestScanner());
                services.RemoveAll<IDocumentRenderer>(); services.AddSingleton<IDocumentRenderer>(new TestRenderer());
            });
        });
        api = factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() { api.Dispose(); await factory.DisposeAsync(); signing.Dispose(); }

    private string Token(Guid? sub = null, string audience = "vairas-document-management", DateTimeOffset? expires = null, RSA? key = null)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey((key ?? signing).ExportParameters(true)) { KeyId = "test" }, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(Issuer, audience, [new Claim("sub", (sub ?? user).ToString("D")), new Claim("sid", session.ToString("D"))],
            (expires ?? DateTimeOffset.UtcNow.AddMinutes(5)).UtcDateTime.AddMinutes(-6), (expires ?? DateTimeOffset.UtcNow.AddMinutes(5)).UtcDateTime, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpRequestMessage Request(HttpMethod method, string path, object? body = null, string? token = null, Guid? key = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key.Value.ToString("D"));
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Health_and_openapi_are_served_and_ready_requires_the_restricted_runtime_role()
    {
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/health/ready")).StatusCode);
        using var openapi = await api.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openapi.StatusCode);
        var paths = (await Json(openapi)).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/schools/{schoolId}/document-templates", out _));
        Assert.True(paths.EnumerateObject().Count() >= 20);
    }

    [Fact]
    public async Task Missing_invalid_expired_foreign_audience_and_foreign_key_tokens_are_denied_with_a_problem()
    {
        var path = $"/api/v1/schools/{school}/document-templates";
        using var anonymous = await api.SendAsync(Request(HttpMethod.Get, path));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("AUTHENTICATION_REQUIRED", (await Json(anonymous)).GetProperty("code").GetString());
        Assert.Equal("Bearer", anonymous.Headers.WwwAuthenticate.Single().Scheme);
        using var foreign = RSA.Create(2048);
        foreach (var token in new[] { "not-a-jwt", Token(expires: DateTimeOffset.UtcNow.AddMinutes(-10)), Token(audience: "another-service"), Token(key: foreign) })
        {
            using var response = await api.SendAsync(Request(HttpMethod.Get, path, token: token));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        // Document Management exposes no workload-only routes: an internal path is simply unknown, never served to a user token.
        using var internalRoute = await api.SendAsync(Request(HttpMethod.Get, "/internal/v1/anything", token: Token()));
        Assert.Equal(HttpStatusCode.NotFound, internalRoute.StatusCode);
    }

    [Fact]
    public async Task Template_lifecycle_runs_through_controller_service_and_row_level_security_with_idempotent_replay()
    {
        var token = Token();
        var key = Guid.NewGuid();
        using var created = await api.SendAsync(Request(HttpMethod.Post, $"/api/v1/schools/{school}/document-templates", new { kind = "PracticalJournal", name = "API template" }, token, key));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var template = await Json(created); var id = template.GetProperty("templateId").GetGuid();
        Assert.Equal(school, template.GetProperty("schoolId").GetGuid());
        using var replay = await api.SendAsync(Request(HttpMethod.Post, $"/api/v1/schools/{school}/document-templates", new { kind = "PracticalJournal", name = "API template" }, token, key));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(id, (await Json(replay)).GetProperty("templateId").GetGuid());
        using var changed = await api.SendAsync(Request(HttpMethod.Post, $"/api/v1/schools/{school}/document-templates", new { kind = "PracticalJournal", name = "Different" }, token, key));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);

        using var listed = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/schools/{school}/document-templates", token: token));
        Assert.Contains((await Json(listed)).EnumerateArray(), item => item.GetProperty("templateId").GetGuid() == id);
        using var read = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/schools/{school}/document-templates/{id}", token: token));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        // Another school's scope never sees it (row-level security under the runtime role).
        using var otherSchool = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/schools/{Guid.NewGuid()}/document-templates/{id}", token: token));
        Assert.Equal(HttpStatusCode.NotFound, otherSchool.StatusCode);
        using var rename = await api.SendAsync(Request(HttpMethod.Put, $"/api/v1/schools/{school}/document-templates/{id}", new { name = "Renamed", expectedRevision = template.GetProperty("revision").GetInt64() }, token, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal("Renamed", (await Json(rename)).GetProperty("name").GetString());
        using var stale = await api.SendAsync(Request(HttpMethod.Put, $"/api/v1/schools/{school}/document-templates/{id}", new { name = "Stale", expectedRevision = template.GetProperty("revision").GetInt64() }, token, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Validation_and_routing_failures_are_safe_problem_responses()
    {
        var token = Token(); var path = $"/api/v1/schools/{school}/document-templates";
        using var noKey = await api.SendAsync(Request(HttpMethod.Post, path, new { kind = "PracticalJournal", name = "x" }, token));
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", (await Json(noKey)).GetProperty("code").GetString());
        using var unknownField = await api.SendAsync(Request(HttpMethod.Post, path, new { kind = "PracticalJournal", name = "x", extra = 1 }, token, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        using var badKind = await api.SendAsync(Request(HttpMethod.Post, path, new { kind = "Passport", name = "x" }, token, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, badKind.StatusCode);
        using var unknownTemplate = await api.SendAsync(Request(HttpMethod.Get, $"{path}/{Guid.NewGuid()}", token: token));
        Assert.Equal(HttpStatusCode.NotFound, unknownTemplate.StatusCode);
        using var unknownRoute = await api.SendAsync(Request(HttpMethod.Get, "/api/v1/nothing", token: token));
        Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);
        Assert.Equal("HTTP_ERROR", (await Json(unknownRoute)).GetProperty("code").GetString());
        Assert.NotNull(unknownRoute.Headers.GetValues("X-Correlation-ID").FirstOrDefault());
        using var malformed = await api.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/documents/not-a-guid/history?schoolId={school}") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } });
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task Upload_is_validated_scanned_and_returned_without_storage_keys()
    {
        var token = Token();
        using var form = new MultipartFormDataContent { { new ByteArrayContent("DOCX fixture"u8.ToArray()), "file", "template.docx" } };
        var request = Request(HttpMethod.Post, $"/api/v1/schools/{school}/document-files?purpose=Template", null, token, Guid.NewGuid()); request.Content = form;
        using var uploaded = await api.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var file = await Json(uploaded);
        Assert.Equal("Clean", file.GetProperty("scanStatus").GetString());
        Assert.Equal(64, file.GetProperty("sha256").GetString()!.Length);
        Assert.DoesNotContain("blob", file.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var status = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/schools/{school}/document-files/{file.GetProperty("fileId").GetGuid()}", token: token));
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var empty = new MultipartFormDataContent { { new ByteArrayContent([]), "file", "empty.docx" } };
        var emptyRequest = Request(HttpMethod.Post, $"/api/v1/schools/{school}/document-files?purpose=Template", null, token, Guid.NewGuid()); emptyRequest.Content = empty;
        using var rejected = await api.SendAsync(emptyRequest);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task Training_documents_for_an_unknown_enrollment_are_empty_and_downloads_need_a_current_grant()
    {
        var token = Token(); var enrollment = Guid.NewGuid();
        using var list = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/enrollments/{enrollment}/documents?schoolId={school}", token: token));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Empty((await Json(list)).EnumerateArray());
        using var contracts = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/enrollments/{enrollment}/contracts?schoolId={school}", token: token));
        Assert.Equal(HttpStatusCode.OK, contracts.StatusCode);
        using var missingDocument = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/documents/{Guid.NewGuid()}/download?schoolId={school}", token: token));
        Assert.Equal(HttpStatusCode.NotFound, missingDocument.StatusCode);
        using var missingGrant = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/document-downloads/{Guid.NewGuid()}?schoolId={school}", token: token));
        Assert.Equal(HttpStatusCode.NotFound, missingGrant.StatusCode);
        using var job = await api.SendAsync(Request(HttpMethod.Get, $"/api/v1/document-jobs/{Guid.NewGuid()}?schoolId={school}", token: token));
        Assert.Equal(HttpStatusCode.NotFound, job.StatusCode);
    }

    /// <summary>Answers the two owner calls the actor factory makes with a consistent, active identity.</summary>
    private sealed class FakeTransport(Guid user, Guid session) : IDependencyTransport
    {
        public Task<T> SendAsync<T>(string origin, string route, object? body, string? userToken, CancellationToken ct)
        {
            object payload = route switch
            {
                "internal/v1/sessions/validate" => new { active = true, identityId = user, sessionId = session, validUntil = DateTimeOffset.UtcNow.AddMinutes(5) },
                "api/v1/users/me" => new { userId = user, status = "Active", studentId = (Guid?)null, teacherId = (Guid?)null },
                _ => throw DocumentErrors.Dependency()
            };
            return Task.FromResult(JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(payload), new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        }
    }
}
