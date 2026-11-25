namespace Vairas.DocumentManagement.Domain;

public sealed class EvidenceCheckpoint : ISchoolRow {
 public Guid SchoolId {get;set;}
 public Guid Id {get;set;}
 public long DependencyRevision {get;set;}
 public Guid LastChangeId {get;set;}
 public DateTimeOffset ObservedAt {get;set;}
}
