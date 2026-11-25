using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

/// <summary>Fresh School-owned authority and enrollment projection.</summary>
public sealed record DocumentAuthority(Guid SchoolId, Guid ActorUserId, Guid? EnrollmentId, Guid? StudentId, string SchoolName, string Category,
    string EnrollmentStatus, long SchoolRevision, long EnrollmentRevision, bool Administrator, bool Self, bool AssignedTeacher, DateTimeOffset ValidUntil);
