using Microsoft.EntityFrameworkCore;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Persistance;

public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        foreach (var type in typeof(ISchoolRow).Assembly.GetTypes().Where(t => t.IsClass && typeof(ISchoolRow).IsAssignableFrom(t)))
        {
            var entity = model.Entity(type);
            var canonical = CanonicalMappings.Tables.GetValueOrDefault(type.Name);
            entity.ToTable(canonical.Table ?? Snake(type.Name));
            entity.HasKey(nameof(ISchoolRow.Id));
            foreach (var property in type.GetProperties())
            {
                var p = entity.Property(property.Name).HasColumnName(property.Name == "Id" ? canonical.Key ?? "id" : Snake(property.Name));
                if (property.Name == "Id") p.ValueGeneratedNever();
            }
        }
        model.Entity<EvidenceCheckpoint>().HasKey(x => new { x.SchoolId, x.Id });
        model.Entity<OperationOutcome>().HasKey(x => new { x.SchoolId, x.Id });
    }
    public static string Snake(string s) => System.Text.RegularExpressions.Regex.Replace(s, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
