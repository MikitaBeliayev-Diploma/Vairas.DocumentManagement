using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Middleware;
using Vairas.DocumentManagement.Domain;

namespace Vairas.DocumentManagement.Security;


public sealed record EntryRequirement(string? Scope = null) : IAuthorizationRequirement;
