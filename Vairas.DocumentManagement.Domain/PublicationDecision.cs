using System.Security.Cryptography;
using System.Text;

namespace Vairas.DocumentManagement.Domain;

public sealed class PublicationDecision : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public Guid ActorId { get; set; }
    public Guid IdentityId { get; set; }
    public Guid SessionId { get; set; }
    public long ExpectedRevision { get; set; }
    public long ExpectedVersionRevision { get; set; }
    public string ArtifactDigest { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
