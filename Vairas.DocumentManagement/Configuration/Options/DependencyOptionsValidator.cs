using Microsoft.Extensions.Options;

namespace Vairas.DocumentManagement.Configuration.Options;

public sealed class DependencyOptionsValidator(IHostEnvironment environment) : IValidateOptions<DependencyOptions>
{
    public ValidateOptionsResult Validate(string? name, DependencyOptions options)
    {
        if (environment.IsEnvironment("Testing"))
            return ValidateOptionsResult.Success;
        if (string.IsNullOrWhiteSpace(options.ClientSecret))
            return ValidateOptionsResult.Fail("Configure workload credentials.");
        if (!new[] { options.AuthBaseUrl, options.UserManagementBaseUrl, options.SchoolBaseUrl, options.LearningBaseUrl }.All(HttpsOrigin.IsValid))
            return ValidateOptionsResult.Fail("Configure HTTPS service origins.");
        return ValidateOptionsResult.Success;
    }
}
