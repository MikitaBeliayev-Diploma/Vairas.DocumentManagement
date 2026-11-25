using Microsoft.Extensions.Options;

namespace Vairas.DocumentManagement.Configuration.Options;

/// <summary>A disabled broker integration needs no credentials.</summary>
public sealed class DocumentBusOptionsValidator(IHostEnvironment environment) : IValidateOptions<DocumentBusOptions>
{
    public ValidateOptionsResult Validate(string? name, DocumentBusOptions options) =>
        !environment.IsEnvironment("Testing") && options.Enabled && string.IsNullOrWhiteSpace(options.ConnectionString)
            ? ValidateOptionsResult.Fail("Configure broker credentials.")
            : ValidateOptionsResult.Success;
}
