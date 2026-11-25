using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Owner-calculated missing or unverified requirement.</summary>
public sealed record EvidenceCheck(string Code, string Field, string Outcome, string Explanation);
