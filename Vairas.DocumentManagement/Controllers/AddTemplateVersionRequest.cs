using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Controllers;

/// <summary>Add a scanned file under exact template root revision.</summary>
public sealed record AddTemplateVersionRequest(Guid FileId, long ExpectedRevision);
