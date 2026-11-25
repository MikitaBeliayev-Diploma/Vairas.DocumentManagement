namespace Vairas.DocumentManagement.Domain;

public sealed class ContractSignedCopy : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public Guid ContractId {get;set;}
 public int CopyNo {get;set;}
 public Guid FileId {get;set;}
 public bool IsCurrent {get;set;}
 public DateTimeOffset DeclaredSignedAt {get;set;}
 public DateTimeOffset RecordedAt {get;set;}
 public Guid RecordedBy {get;set;}
 public string Basis {get;set;} = "";
}
