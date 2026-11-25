using System.Text;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Business;

public interface IDocumentService
{
    Task<TemplateResponse[]> TemplatesAsync(Guid school, DocumentActor actor, CancellationToken ct);
    Task<TemplateResponse> TemplateAsync(Guid school, Guid template, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<TemplateResponse>> CreateTemplateAsync(Guid school, CreateTemplateRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<TemplateResponse>> RenameTemplateAsync(Guid school, Guid template, RenameTemplateRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<TemplateResponse>> AddVersionAsync(Guid school, Guid template, Guid file, long revision, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<TemplateResponse>> TemplateLifecycleAsync(Guid school, Guid template, Guid version, string action, RevisionRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<FileResponse>> UploadAsync(Guid school, string purpose, byte[] bytes, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<FileResponse> FileAsync(Guid school, Guid file, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<DocumentJobResponse>> GenerateContractAsync(Guid school, GenerateContractRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<DocumentJobResponse>> GenerateAsync(Guid school, Guid enrollment, string kind, GenerateTrainingDocumentRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<DocumentJobResponse> JobAsync(Guid school, Guid job, DocumentActor actor, CancellationToken ct);
    Task<ContractResponse> ContractAsync(Guid school, Guid contract, DocumentActor actor, CancellationToken ct);
    Task<ContractResponse[]> ContractsAsync(Guid school, Guid enrollment, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<ContractResponse>> AttachAsync(Guid school, Guid contract, AttachSignedCopyRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<ContractResponse>> CancelAsync(Guid school, Guid contract, RevisionRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<DocumentResponse[]> DocumentsAsync(Guid school, Guid enrollment, DocumentActor actor, CancellationToken ct);
    Task<DocumentResponse> DocumentAsync(Guid school, Guid document, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<DocumentResponse>> VersionCommandAsync(Guid school, Guid document, Guid version, string action, VersionCommandRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<CommandResult<DocumentResponse>> PublishAsync(Guid school, Guid document, Guid version, PublishRequest request, Guid operation, DocumentActor actor, CancellationToken ct);
    Task<HistoryResponse[]> HistoryAsync(Guid school, Guid document, bool contract, DocumentActor actor, CancellationToken ct);
    Task<DocumentDownloadResponse> DownloadAsync(Guid school, Guid document, Guid? version, bool historical, string format, DocumentActor actor, CancellationToken ct);
    Task<DownloadContent> ContentAsync(Guid school, Guid grant, DocumentActor actor, CancellationToken ct);
}
