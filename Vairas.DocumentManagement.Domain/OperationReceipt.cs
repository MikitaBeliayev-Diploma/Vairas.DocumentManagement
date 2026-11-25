namespace Vairas.DocumentManagement.Domain;

public sealed class OperationReceipt : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public Guid ActorId {get;set;}
 public Guid? TemplateId {get;set;}
 public Guid? TemplateVersionId {get;set;}
 public string Operation {get;set;} = "";
 public string RequestDigest {get;set;} = "";
 public string Status {get;set;} = "";
 public Guid? ContractId {get;set;}
 public Guid? DocumentId {get;set;}
 public Guid? DocumentVersionId {get;set;}
 public string? OutcomeCode {get;set;}
 public DateTimeOffset CreatedAt {get;set;}
 public DateTimeOffset? CompletedAt {get;set;}
}
