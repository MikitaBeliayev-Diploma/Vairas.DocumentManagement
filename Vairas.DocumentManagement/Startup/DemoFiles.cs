using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Integrations;

namespace Vairas.DocumentManagement.Startup;

/// <summary>Writes synthetic DEMO templates and renders fixture documents to VAIRAS_ARTIFACT_DIRECTORY.</summary>
public static class DemoFiles
{
    public static async Task WriteDemoTemplatesAsync()
    {
        var directory = Environment.GetEnvironmentVariable("VAIRAS_ARTIFACT_DIRECTORY") ?? throw new InvalidOperationException("Set VAIRAS_ARTIFACT_DIRECTORY."); Directory.CreateDirectory(directory);
        foreach (var kind in Vairas.DocumentManagement.Domain.DocumentRules.Kinds) await File.WriteAllBytesAsync(Path.Combine(directory,kind+".docx"),DocxRenderer.DemoTemplate(kind));
    }

    public static async Task RenderFixturesAsync()
    {
        var directory = Environment.GetEnvironmentVariable("VAIRAS_ARTIFACT_DIRECTORY") ?? throw new InvalidOperationException("Set VAIRAS_ARTIFACT_DIRECTORY."); Directory.CreateDirectory(directory);
        var renderer = new DocxRenderer(new(),new()); var school = Guid.Parse("11111111-1111-4111-8111-111111111111"); var enrollment = Guid.Parse("22222222-2222-4222-8222-222222222222");
        foreach (var kind in Vairas.DocumentManagement.Domain.DocumentRules.Kinds)
        {
            var snapshot = Guid.Parse("33333333-3333-4333-8333-333333333333");
            var rows = Enumerable.Range(1,55).Select(i => new EvidenceRow(i % 3 == 0 ? "Theory" : "Practical",Guid.NewGuid(),Guid.NewGuid(),"Confirmed",45,
                DateTimeOffset.Parse("2026-09-01T10:00:00+02:00").AddDays(i),DateTimeOffset.Parse("2026-09-01T11:00:00+02:00").AddDays(i),12.5m,["Urban driving", "Road safety"],["Instructor Confirm", "Student Confirm"],null,null)).ToArray();
            var evidence = kind == "TrainingContract" ? null : new EvidencePayload(school,enrollment,snapshot,kind,7,7,"Incomplete",false,true,Vairas.DocumentManagement.Domain.DocumentRules.SourceVersion(snapshot,7),"fixture-v1",DateTimeOffset.UtcNow,rows,[new("DEMO_POLICY","Authority","Unverified","This demonstration has no approved legal policy.")]);
            var basis = new RenderBasis("fixture-v1",kind,school,enrollment,Guid.NewGuid(),"Vairas Synthetic Driving School","B","Микита Šynthetic Learner",1,1,1,Guid.NewGuid(),DateTimeOffset.UtcNow,evidence);
            var result = await renderer.RenderAsync(DocxRenderer.DemoTemplate(kind),basis,CancellationToken.None);
            await File.WriteAllBytesAsync(Path.Combine(directory,kind+".docx"),result.Docx); await File.WriteAllBytesAsync(Path.Combine(directory,kind+".pdf"),result.Pdf);
        }
    }
}
