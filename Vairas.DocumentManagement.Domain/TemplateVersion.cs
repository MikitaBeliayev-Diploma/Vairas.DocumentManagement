namespace Vairas.DocumentManagement.Domain;

public sealed class TemplateVersion : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public Guid TemplateId {get;set;}
 public int VersionNo {get;set;}
 public string Status {get;set;} = "";
 public Guid FileId {get;set;}
 public Guid CreatedBy {get;set;}
 public DateTimeOffset CreatedAt {get;set;}
}
