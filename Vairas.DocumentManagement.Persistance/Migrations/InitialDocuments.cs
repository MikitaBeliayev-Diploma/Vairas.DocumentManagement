using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Vairas.DocumentManagement.Persistance.Migrations;

[DbContext(typeof(DocumentDbContext)), Migration("202611240001_InitialDocuments")]
public sealed class InitialDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(Sql.Read("schema.sql"));
        migrationBuilder.Sql(Sql.Read("postgresql-constraints.sql"));
        migrationBuilder.Sql(Sql.Read("implementation.sql"));
    }
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Retained document history requires a reviewed restore or retention migration.");
}
public static class Sql
{
    public static string Read(string file)
    {
        var assembly = typeof(Sql).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(x => x.EndsWith("." + file, StringComparison.Ordinal)))!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
