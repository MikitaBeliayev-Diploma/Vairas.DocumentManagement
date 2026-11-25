using System.Security.Cryptography;
using System.Text;

namespace Vairas.DocumentManagement.Domain;

public static class DocumentRules
{
    public static readonly string[] Kinds = ["TrainingContract", "PracticalJournal", "CompletionReport"];
    public static bool Published(string state) => state is "PublishedReady" or "PublishedReview";
    public static bool CanTransition(string from, string to) => from switch
    {
        "Prepared" or "Indeterminate" => to is "PublishedReview" or "PublishedReady" or "Stale" or "Withdrawn" || from == "Prepared" && to == "Indeterminate",
        "PublishedReview" or "PublishedReady" => to == from || to is "Stale" or "Withdrawn",
        "Stale" => to == "Withdrawn",
        _ => false
    };
    public static bool CurrentUse(string state, bool selected, long versionRevision, long checkpoint, bool ownerCurrent) =>
        state == "PublishedReady" && selected && versionRevision >= checkpoint && ownerCurrent;
    public static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string Digest(string value) => Digest(Encoding.UTF8.GetBytes(value));
    public static string Pointer(Guid? id) => id?.ToString("D") ?? "Unselected";
    public static bool CanRead(bool administrator, bool self, bool assignedTeacher, string kind, bool published) =>
        administrator || published && (self || assignedTeacher && kind != "TrainingContract");
    public static string SourceVersion(Guid snapshot, long revision) => $"evidence-{snapshot:D}-{revision}";
}
