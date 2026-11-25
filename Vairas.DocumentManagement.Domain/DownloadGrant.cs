using System.Security.Cryptography;
using System.Text;

namespace Vairas.DocumentManagement.Domain;

public sealed class DownloadGrant : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ActorId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid FileId { get; set; }
    public Guid? VersionId { get; set; }
    public Guid? SignedCopyId { get; set; }
    public bool Historical { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
