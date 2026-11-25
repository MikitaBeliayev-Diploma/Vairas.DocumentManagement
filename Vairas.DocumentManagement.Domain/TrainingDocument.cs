namespace Vairas.DocumentManagement.Domain;

public sealed class TrainingDocument : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public Guid EnrollmentId {get;set;}
 public string Kind {get;set;} = "";
 public long Revision {get;set;}
 public Guid? CurrentVersionId {get;set;}
}
