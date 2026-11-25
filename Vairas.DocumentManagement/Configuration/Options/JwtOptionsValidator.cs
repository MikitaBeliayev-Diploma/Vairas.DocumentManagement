using Microsoft.Extensions.Options;

namespace Vairas.DocumentManagement.Configuration.Options;

public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options) =>
        environment.IsEnvironment("Testing") || HttpsOrigin.IsValid(options.Authority) && HttpsOrigin.IsValid(options.Issuer)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Configure HTTPS issuer and metadata authority.");
}
