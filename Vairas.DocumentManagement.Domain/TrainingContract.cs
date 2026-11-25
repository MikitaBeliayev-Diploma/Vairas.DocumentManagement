namespace Vairas.DocumentManagement.Domain;

public sealed class TrainingContract : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public Guid EnrollmentId {get;set;}
 public Guid StudentId {get;set;}
 public Guid TemplateVersionId {get;set;}
 public Guid GeneratedFileId {get;set;}
 public Guid BasisFileId {get;set;}
 public string BasisSchemaVersion {get;set;} = "";
 public string Status {get;set;} = "";
 public Guid? ReplacesContractId {get;set;}
 public Guid GeneratedBy {get;set;}
 public DateTimeOffset GeneratedAt {get;set;}
 public long Revision {get;set;}
}
