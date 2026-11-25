namespace Vairas.DocumentManagement.Configuration.Options;

/// <summary>Bound from "ConnectionStrings": restricted runtime role plus the separate delivery worker role.</summary>
public sealed class DatabaseOptions
{
    public string DocumentManagement { get; set; } = "";
    public string Delivery { get; set; } = "";
}
