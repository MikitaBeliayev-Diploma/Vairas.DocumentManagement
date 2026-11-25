using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Minimal current profile projection with owner revision.</summary>
public sealed record DocumentProfile(Guid UserId, string FirstName, string LastName, long Revision);
