namespace Vairas.DocumentManagement.Configuration.Options;

public sealed class ScannerOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 30;
}
