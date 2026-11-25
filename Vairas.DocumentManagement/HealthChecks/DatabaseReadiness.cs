using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Configuration.Options;

namespace Vairas.DocumentManagement.HealthChecks;

public sealed class DatabaseReadiness(IOptions<DatabaseOptions> database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NpgsqlConnection(database.Value.DocumentManagement); await db.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("""
                SELECT NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb AND NOT r.rolcreaterole
                  AND NOT EXISTS(SELECT 1 FROM pg_tables WHERE schemaname='public' AND tableowner=current_user)
                  AND has_table_privilege(current_user,'public.document_template','SELECT,INSERT')
                  AND NOT has_table_privilege(current_user,'public.state_event','UPDATE,DELETE')
                  AND NOT has_schema_privilege(current_user,'public','CREATE')
                  AND (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND rowsecurity AND tablename IN ('training_document','document_version','file_object','operation_receipt','document_job'))=5
                FROM pg_roles r WHERE r.rolname=current_user
                """,db);
            return await command.ExecuteScalarAsync(cancellationToken) is true ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Restricted runtime grants or schema are missing.");
        }
        catch (Exception e) when (e is NpgsqlException or InvalidOperationException or ArgumentException) { return HealthCheckResult.Unhealthy("Database unavailable or incorrectly provisioned."); }
    }
}
