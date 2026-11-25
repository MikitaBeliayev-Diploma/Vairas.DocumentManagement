namespace Vairas.DocumentManagement.Persistance;
internal static class CanonicalMappings { public static readonly Dictionary<string,(string Table,string Key)> Tables = new() {
["DocumentTemplate"]=("document_template","template_id"),
["TemplateVersion"]=("template_version","template_version_id"),
["FileObject"]=("file_object","file_id"),
["TrainingContract"]=("training_contract","contract_id"),
["ContractSignedCopy"]=("contract_signed_copy","signed_copy_id"),
["TrainingDocument"]=("training_document","document_id"),
["DocumentVersion"]=("document_version","document_version_id"),
["StateEvent"]=("state_event","event_id"),
["OperationReceipt"]=("operation_receipt","command_id"),
["EvidenceCheckpoint"]=("evidence_checkpoint","enrollment_id"),
}; }