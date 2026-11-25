using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Configuration.Options;

namespace Vairas.DocumentManagement.Integrations;


public sealed class DocxRenderer(DocumentOptions options, RendererOptions rendererOptions) : IDocumentRenderer
{
    public static readonly string[] Fields = ["schoolName","studentName","schoolId","enrollmentId","category","kind","generatedAt","snapshotVersion","readiness","missingItems","evidenceRows","programVersion","checklistVersion","ruleSources"];
    public void ValidateTemplate(byte[] docx, string kind)
    {
        if (docx.LongLength is 0 || docx.LongLength > options.MaxUploadBytes) throw DocumentErrors.Invalid("INVALID_TEMPLATE_SIZE");
        try
        {
            using var zip = new ZipArchive(new MemoryStream(docx),ZipArchiveMode.Read);
            if (zip.Entries.Count > 2000 || zip.Entries.Sum(x => x.Length) > options.MaxExpandedDocxBytes ||
                zip.Entries.Any(x => x.FullName.Contains("..",StringComparison.Ordinal) || x.FullName.Contains("vbaProject",StringComparison.OrdinalIgnoreCase) || x.FullName.Contains("embeddings/",StringComparison.OrdinalIgnoreCase)))
                throw DocumentErrors.Invalid("UNSAFE_TEMPLATE");
            using var document = WordprocessingDocument.Open(new MemoryStream(docx),false);
            if (document.DocumentType != WordprocessingDocumentType.Document || document.MainDocumentPart?.Document.Body is null) throw DocumentErrors.Invalid("DOCX_REQUIRED");
            var parts = Parts(document).ToArray();
            if (parts.Any(p => p.ExternalRelationships.Any() || p.HyperlinkRelationships.Any(r => r.IsExternal)) || document.ExternalRelationships.Any()) throw DocumentErrors.Invalid("EXTERNAL_RELATIONSHIP_FORBIDDEN");
            if (parts.Any(p => p.RootElement is not null && (p.RootElement.Descendants<FieldCode>().Any() || p.RootElement.Descendants<SimpleField>().Any() || p.RootElement.Descendants<AltChunk>().Any()))) throw DocumentErrors.Invalid("ACTIVE_CONTENT_FORBIDDEN");
            if (parts.Where(p => p != document.MainDocumentPart).Any(p => p.RootElement?.Descendants<SdtElement>().Any() == true)) throw DocumentErrors.Invalid("BODY_CONTENT_CONTROLS_REQUIRED");
            var body = document.MainDocumentPart.Document.Body;
            if (body.Descendants<FieldCode>().Any() || body.Descendants<SimpleField>().Any() || body.Descendants<AltChunk>().Any()) throw DocumentErrors.Invalid("ACTIVE_CONTENT_FORBIDDEN");
            var tags = body.Descendants<SdtElement>().Select(x => x.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value).ToArray();
            if (tags.Length == 0 || tags.Any(x => x is null || !Fields.Contains(x))) throw DocumentErrors.Invalid("UNKNOWN_TEMPLATE_FIELD");
            if (kind != "Any" && (!tags.Contains("schoolName") || !tags.Contains("studentName") || kind != "TrainingContract" && !tags.Contains("evidenceRows"))) throw DocumentErrors.Invalid("REQUIRED_TEMPLATE_FIELDS_MISSING");
            if (new OpenXmlValidator().Validate(document).Take(1).Any()) throw DocumentErrors.Invalid("INVALID_DOCX_STRUCTURE");
        }
        catch (Exception e) when (e is InvalidDataException or IOException or OpenXmlPackageException or System.Xml.XmlException or OverflowException) { throw DocumentErrors.Invalid("INVALID_DOCX"); }
    }
    private static IEnumerable<OpenXmlPart> Parts(OpenXmlPartContainer container)
    {
        var visited = new HashSet<Uri>(); var pending = new Stack<OpenXmlPart>(container.Parts.Select(x => x.OpenXmlPart));
        while (pending.TryPop(out var part))
        { if (!visited.Add(part.Uri)) continue; yield return part; foreach (var child in part.Parts) pending.Push(child.OpenXmlPart); }
    }
    public byte[] Fill(byte[] template, RenderBasis basis)
    {
        ValidateTemplate(template,basis.Kind);
        using var stream = new MemoryStream(); stream.Write(template); stream.Position = 0;
        using (var doc = WordprocessingDocument.Open(stream,true))
        {
            var values = new Dictionary<string,string>
            {
                ["schoolName"] = basis.SchoolName, ["studentName"] = basis.StudentName, ["schoolId"] = basis.SchoolId.ToString("D"), ["enrollmentId"] = basis.EnrollmentId.ToString("D"),
                ["category"] = basis.Category, ["kind"] = basis.Kind, ["generatedAt"] = basis.GeneratedAt.ToString("O",CultureInfo.InvariantCulture),
                ["snapshotVersion"] = basis.Evidence?.SourceSnapshotVersion ?? "Not applicable", ["readiness"] = basis.Evidence?.Status ?? "External signature required",
                ["programVersion"] = basis.Evidence?.ProgramVersionId?.ToString("D") ?? "Not supplied", ["checklistVersion"] = basis.Evidence?.ChecklistVersionId?.ToString("D") ?? "Not supplied",
                ["ruleSources"] = basis.Evidence?.RuleSources is { Length: > 0 } rules ? string.Join("; ",rules.Select(x => $"{x.Code}: {x.Edition} ({x.SourceUri}); {x.Applicability}")) : "Not supplied",
                ["missingItems"] = basis.Evidence is null ? "No signature validity is asserted." : string.Join("; ",basis.Evidence.Checks.Where(x => x.Outcome != "Satisfied").Select(x => $"{x.Code}/{x.Field}: {x.Explanation}"))
            };
            foreach (var control in doc.MainDocumentPart!.Document.Body!.Descendants<SdtElement>().ToArray())
            {
                var tag = control.SdtProperties!.GetFirstChild<Tag>()!.Val!.Value!;
                if (control is not SdtBlock block) throw DocumentErrors.Invalid("BLOCK_CONTENT_CONTROL_REQUIRED");
            var content = block.GetFirstChild<SdtContentBlock>() ?? throw DocumentErrors.Invalid("INVALID_CONTENT_CONTROL"); content.RemoveAllChildren();
                if (tag == "evidenceRows") content.Append(EvidenceTable(basis.Evidence?.Rows ?? []));
                else content.Append(Paragraph(values[tag]));
            }
            var body = doc.MainDocumentPart.Document.Body!;
            var provenance = Paragraph($"Generated artifact {basis.GenerationId?.ToString("D") ?? "demonstration"}. Template version {basis.TemplateVersionId:D}. Generated {basis.GeneratedAt:O}. Evidence {basis.Evidence?.SourceSnapshotVersion ?? "not applicable"}. Current publication and readiness must be checked in Vairas; external copies cannot be recalled.");
            var section = body.GetFirstChild<SectionProperties>(); if (section is null) body.Append(provenance); else body.InsertBefore(provenance,section);
            doc.MainDocumentPart.Document.Save();
        }
        return stream.ToArray();
    }
    public async Task<RenderOutput> RenderAsync(byte[] template, RenderBasis basis, CancellationToken ct)
    {
        var bytes = Fill(template,basis); var directory = Path.Combine(Path.GetTempPath(),"vairas-render-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory,"document.docx"); await File.WriteAllBytesAsync(input,bytes,ct);
            var profile = new Uri(Path.Combine(directory,"profile")).AbsoluteUri;
            var start = new ProcessStartInfo(rendererOptions.Executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = directory };
            foreach (var arg in new[] { "-env:UserInstallation="+profile,"--headless","--nologo","--nodefault","--norestore","--convert-to","pdf:writer_pdf_Export","--outdir",directory,input }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw DocumentErrors.Dependency("RENDERER_UNAVAILABLE");
            // Drain output without retaining private filenames or unbounded process output.
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null,ct); var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null,ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.RendererTimeoutSeconds));
            try { await process.WaitForExitAsync(timeout.Token); await Task.WhenAll(stdout,stderr); }
            catch (OperationCanceledException) { try { process.Kill(true); } catch (InvalidOperationException) { } if (ct.IsCancellationRequested) throw; throw DocumentErrors.Dependency("RENDERER_TIMEOUT"); }
            var output = Path.Combine(directory,"document.pdf");
            if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length > options.MaxExpandedDocxBytes) throw DocumentErrors.Dependency("PDF_CONVERSION_FAILED");
            var pdf = await File.ReadAllBytesAsync(output,ct); if (!pdf.AsSpan().StartsWith("%PDF-"u8)) throw DocumentErrors.Dependency("INVALID_PDF_OUTPUT");
            return new(bytes,pdf,"openxml-3.3.0-libreoffice-v1");
        }
        catch (System.ComponentModel.Win32Exception) { throw DocumentErrors.Dependency("RENDERER_UNAVAILABLE"); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
    internal static Paragraph Paragraph(string text, bool bold = false)
    {
        var run = new Run(new RunProperties { RunFonts = new RunFonts { Ascii = "Liberation Sans", HighAnsi = "Liberation Sans" }, Bold = new Bold { Val = bold }, Color = new Color { Val = "000000" }, FontSize = new FontSize { Val = "20" } });
        foreach (var line in text.Split('\n')) { if (run.Elements<Text>().Any()) run.Append(new Break()); run.Append(new Text(line) { Space = SpaceProcessingModeValues.Preserve }); }
        return new Paragraph(run);
    }
    private static Table EvidenceTable(EvidenceRow[] rows)
    {
        var table = new Table(new TableProperties(new TableWidth { Width = "9000", Type = TableWidthUnitValues.Dxa },new TableBorders(
            new TopBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 4 },new LeftBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 4 },new RightBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 4 },new InsideVerticalBorder { Val = BorderValues.Single, Color = "D9D9D9", Size = 4 })));
        table.Append(new TableGrid(new GridColumn { Width = "1200" },new GridColumn { Width = "2200" },new GridColumn { Width = "1000" },new GridColumn { Width = "4600" }));
        TableCell Cell(string text, string width, bool header = false) => new(new TableCellProperties(new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa },new Shading { Val = ShadingPatternValues.Clear, Fill = header ? "E8E8E8" : "FFFFFF" }),Paragraph(text,header));
        table.Append(new TableRow(new TableRowProperties(new TableHeader()),Cell("Type","1200",true),Cell("Actual interval","2200",true),Cell("Credit minutes","1000",true),Cell("Observed facts","4600",true)));
        foreach (var row in rows)
        {
            var details = string.Join("; ",new[] { row.State,row.DistanceKm is null ? "" : $"{row.DistanceKm.Value.ToString(CultureInfo.InvariantCulture)} km",string.Join(", ",row.Topics),row.Outcome ?? "",row.Mark?.ToString(CultureInfo.InvariantCulture) ?? "",string.Join("; ",row.Confirmations) }.Where(x => x.Length > 0));
            table.Append(new TableRow(new TableRowProperties(new CantSplit()),Cell(row.Kind,"1200"),Cell(row.AssessedAt is not null ? $"Assessed {row.AssessedAt:yyyy-MM-dd HH:mm zzz}" : row.ActualStart is null ? "Not applicable" : $"{row.ActualStart:yyyy-MM-dd HH:mm zzz}\n{row.ActualEnd:yyyy-MM-dd HH:mm zzz}","2200"),Cell(row.CreditedMinutes.ToString(CultureInfo.InvariantCulture),"1000"),Cell(details,"4600")));
        }
        return table;
    }
    public static byte[] DemoTemplate(string kind)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream,WordprocessingDocumentType.Document,true))
        {
            var main = document.AddMainDocumentPart(); main.Document = new Document(); var body = main.Document.AppendChild(new Body());
            var title = kind switch { "TrainingContract" => "Training contract", "PracticalJournal" => "Practical training journal", _ => "Training completion report" };
            body.Append(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Title" }),new Run(new RunProperties(new Bold(),new Color { Val = "000000" },new FontSize { Val = "32" }),new Text("Vairas "+title.ToLowerInvariant()+" demonstration"))));
            body.Append(Paragraph("DEMO — Synthetic template. Not approved for official use."));
            var labels = new Dictionary<string,string> { ["schoolName"] = "School", ["studentName"] = "Student", ["category"] = "Category", ["enrollmentId"] = "Enrollment reference", ["generatedAt"] = "Generated at", ["readiness"] = "Readiness", ["snapshotVersion"] = "Evidence reference", ["missingItems"] = "Outstanding requirements" };
            if (kind != "TrainingContract") { labels["programVersion"] = "Programme version"; labels["checklistVersion"] = "Checklist version"; labels["ruleSources"] = "Applicable rule editions"; }
            foreach (var field in labels)
            { body.Append(Paragraph(field.Value,true)); body.Append(new SdtBlock(new SdtProperties(new Tag { Val = field.Key }),new SdtContentBlock(Paragraph("DEMO "+field.Value)))); }
            if (kind != "TrainingContract") body.Append(new SdtBlock(new SdtProperties(new Tag { Val = "evidenceRows" }),new SdtContentBlock(Paragraph("Evidence rows"))));
            body.Append(new SectionProperties(new PageSize { Width = 11906, Height = 16838 },new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134, Header = 567, Footer = 567, Gutter = 0 }));
            main.Document.Save();
        }
        return stream.ToArray();
    }
}
