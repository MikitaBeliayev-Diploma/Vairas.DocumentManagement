namespace Vairas.DocumentManagement.Domain;

public sealed class FileObject : ISchoolRow {
 public Guid Id {get;set;}
 public Guid SchoolId {get;set;}
 public string StorageKey {get;set;} = "";
 public string Purpose {get;set;} = "";
 public string Sha256 {get;set;} = "";
 public long ByteLength {get;set;}
 public string MediaType {get;set;} = "";
 public string ScanStatus {get;set;} = "";
 public Guid UploadedBy {get;set;}
 public DateTimeOffset UploadedAt {get;set;}
}
