namespace Vairas.DocumentManagement.Domain;

public sealed class DocumentVersion : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public Guid DocumentId {get;set;}
 public Guid EnrollmentId {get;set;}
 public int VersionNo {get;set;}
 public Guid TemplateVersionId {get;set;}
 public Guid EvidenceSnapshotId {get;set;}
 public long DependencyRevision {get;set;}
 public string EvidenceStatus {get;set;} = "";
 public Guid RenderedFileId {get;set;}
 public Guid BasisFileId {get;set;}
 public string BasisSchemaVersion {get;set;} = "";
 public string RendererVersion {get;set;} = "";
 public string State {get;set;} = "";
 public long Revision {get;set;}
 public Guid GeneratedBy {get;set;}
 public DateTimeOffset GeneratedAt {get;set;}
}
