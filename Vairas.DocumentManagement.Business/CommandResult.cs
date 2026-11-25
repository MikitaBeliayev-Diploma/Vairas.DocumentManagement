using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Stable command result and replay indicator.</summary>
public sealed record CommandResult<T>(T Value, bool Replay);
