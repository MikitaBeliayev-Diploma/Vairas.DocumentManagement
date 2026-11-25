using System.Security.Cryptography;
using System.Text;

namespace Vairas.DocumentManagement.Domain;

public sealed class InboxMessage : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset ProcessedAt { get; set; }
}
