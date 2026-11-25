using System.Security.Cryptography;
using System.Text;

namespace Vairas.DocumentManagement.Domain;

public sealed class DocumentReview : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
    public string Reason { get; set; } = "";
}
