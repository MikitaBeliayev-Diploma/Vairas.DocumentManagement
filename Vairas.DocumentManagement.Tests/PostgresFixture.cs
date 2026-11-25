using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Vairas.DocumentManagement.Persistance;
using Vairas.DocumentManagement.Persistance.Migrations;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string password = Guid.NewGuid().ToString("N");
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("document_tests").Build();
    public string Runtime { get; private set; } = "";
    public string Migration { get; private set; } = "";
    public string Delivery { get; private set; } = "";
    public DocumentStore Store => new(new ContextFactory(Runtime));
    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var admin = new NpgsqlConnection(container.GetConnectionString()); await admin.OpenAsync();
        var sql = $"CREATE ROLE v_document_management_migration LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS; CREATE ROLE v_document_management_runtime LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS; CREATE ROLE v_document_management_delivery LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS; ALTER DATABASE document_tests OWNER TO v_document_management_migration;";
        await using (var command = new NpgsqlCommand(sql,admin)) await command.ExecuteNonQueryAsync();
        string Connection(string user) { var b = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Username = user, Password = password }; return b.ConnectionString; }
        Migration = Connection("v_document_management_migration"); Runtime = Connection("v_document_management_runtime"); Delivery = Connection("v_document_management_delivery");
        await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(Migration).Options);
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        await using var owner = new NpgsqlConnection(Migration); await owner.OpenAsync(); await using var grants = new NpgsqlCommand(Sql.Read("runtime-grants.sql"),owner); await grants.ExecuteNonQueryAsync();
    }
    public async Task DisposeAsync() { NpgsqlConnection.ClearAllPools(); await container.DisposeAsync(); }
    private sealed class ContextFactory(string connection) : IDbContextFactory<DocumentDbContext>
    { public DocumentDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(connection).Options); }
}
