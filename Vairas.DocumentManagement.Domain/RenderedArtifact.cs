using System.Security.Cryptography;
using System.Text;

namespace Vairas.DocumentManagement.Domain;

public sealed class RenderedArtifact : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public Guid FileId { get; set; }
    public string Format { get; set; } = "";
}
