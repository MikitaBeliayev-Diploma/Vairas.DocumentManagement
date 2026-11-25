namespace Vairas.DocumentManagement.Domain;

public sealed class DocumentTemplate : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public string Kind {get;set;} = "";
 public string Name {get;set;} = "";
 public Guid? ActiveVersionId {get;set;}
 public long Revision {get;set;}
}
