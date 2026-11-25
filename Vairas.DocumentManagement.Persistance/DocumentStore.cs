using System.Buffers.Binary;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Persistance;

public sealed class DocumentStore(IDbContextFactory<DocumentDbContext> factory) : IDocumentStore
{
    public Task<IDocumentSession> OpenAsync(Guid school, bool write, CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(() => OpenCoreAsync(school, write, ct));
    private async Task<IDocumentSession> OpenCoreAsync(Guid school, bool write, CancellationToken ct)
    {
        if (school == Guid.Empty) throw DocumentErrors.Invalid();
        var db = await factory.CreateDbContextAsync(ct);
        try
        {
            var tx = await db.Database.BeginTransactionAsync(write ? System.Data.IsolationLevel.ReadCommitted : System.Data.IsolationLevel.RepeatableRead, ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('vairas.school_id',{school.ToString("D")},true)", ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='3s'; SET CONSTRAINTS ALL DEFERRED", ct);
            // One local school writer also serializes first-create/checkpoint races. No network I/O under this lock.
            if (write) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BinaryPrimitives.ReadInt64LittleEndian(school.ToByteArray())})", ct);
            else db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return new Session(db, tx, school);
        }
        catch { await db.DisposeAsync(); throw; }
    }
    private sealed class Session(DocumentDbContext db, IDbContextTransaction tx, Guid school) : IDocumentSession
    {
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class, ISchoolRow => DatabaseExceptionTranslator.GuardAsync(() => db.Set<T>().Where(x => x.SchoolId == school).SingleOrDefaultAsync(predicate, ct));
        public async Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class, ISchoolRow => await DatabaseExceptionTranslator.GuardAsync<IReadOnlyList<T>>(async () => await db.Set<T>().Where(x => x.SchoolId == school).Where(predicate).ToArrayAsync(ct));
        public void Add<T>(T row) where T : class, ISchoolRow { if (row.SchoolId != school) throw DocumentErrors.Denied(); db.Add(row); }
        public Task FlushAsync(CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(() => db.SaveChangesAsync(ct));
        public Task CommitAsync(CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(async () => { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); });
        public async ValueTask DisposeAsync() { await tx.DisposeAsync(); await db.DisposeAsync(); }
    }
}
