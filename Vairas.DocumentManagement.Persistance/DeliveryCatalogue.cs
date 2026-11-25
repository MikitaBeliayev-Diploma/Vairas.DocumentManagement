using Npgsql;
using Vairas.DocumentManagement.Business;

namespace Vairas.DocumentManagement.Persistance;

public sealed class DeliveryCatalogue(string connection) : IDeliveryCatalogue
{
    private async Task<(Guid, Guid)[]> Read(string function, CancellationToken ct)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT * FROM " + function + "()", db);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<(Guid, Guid)>(); while (await reader.ReadAsync(ct)) rows.Add((reader.GetGuid(0), reader.GetGuid(1)));
        return rows.ToArray();
    }
    public Task<(Guid School, Guid Id)[]> JobsAsync(CancellationToken ct) => Read("dm_pending_jobs", ct);
    public Task<(Guid School, Guid Id)[]> PublicationsAsync(CancellationToken ct) => Read("dm_pending_publications", ct);
    public Task<(Guid School, Guid Enrollment)[]> EnrollmentsAsync(CancellationToken ct) => Read("dm_enrollment_catalogue", ct);
    public Task<(Guid School, Guid Id)[]> FilesAsync(CancellationToken ct) => Read("dm_pending_files", ct);
}
