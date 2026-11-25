using Microsoft.Extensions.Options;

namespace Vairas.DocumentManagement.Configuration.Options;

public sealed class BlobOptionsValidator(IHostEnvironment environment) : IValidateOptions<BlobOptions>
{
    public ValidateOptionsResult Validate(string? name, BlobOptions options) =>
        !environment.IsEnvironment("Testing") && !environment.IsDevelopment()
            && (options.ConnectionString?.Contains("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase) == true
                || options.ServiceUri?.StartsWith("http:", StringComparison.OrdinalIgnoreCase) == true)
            ? ValidateOptionsResult.Fail("Development Blob settings are not allowed in production.")
            : ValidateOptionsResult.Success;
}
