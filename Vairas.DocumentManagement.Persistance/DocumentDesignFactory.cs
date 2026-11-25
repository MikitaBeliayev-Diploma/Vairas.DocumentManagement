using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Vairas.DocumentManagement.Persistance;

public sealed class DocumentDesignFactory : IDesignTimeDbContextFactory<DocumentDbContext>
{
    public DocumentDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(
        Environment.GetEnvironmentVariable("VAIRAS_MIGRATION_CONNECTION") ?? "Host=localhost;Database=vairas_documents_design;Username=design_only").Options);
}
