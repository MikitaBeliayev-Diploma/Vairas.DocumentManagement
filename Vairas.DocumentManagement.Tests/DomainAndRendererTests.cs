using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Vairas.DocumentManagement.Integrations;
using Xunit;
namespace Vairas.DocumentManagement.Tests;

public sealed class DomainAndRendererTests
{
    [Theory]
    [InlineData("Prepared","Indeterminate",true)] [InlineData("Indeterminate","PublishedReady",true)] [InlineData("Prepared","PublishedReview",true)]
    [InlineData("PublishedReady","PublishedReady",true)] [InlineData("PublishedReview","Stale",true)] [InlineData("Stale","Withdrawn",true)]
    [InlineData("Stale","PublishedReady",false)] [InlineData("Withdrawn","Prepared",false)] [InlineData("Prepared","Prepared",false)]
    public void Lifecycle_is_terminal_for_stale_and_withdrawn(string from, string to, bool expected) => Assert.Equal(expected,DocumentRules.CanTransition(from,to));
    [Fact] public void Readiness_is_never_inferred_from_a_pointer_or_completed_job()
    {
        Assert.True(DocumentRules.CurrentUse("PublishedReady",true,4,4,true)); Assert.False(DocumentRules.CurrentUse("PublishedReady",true,4,5,true));
        Assert.False(DocumentRules.CurrentUse("PublishedReady",true,4,4,false)); Assert.False(DocumentRules.CurrentUse("PublishedReview",true,4,4,true));
        Assert.False(DocumentRules.CanRead(false,false,true,"TrainingContract",true)); Assert.True(DocumentRules.CanRead(false,true,false,"TrainingContract",true));
        Assert.False(DocumentRules.CanRead(false,true,false,"PracticalJournal",false)); Assert.True(DocumentRules.CanRead(true,false,false,"PracticalJournal",false));
        Assert.Equal("Unselected",DocumentRules.Pointer(null)); Assert.Equal(64,DocumentRules.Digest("payload").Length);
    }
    [Theory] [InlineData("TrainingContract")] [InlineData("PracticalJournal")] [InlineData("CompletionReport")]
    public void Demo_templates_are_valid_and_fill_exact_unicode_inputs(string kind)
    {
        var renderer = new DocxRenderer(new(),new()); var template = DocxRenderer.DemoTemplate(kind); renderer.ValidateTemplate(template,kind);
        var school = Guid.NewGuid(); var enrollment = Guid.NewGuid(); var snapshot = Guid.NewGuid();
        var evidence = kind == "TrainingContract" ? null : new EvidencePayload(school,enrollment,snapshot,kind,1,1,"Ready",true,true,DocumentRules.SourceVersion(snapshot,1),"fixture-v1",DateTimeOffset.UtcNow,
            [new("Practical",Guid.NewGuid(),Guid.NewGuid(),"Confirmed",45,DateTimeOffset.UtcNow.AddHours(-2),DateTimeOffset.UtcNow.AddHours(-1),12.5m,["Urban driving"],["Instructor Confirm","Student Confirm"],null,null)],[]);
        var basis = new RenderBasis("v1",kind,school,enrollment,Guid.NewGuid(),"Škola <B>","B","Микита Learner",1,1,1,Guid.NewGuid(),DateTimeOffset.UtcNow,evidence);
        var result = renderer.Fill(template,basis); using var doc = WordprocessingDocument.Open(new MemoryStream(result),false);
        Assert.Contains("Микита Learner",doc.MainDocumentPart!.Document.InnerText); Assert.Contains("Škola <B>",doc.MainDocumentPart.Document.InnerText); Assert.Contains("DEMO",doc.MainDocumentPart.Document.InnerText);
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc));
    }
    [Fact] public void Unknown_fields_and_external_relationships_are_rejected_before_conversion()
    {
        var renderer = new DocxRenderer(new(),new()); var bytes = DocxRenderer.DemoTemplate("TrainingContract");
        using var buffer = new MemoryStream(); buffer.Write(bytes); buffer.Position = 0;
        using (var document = WordprocessingDocument.Open(buffer,true)) { document.MainDocumentPart!.Document.Descendants<Tag>().First().Val = "execute"; document.MainDocumentPart.Document.Save(); }
        Business.ThrowsBusiness(() => renderer.ValidateTemplate(buffer.ToArray(),"TrainingContract"));
        using var other = new MemoryStream(); other.Write(bytes); other.Position = 0;
        using (var document = WordprocessingDocument.Open(other,true)) document.MainDocumentPart!.AddExternalRelationship("http://schemas.openxmlformats.org/officeDocument/2006/relationships/image",new Uri("https://example.test/private"));
        Business.ThrowsBusiness(() => renderer.ValidateTemplate(other.ToArray(),"TrainingContract"));
    }
}
