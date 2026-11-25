using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vairas.DocumentManagement.Persistance;
using Vairas.DocumentManagement.Persistance.Migrations;

namespace Vairas.DocumentManagement.Startup;

/// <summary>`--migrate` and `--grant-runtime`: run as the migration role, then exit.</summary>
public static class DatabaseCommands
{
    public static async Task RunAsync(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("VAIRAS_MIGRATION_CONNECTION") ?? throw new InvalidOperationException("Supply VAIRAS_MIGRATION_CONNECTION externally.");
        if (args.Contains("--migrate"))
        { await using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(connection).Options); await db.Database.MigrateAsync(); }
        if (args.Contains("--grant-runtime"))
        { await using var db = new NpgsqlConnection(connection); await db.OpenAsync(); await using var command = new NpgsqlCommand(Sql.Read("runtime-grants.sql"),db); await command.ExecuteNonQueryAsync(); }
    }
}
