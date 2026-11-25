using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Controllers;

[Authorize(Policy = "DocumentEntry"), Route("api/v1")]
public sealed class DocumentsController(IDocumentService service, IDocumentActorFactory actors, DocumentOptions options) : BaseApiController
{
    private Task<DocumentActor> Actor(CancellationToken ct) => actors.CreateAsync(HttpContext,ct);
    private Guid Operation() => Guid.TryParse(Request.Headers["Idempotency-Key"],out var id) && id != Guid.Empty ? id : throw DocumentErrors.Invalid("IDEMPOTENCY_KEY_REQUIRED");
    private ActionResult<T> Created<T>(CommandResult<T> r, string location) => r.Replay ? Ok(r.Value) : base.Created(location,r.Value);
    /// <summary>List school-owned templates and their retained versions.</summary>
    [HttpGet("schools/{schoolId:guid}/document-templates",Name = "listDocumentTemplates")]
    public async Task<ActionResult<TemplateResponse[]>> Templates(Guid schoolId, CancellationToken ct) => Ok(await service.TemplatesAsync(schoolId,await Actor(ct),ct));
    /// <summary>Read one school-owned template.</summary>
    [HttpGet("schools/{schoolId:guid}/document-templates/{templateId:guid}",Name = "getDocumentTemplate")]
    public async Task<ActionResult<TemplateResponse>> Template(Guid schoolId, Guid templateId, CancellationToken ct) => Ok(await service.TemplateAsync(schoolId,templateId,await Actor(ct),ct));
    /// <summary>Create a typed template root without an active version.</summary>
    [HttpPost("schools/{schoolId:guid}/document-templates",Name = "createDocumentTemplate"), ProducesResponseType<TemplateResponse>(201)]
    public async Task<ActionResult<TemplateResponse>> CreateTemplate(Guid schoolId, CreateTemplateRequest request, CancellationToken ct)
    { var r = await service.CreateTemplateAsync(schoolId,request,Operation(),await Actor(ct),ct); return Created(r,$"/api/v1/schools/{schoolId:D}/document-templates/{r.Value.TemplateId:D}"); }
    /// <summary>Rename a template under exact parent revision.</summary>
    [HttpPut("schools/{schoolId:guid}/document-templates/{templateId:guid}",Name = "renameDocumentTemplate")]
    public async Task<ActionResult<TemplateResponse>> Rename(Guid schoolId, Guid templateId, RenameTemplateRequest request, CancellationToken ct) => Ok((await service.RenameTemplateAsync(schoolId,templateId,request,Operation(),await Actor(ct),ct)).Value);
    /// <summary>Add an immutable Draft version from a Clean uploaded DOCX.</summary>
    [HttpPost("schools/{schoolId:guid}/document-templates/{templateId:guid}/versions",Name = "addDocumentTemplateVersion"), ProducesResponseType<TemplateResponse>(201)]
    public async Task<ActionResult<TemplateResponse>> AddVersion(Guid schoolId, Guid templateId, AddTemplateVersionRequest request, CancellationToken ct)
    { var r = await service.AddVersionAsync(schoolId,templateId,request.FileId,request.ExpectedRevision,Operation(),await Actor(ct),ct); return Created(r,$"/api/v1/schools/{schoolId:D}/document-templates/{templateId:D}"); }
    /// <summary>Activate an own Draft and archive the previous active template version atomically.</summary>
    [HttpPost("schools/{schoolId:guid}/document-templates/{templateId:guid}/versions/{versionId:guid}/activate",Name = "activateDocumentTemplateVersion")]
    public async Task<ActionResult<TemplateResponse>> Activate(Guid schoolId, Guid templateId, Guid versionId, RevisionRequest request, CancellationToken ct) => Ok((await service.TemplateLifecycleAsync(schoolId,templateId,versionId,"activate",request,Operation(),await Actor(ct),ct)).Value);
    /// <summary>Archive a Draft or Active version without editing its bytes.</summary>
    [HttpPost("schools/{schoolId:guid}/document-templates/{templateId:guid}/versions/{versionId:guid}/archive",Name = "archiveDocumentTemplateVersion")]
    public async Task<ActionResult<TemplateResponse>> Archive(Guid schoolId, Guid templateId, Guid versionId, RevisionRequest request, CancellationToken ct) => Ok((await service.TemplateLifecycleAsync(schoolId,templateId,versionId,"archive",request,Operation(),await Actor(ct),ct)).Value);
    /// <summary>Upload a DOCX Template or PDF SignedCopy; Pending scans cannot be referenced.</summary>
    [HttpPost("schools/{schoolId:guid}/document-files",Name = "uploadDocumentFile"), RequestSizeLimit(21*1024*1024), ProducesResponseType<FileResponse>(201)]
    public async Task<ActionResult<FileResponse>> Upload(Guid schoolId, [FromQuery] string purpose, [FromForm] IFormFile file, CancellationToken ct)
    {
        var actor = await Actor(ct); var operation = Operation();
        if (file.Length is <= 0 || file.Length > options.MaxUploadBytes) throw DocumentErrors.Invalid("INVALID_UPLOAD_SIZE");
        using var bytes = new MemoryStream(); await using var input = file.OpenReadStream(); var buffer = new byte[65536]; int count;
        while ((count = await input.ReadAsync(buffer,ct)) > 0) { if (bytes.Length+count > options.MaxUploadBytes) throw DocumentErrors.Invalid("INVALID_UPLOAD_SIZE"); await bytes.WriteAsync(buffer.AsMemory(0,count),ct); }
        var r = await service.UploadAsync(schoolId,purpose,bytes.ToArray(),operation,actor,ct);
        return Created(r,$"/api/v1/schools/{schoolId:D}/document-files/{r.Value.FileId:D}");
    }
    /// <summary>Read an uploaded file's scan receipt without exposing Blob keys.</summary>
    [HttpGet("schools/{schoolId:guid}/document-files/{fileId:guid}",Name = "getDocumentFileStatus")]
    public async Task<ActionResult<FileResponse>> FileStatus(Guid schoolId, Guid fileId, CancellationToken ct) => Ok(await service.FileAsync(schoolId,fileId,await Actor(ct),ct));
    /// <summary>Generate an immutable contract asynchronously from current authorized owner projections.</summary>
    [HttpPost("schools/{schoolId:guid}/contracts",Name = "generateTrainingContract"), ProducesResponseType<DocumentJobResponse>(202)]
    public async Task<ActionResult<DocumentJobResponse>> GenerateContract(Guid schoolId, GenerateContractRequest request, CancellationToken ct)
    { var r = await service.GenerateContractAsync(schoolId,request,Operation(),await Actor(ct),ct); return Accepted($"/api/v1/document-jobs/{r.Value.JobId:D}?schoolId={schoolId:D}",r.Value); }
    /// <summary>Generate a PracticalJournal Prepared version from exact Learning evidence.</summary>
    [HttpPost("enrollments/{enrollmentId:guid}/journals",Name = "generatePracticalJournal"), ProducesResponseType<DocumentJobResponse>(202)]
    public async Task<ActionResult<DocumentJobResponse>> Journal(Guid schoolId, Guid enrollmentId, GenerateTrainingDocumentRequest request, CancellationToken ct)
    { var r = await service.GenerateAsync(schoolId,enrollmentId,"PracticalJournal",request,Operation(),await Actor(ct),ct); return Accepted($"/api/v1/document-jobs/{r.Value.JobId:D}?schoolId={schoolId:D}",r.Value); }
    /// <summary>Prepare an internal CompletionReport for explicit human review and publication.</summary>
    [HttpPost("enrollments/{enrollmentId:guid}/completion-reports",Name = "generateCompletionReport"), ProducesResponseType<DocumentJobResponse>(202)]
    public async Task<ActionResult<DocumentJobResponse>> Completion(Guid schoolId, Guid enrollmentId, GenerateTrainingDocumentRequest request, CancellationToken ct)
    { var r = await service.GenerateAsync(schoolId,enrollmentId,"CompletionReport",request,Operation(),await Actor(ct),ct); return Accepted($"/api/v1/document-jobs/{r.Value.JobId:D}?schoolId={schoolId:D}",r.Value); }
    /// <summary>Read durable generation progress separately from document readiness.</summary>
    [HttpGet("document-jobs/{jobId:guid}",Name = "getDocumentJob")]
    public async Task<ActionResult<DocumentJobResponse>> Job(Guid schoolId, Guid jobId, CancellationToken ct) => Ok(await service.JobAsync(schoolId,jobId,await Actor(ct),ct));
    /// <summary>Read a contract and retained signed-copy metadata.</summary>
    [HttpGet("enrollments/{enrollmentId:guid}/contracts",Name = "listEnrollmentContracts")]
    public async Task<ActionResult<ContractResponse[]>> Contracts(Guid schoolId, Guid enrollmentId, CancellationToken ct) => Ok(await service.ContractsAsync(schoolId,enrollmentId,await Actor(ct),ct));
    /// <summary>Read a contract and retained signed-copy metadata.</summary>
    [HttpGet("schools/{schoolId:guid}/contracts/{contractId:guid}",Name = "getTrainingContract")]
    public async Task<ActionResult<ContractResponse>> Contract(Guid schoolId, Guid contractId, CancellationToken ct) => Ok(await service.ContractAsync(schoolId,contractId,await Actor(ct),ct));
    /// <summary>Register a Clean external signed PDF, retaining all previous copies.</summary>
    [HttpPost("schools/{schoolId:guid}/contracts/{contractId:guid}/signed-copies",Name = "attachContractSignedCopy"), ProducesResponseType<ContractResponse>(201)]
    public async Task<ActionResult<ContractResponse>> Attach(Guid schoolId, Guid contractId, AttachSignedCopyRequest request, CancellationToken ct)
    { var r = await service.AttachAsync(schoolId,contractId,request,Operation(),await Actor(ct),ct); return Created(r,$"/api/v1/schools/{schoolId:D}/contracts/{contractId:D}"); }
    /// <summary>Cancel a contract with immutable audit and preserved files.</summary>
    [HttpPost("schools/{schoolId:guid}/contracts/{contractId:guid}/cancel",Name = "cancelTrainingContract")]
    public async Task<ActionResult<ContractResponse>> Cancel(Guid schoolId, Guid contractId, RevisionRequest request, CancellationToken ct) => Ok((await service.CancelAsync(schoolId,contractId,request,Operation(),await Actor(ct),ct)).Value);
    /// <summary>List documents within one currently authorized enrollment.</summary>
    [HttpGet("enrollments/{enrollmentId:guid}/documents",Name = "listEnrollmentDocuments")]
    public async Task<ActionResult<DocumentResponse[]>> Documents(Guid schoolId, Guid enrollmentId, CancellationToken ct) => Ok(await service.DocumentsAsync(schoolId,enrollmentId,await Actor(ct),ct));
    /// <summary>Read visible document versions and the explicit current selection.</summary>
    [HttpGet("documents/{documentId:guid}",Name = "getTrainingDocument")]
    public async Task<ActionResult<DocumentResponse>> Document(Guid schoolId, Guid documentId, CancellationToken ct) => Ok(await service.DocumentAsync(schoolId,documentId,await Actor(ct),ct));
    /// <summary>Record administrator review of a Prepared version.</summary>
    [HttpPost("documents/{documentId:guid}/versions/{versionId:guid}/review",Name = "reviewTrainingDocument")]
    public async Task<ActionResult<DocumentResponse>> Review(Guid schoolId, Guid documentId, Guid versionId, VersionCommandRequest request, CancellationToken ct) => Ok((await service.VersionCommandAsync(schoolId,documentId,versionId,"review",request,Operation(),await Actor(ct),ct)).Value);
    /// <summary>Publish as explicit Review-only or request a durable Learning Ready decision.</summary>
    [HttpPost("documents/{documentId:guid}/versions/{versionId:guid}/publish",Name = "publishTrainingDocument"), ProducesResponseType<DocumentResponse>(202)]
    public async Task<ActionResult<DocumentResponse>> Publish(Guid schoolId, Guid documentId, Guid versionId, PublishRequest request, CancellationToken ct)
    { var r = await service.PublishAsync(schoolId,documentId,versionId,request,Operation(),await Actor(ct),ct); return request.Mode == "Ready" ? Accepted($"/api/v1/documents/{documentId:D}?schoolId={schoolId:D}",r.Value) : Ok(r.Value); }
    /// <summary>Withdraw a version without deleting historical bytes.</summary>
    [HttpPost("documents/{documentId:guid}/versions/{versionId:guid}/withdraw",Name = "withdrawTrainingDocument")]
    public async Task<ActionResult<DocumentResponse>> Withdraw(Guid schoolId, Guid documentId, Guid versionId, VersionCommandRequest request, CancellationToken ct) => Ok((await service.VersionCommandAsync(schoolId,documentId,versionId,"withdraw",request,Operation(),await Actor(ct),ct)).Value);
    /// <summary>Select an eligible existing publication after current owner checks.</summary>
    [HttpPost("documents/{documentId:guid}/versions/{versionId:guid}/select",Name = "selectTrainingDocument")]
    public async Task<ActionResult<DocumentResponse>> Select(Guid schoolId, Guid documentId, Guid versionId, VersionCommandRequest request, CancellationToken ct) => Ok((await service.VersionCommandAsync(schoolId,documentId,versionId,"select",request,Operation(),await Actor(ct),ct)).Value);
    /// <summary>Clear an explicit current selection without choosing an older fallback.</summary>
    [HttpPost("documents/{documentId:guid}/versions/{versionId:guid}/clear",Name = "clearTrainingDocument")]
    public async Task<ActionResult<DocumentResponse>> Clear(Guid schoolId, Guid documentId, Guid versionId, VersionCommandRequest request, CancellationToken ct) => Ok((await service.VersionCommandAsync(schoolId,documentId,versionId,"clear",request,Operation(),await Actor(ct),ct)).Value);
    /// <summary>Read scoped document lifecycle history.</summary>
    [HttpGet("documents/{documentId:guid}/history",Name = "getTrainingDocumentHistory")]
    public async Task<ActionResult<HistoryResponse[]>> History(Guid schoolId, Guid documentId, CancellationToken ct) => Ok(await service.HistoryAsync(schoolId,documentId,false,await Actor(ct),ct));
    /// <summary>Read scoped contract lifecycle history.</summary>
    [HttpGet("schools/{schoolId:guid}/contracts/{contractId:guid}/history",Name = "getTrainingContractHistory")]
    public async Task<ActionResult<HistoryResponse[]>> ContractHistory(Guid schoolId, Guid contractId, CancellationToken ct) => Ok(await service.HistoryAsync(schoolId,contractId,true,await Actor(ct),ct));
    /// <summary>Issue a 60-second authenticated service URL for current or explicitly historical bytes.</summary>
    [HttpGet("documents/{documentId:guid}/download",Name = "getDocumentDownload")]
    public async Task<ActionResult<DocumentDownloadResponse>> Download(Guid schoolId, Guid documentId, Guid? versionId, bool historical, CancellationToken ct, string format = "pdf") => Ok(await service.DownloadAsync(schoolId,documentId,versionId,historical,format,await Actor(ct),ct));
    /// <summary>Reauthorize and verify current evidence before delivering private file bytes.</summary>
    [HttpGet("document-downloads/{grantId:guid}",Name = "downloadDocumentContent"), Produces("application/pdf","application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task<IActionResult> Content(Guid schoolId, Guid grantId, CancellationToken ct)
    { var r = await service.ContentAsync(schoolId,grantId,await Actor(ct),ct); Response.Headers["X-Content-Type-Options"] = "nosniff"; return File(r.Bytes,r.MediaType,r.Name,enableRangeProcessing:false); }
}
