using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Middleware;
using Vairas.DocumentManagement.Persistance;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class ExceptionHandlingTests
{
    private static PostgresException Postgres(string sqlState, string? constraint = null) => new("synthetic", "ERROR", "ERROR", sqlState, constraintName: constraint);
    private static DbUpdateException Update(string sqlState, string? constraint = null) => new("synthetic", Postgres(sqlState, constraint));

    [Fact]
    public void Optimistic_concurrency_becomes_a_revision_conflict()
    {
        var translated = Assert.IsType<ConflictException>(DatabaseExceptionTranslator.Translate(new DbUpdateConcurrencyException()));
        Assert.Equal("REVISION_CONFLICT", translated.Code);
    }

    [Theory, InlineData(PostgresErrorCodes.UniqueViolation, "dm_one_active_template")]
    public void Demonstrated_constraint_conflicts_keep_the_original(string state, string constraint)
    {
        var original = Update(state, constraint);
        var translated = Assert.IsType<ConflictException>(DatabaseExceptionTranslator.Translate(original));
        Assert.Equal("RELATIONSHIP_CONFLICT", translated.Code);
        Assert.Same(original, translated.InnerException);
    }

    [Theory, InlineData(PostgresErrorCodes.SerializationFailure), InlineData(PostgresErrorCodes.DeadlockDetected), InlineData(PostgresErrorCodes.LockNotAvailable)]
    public void Transient_transaction_failures_ask_for_a_retry(string state)
    {
        var translated = Assert.IsType<ServiceUnavailableException>(DatabaseExceptionTranslator.Translate(Postgres(state)));
        Assert.Equal("RETRY_REQUIRED", translated.Code);
    }

    [Theory, InlineData(PostgresErrorCodes.UniqueViolation), InlineData(PostgresErrorCodes.ExclusionViolation), InlineData(PostgresErrorCodes.ForeignKeyViolation), InlineData(PostgresErrorCodes.CheckViolation)]
    public void Unrecognised_integrity_violations_are_implementation_defects_not_business_conflicts(string state)
    {
        var failure = Assert.IsType<PersistenceFailureException>(DatabaseExceptionTranslator.Translate(Update(state, "synthetic_check")));
        Assert.Equal((state, "synthetic_check"), (failure.SqlState, failure.Constraint));
    }

    [Fact]
    public void Business_exceptions_and_cancellation_pass_through_untranslated()
    {
        Assert.Null(DatabaseExceptionTranslator.Translate(new NotFoundException("NOT_FOUND")));
        Assert.Null(DatabaseExceptionTranslator.Translate(new OperationCanceledException()));
        Assert.Null(DatabaseExceptionTranslator.Translate(new InvalidOperationException()));
    }

    private static async Task<(int Status, string Body)> Handle(Exception exception, CancellationToken aborted = default)
    {
        var http = new DefaultHttpContext { RequestAborted = aborted };
        http.Response.Body = new MemoryStream();
        await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance).TryHandleAsync(http, exception, default);
        http.Response.Body.Position = 0;
        return (http.Response.StatusCode, await new StreamReader(http.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task Each_application_category_maps_to_its_http_status_with_the_existing_code()
    {
        (BusinessException Exception, int Status)[] items =
        [
            (new BadRequestException("VALIDATION_FAILED"), 400), (new UnauthorizedException("SESSION_INVALID"), 401), (new ForbiddenException("ACCESS_DENIED"), 403),
            (new NotFoundException("NOT_FOUND"), 404), (new ConflictException("REVISION_CONFLICT"), 409), (new UnprocessableEntityException("DOCUMENT_RULE"), 422),
            (new ServiceUnavailableException("DEPENDENCY_UNAVAILABLE"), 503)
        ];
        foreach (var item in items)
        {
            var (status, body) = await Handle(item.Exception);
            Assert.Equal(item.Status, status);
            Assert.Contains("\"code\":\"" + item.Exception.Code + "\"", body);
        }
    }

    [Fact]
    public async Task Unexpected_failures_are_a_generic_500_without_internal_details()
    {
        var (status, body) = await Handle(new PersistenceFailureException("23514", "synthetic_check", new InvalidOperationException("secret-connection-detail")));
        Assert.Equal(500, status);
        Assert.Contains("INTERNAL_ERROR", body);
        Assert.DoesNotContain("secret-connection-detail", body);
        Assert.DoesNotContain("synthetic_check", body);
        Assert.DoesNotContain("23514", body);
    }

    [Fact]
    public async Task A_canceled_request_is_not_reported_as_a_server_error()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var (status, body) = await Handle(new OperationCanceledException(), source.Token);
        Assert.Empty(body);
        Assert.NotEqual(500, status);
    }
}
