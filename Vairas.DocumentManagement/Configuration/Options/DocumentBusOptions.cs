namespace Vairas.DocumentManagement.Configuration.Options;

public sealed class DocumentBusOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "";
    public string LearningTopic { get; set; } = "vairas.learning.events.v1";
    public string SchoolTopic { get; set; } = "vairas.school.events.v1";
    public string Subscription { get; set; } = "document-management";
    public int MaxDeliveryCount { get; set; } = 10;
}
