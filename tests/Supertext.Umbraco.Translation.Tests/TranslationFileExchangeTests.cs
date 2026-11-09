using System.Text.RegularExpressions;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.Sdk.Files;
using Supertext.Umbraco.Translation.Services;
using Xunit;

namespace Supertext.Umbraco.Translation.Tests;

/// <summary>Export for translation / Import translation: the glue around the SDK's TranslationFiles.</summary>
public class TranslationFileExchangeTests
{
    private static readonly Guid Document = Guid.Parse("5f2c3a8e-1d2b-4c3d-9e8f-0a1b2c3d4e5f");

    /// <summary>The texts ContentTranslator collects for a small page: name, a text box, rich text, a block.</summary>
    private static TranslationSegments Page(string name = "Hello")
    {
        var segments = new TranslationSegments();
        segments.Add("name", name, false);
        segments.Add("subtitle", "A subtitle", false);
        segments.Add("body/markup", "<p>Body <b>text</b></p>", true);
        segments.Add("blocks/7f3c/text", "Block text", false);
        return segments;
    }

    private static string ExportXliff(string target = "de-CH")
    {
        var job = TranslationFileExchange.Job(Document, "en-US", target, "en", "de-CH", Page());
        return TranslationFileExchange.Export(TranslationFileExchange.FormatXliff, Document, [target], [job]).Content;
    }

    /// <summary>Fills every target of an exported XLIFF file with "[de] " + source.</summary>
    private static string Translate(string xliff)
        => Regex.Replace(xliff, "<source>(.*?)</source>", m => $"<source>{m.Groups[1].Value}</source><target>[de] {m.Groups[1].Value}</target>");

    [Fact]
    public void ReferenceRoundTrip()
    {
        var reference = TranslationFileExchange.Reference(Document, "en-US", "de-CH");
        Assert.Equal("document:5f2c3a8e-1d2b-4c3d-9e8f-0a1b2c3d4e5f:en-US:de-CH", reference);
        Assert.True(TranslationFileExchange.TryParseReference(reference, out var key, out var source, out var target));
        Assert.Equal((Document, "en-US", "de-CH"), (key, source, target));
        Assert.False(TranslationFileExchange.TryParseReference("page:12", out _, out _, out _));
        Assert.False(TranslationFileExchange.TryParseReference("document:not-a-guid:en:de", out _, out _, out _));
        Assert.False(TranslationFileExchange.TryParseReference($"document:{Document}:en", out _, out _, out _));
        Assert.False(TranslationFileExchange.TryParseReference(null, out _, out _, out _));
    }

    [Fact]
    public void SegmentKeysAreUnique()
    {
        var segments = new TranslationSegments();
        Assert.Equal("title", segments.Add("title", "A", false));
        Assert.Equal("title#2", segments.Add("title", "B", false));
    }

    [Fact]
    public void ExportUsesTheCollectorKeysAndTheMapping()
    {
        var options = new SupertextOptions();
        options.Languages["de"] = new SupertextLanguageOptions { Code = "de-CH" };
        var target = TranslationFileExchange.LanguageCode(() => LanguageMapping.Target("de", options), "de");
        var source = TranslationFileExchange.LanguageCode(() => LanguageMapping.Source("en-US", options), "en-US");
        var job = TranslationFileExchange.Job(Document, "en-US", "de", source, target, Page());

        Assert.Equal(TranslationFileExchange.PluginName, job.Plugin);
        Assert.Equal($"document:{Document}:en-US:de", job.Reference);
        Assert.Equal(("en", "de-CH"), (job.SourceLanguage, job.TargetLanguage));
        Assert.Equal(["name", "subtitle", "body/markup", "blocks/7f3c/text"], job.Segments.Select(s => s.Key));
        Assert.True(job.Segments.Single(s => s.Key == "body/markup").Html);

        var file = TranslationFileExchange.Export(TranslationFileExchange.FormatXliff, Document, ["de"], [job]);
        Assert.Equal(($"umbraco-{Document}-de.xlf", "application/x-xliff+xml"), (file.FileName, file.ContentType));
        Assert.Equal(job.Segments.Select(s => s.Key), Assert.Single(TranslationFiles.Read(file.Content)).Units.Select(u => u.Key));

        var html = TranslationFileExchange.Export(TranslationFileExchange.FormatHtml, Document, ["de"], [job]);
        Assert.Equal(($"umbraco-{Document}-de.html", "text/html; charset=utf-8"), (html.FileName, html.ContentType));
        Assert.Equal($"umbraco-{Document}.xlf", TranslationFileExchange.Export(TranslationFileExchange.FormatXliff, Document, ["de", "fr"], [job, job with { Reference = "x" }]).FileName);
        Assert.Equal("htmlOneLanguage", Assert.Throws<TranslationException>(() => TranslationFileExchange.Export(TranslationFileExchange.FormatHtml, Document, ["de", "fr"], [job, job])).Code);
    }

    [Fact]
    public void UnmappedLanguagesDoNotBlockTheExport()
    {
        Assert.Equal("en-IE", TranslationFileExchange.LanguageCode(() => LanguageMapping.Target("en_IE", new SupertextOptions()), "en_IE"));
        Assert.Equal("de-DE", TranslationFileExchange.LanguageCode(() => LanguageMapping.Target("de", new SupertextOptions()), "de"));
    }

    [Fact]
    public void ImportWritesOkTexts()
    {
        var file = Assert.Single(TranslationFileExchange.Read(Translate(ExportXliff())));
        Assert.Equal((Document, "en-US", "de-CH"), TranslationFileExchange.Locate(file));

        var current = Page();
        Assert.Equal(new ImportCounts(4, 0, 0, 0, 0), TranslationFileExchange.Import(file, current, includeChanged: false));
        Assert.Equal("[de] Hello", current.Result("name"));
        Assert.Equal("[de] <p>Body <b>text</b></p>", current.Result("body/markup"));
    }

    [Fact]
    public void SourceChangedIsWrittenOnlyWithTheOption()
    {
        var file = Assert.Single(TranslationFileExchange.Read(Translate(ExportXliff())));

        var skipped = Page(name: "Hello again");
        Assert.Equal(new ImportCounts(3, 1, 0, 0, 0), TranslationFileExchange.Import(file, skipped, includeChanged: false));
        Assert.Null(skipped.Result("name"));

        var included = Page(name: "Hello again");
        Assert.Equal(new ImportCounts(4, 0, 0, 0, 0), TranslationFileExchange.Import(file, included, includeChanged: true));
        Assert.Equal("[de] Hello", included.Result("name"));
    }

    [Fact]
    public void UntranslatedInvalidAndMissingAreNotWritten()
    {
        var xliff = ExportXliff();
        var translated = Regex.Replace(xliff, @"(<trans-unit id=""name""[^>]*>\s*<source>Hello</source>)", "$1<target>Hallo</target>");
        translated = Regex.Replace(translated, @"(<trans-unit id=""body/markup""[^>]*>\s*<source>.*?</source>)", "$1<target>Text <script>x</script></target>");
        var file = Assert.Single(TranslationFileExchange.Read(translated));

        var current = Page();
        current.Add("added", "Added since the export", false);
        var counts = TranslationFileExchange.Import(file, current, includeChanged: true);

        Assert.Equal(new ImportCounts(1, 0, 2, 1, 1), counts);
        Assert.Equal("Hallo", current.Result("name"));
        Assert.Null(current.Result("subtitle"));
        Assert.Null(current.Result("body/markup"));
        Assert.Null(current.Result("added"));
    }

    [Fact]
    public void OtherSystemsAndUnknownReferencesAreRefused()
    {
        var other = new FileJob("Contao-Supertext-Translation", "page:12", "en", "de-CH", [new FileSegment("title", "Hello")]);
        var file = Assert.Single(TranslationFiles.Read(TranslationFiles.ExportXliff([other])));
        var error = Assert.Throws<TranslationException>(() => TranslationFileExchange.Locate(file));
        Assert.Equal("otherSystem", error.Code);
        Assert.Equal(["Contao-Supertext-Translation"], error.Args);

        file = Assert.Single(TranslationFiles.Read(TranslationFiles.ExportXliff([other with { Plugin = TranslationFileExchange.PluginName }])));
        Assert.Equal("fileDocumentNotFound", Assert.Throws<TranslationException>(() => TranslationFileExchange.Locate(file)).Code);
    }

    [Fact]
    public void BadAndTooLargeFilesAreRefused()
    {
        Assert.Equal("fileTooLarge", Assert.Throws<TranslationException>(() => TranslationFileExchange.Read(new string('a', (int)TranslationFileExchange.MaxUploadBytes + 1))).Code);
        // Multi-byte characters count as bytes.
        Assert.Equal("fileTooLarge", Assert.Throws<TranslationException>(() => TranslationFileExchange.Read(new string('ä', (int)TranslationFileExchange.MaxUploadBytes / 2 + 1))).Code);
        Assert.Equal("chooseFile", Assert.Throws<TranslationException>(() => TranslationFileExchange.Read("")).Code);
        Assert.Equal("invalidFile", Assert.Throws<TranslationException>(() => TranslationFileExchange.Read("<!DOCTYPE x [<!ENTITY a 'b'>]><xliff/>")).Code);
        Assert.Equal("missingMetadata", Assert.Throws<TranslationException>(() => TranslationFileExchange.Read("<html><head></head><body><div data-st-id=\"0\">x</div></body></html>")).Code);
    }
}
