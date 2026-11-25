using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public sealed record RenderOutput(byte[] Docx, byte[] Pdf, string RendererVersion);
