using System.Security.Cryptography;
using System.Text;

namespace Vairas.DocumentManagement.Domain;

public sealed class OperationOutcome : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string ResponseJson { get; set; } = "";
}
