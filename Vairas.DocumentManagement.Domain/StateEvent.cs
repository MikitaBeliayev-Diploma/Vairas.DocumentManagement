namespace Vairas.DocumentManagement.Domain;

public sealed class StateEvent : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public Guid? TemplateId {get;set;}
 public Guid? TemplateVersionId {get;set;}
 public Guid? SignedCopyId {get;set;}
 public string Action {get;set;} = "";
 public Guid? ContractId {get;set;}
 public Guid? DocumentId {get;set;}
 public Guid? DocumentVersionId {get;set;}
 public long SequenceNo {get;set;}
 public string? FromState {get;set;}
 public string ToState {get;set;} = "";
 public Guid ActorId {get;set;}
 public DateTimeOffset OccurredAt {get;set;}
 public string Reason {get;set;} = "";
 public Guid CommandId {get;set;}
}
