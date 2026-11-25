using Microsoft.Extensions.Options;
using Vairas.DocumentManagement.Business;

namespace Vairas.DocumentManagement.Configuration.Options;

/// <summary>Skipped in the Testing environment, where tests supply disposable database roles.</summary>
public sealed class DatabaseOptionsValidator(IHostEnvironment environment, IOptions<DocumentOptions> documents) : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (environment.IsEnvironment("Testing"))
            return ValidateOptionsResult.Success;
        if (string.IsNullOrWhiteSpace(options.DocumentManagement))
            return ValidateOptionsResult.Fail("Configure the restricted runtime database.");
        if (documents.Value.WorkersEnabled && string.IsNullOrWhiteSpace(options.Delivery))
            return ValidateOptionsResult.Fail("Workers require restricted delivery credentials.");
        return ValidateOptionsResult.Success;
    }
}
