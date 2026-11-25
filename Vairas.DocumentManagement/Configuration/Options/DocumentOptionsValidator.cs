using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Business;

namespace Vairas.DocumentManagement.Configuration.Options;

public sealed class DocumentOptionsValidator(IHostEnvironment environment) : IValidateOptions<DocumentOptions>
{
    public ValidateOptionsResult Validate(string? name, DocumentOptions options) =>
        environment.IsEnvironment("Testing") || HttpsOrigin.IsValid(options.PublicOrigin)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Configure HTTPS service origins.");
}
