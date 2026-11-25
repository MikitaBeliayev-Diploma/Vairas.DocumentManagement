using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Clean or pending uploaded file receipt.</summary>
public sealed record FileResponse(Guid FileId, string ScanStatus, string Sha256, long ByteLength);
