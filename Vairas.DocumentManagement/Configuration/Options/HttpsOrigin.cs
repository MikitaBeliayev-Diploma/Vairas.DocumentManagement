namespace Vairas.DocumentManagement.Configuration.Options;

internal static class HttpsOrigin
{
    public static bool IsValid(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0;
}
