using System.Security.Cryptography;
using System.Text;

namespace Vairas.DocumentManagement.Domain;

public sealed class DocumentJob : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ActorId { get; set; }
    public Guid IdentityId { get; set; }
    public Guid SessionId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public Guid? SnapshotId { get; set; }
    public Guid? ReplacesContractId { get; set; }
    public string SourceSnapshotVersion { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public Guid? DocumentId { get; set; }
    public Guid? ResultVersionId { get; set; }
    public string MissingItemsJson { get; set; } = "[]";
    public string? ErrorCode { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
