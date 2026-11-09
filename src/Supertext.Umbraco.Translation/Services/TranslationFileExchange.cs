using System.Text;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Sdk.Files;
using Supertext.Umbraco.Translation.Sdk.V1;

namespace Supertext.Umbraco.Translation.Services;

/// <summary>A translation file ready for download.</summary>
public sealed record ExportedFile(string FileName, string ContentType, string Content);

/// <summary>
/// What an import did with one job's texts. <paramref name="Imported"/> counts the texts written (with
/// "also import texts whose source changed" ticked, those are included); <paramref name="SourceChanged"/>
/// the ones skipped because their source changed since the export.
/// </summary>
public sealed record ImportCounts(int Imported, int SourceChanged, int Untranslated, int InvalidMarkup, int Missing);

/// <summary>
/// "Export for translation" / "Import translation" without Umbraco (unit-tested): the job reference, the
/// SDK's <see cref="FileJob"/>s from the segments <see cref="ContentTranslator"/> collects (the same keys as the
/// API translation), file names, the upload size check, and applying an imported file to freshly collected
/// segments. <see cref="ContentTranslator"/> loads documents and writes back; the controller checks permissions.
/// </summary>
public static class TranslationFileExchange
{
    /// <summary>The <c>plugin</c> of every job: the repository name.</summary>
    public const string PluginName = "Umbraco-Supertext-Translation";

    /// <summary>The first part of the download's file name.</summary>
    public const string ShortName = "umbraco";

    /// <summary>Uploads larger than this (UTF-8 bytes) are refused before they are read.</summary>
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    public const string FormatXliff = "xliff";

    public const string FormatHtml = "html";

    private const string ReferencePrefix = "document:";

    /// <summary>
    /// The job reference: <c>document:&lt;document key&gt;:&lt;source culture&gt;:&lt;target culture&gt;</c>, e.g.
    /// <c>document:5f2c3a8e-…:en-US:de-CH</c> (the source culture too, so an import collects the same texts again).
    /// </summary>
    public static string Reference(Guid documentKey, string sourceCulture, string targetCulture)
        => ReferencePrefix + documentKey.ToString("D") + ":" + sourceCulture + ":" + targetCulture;

    /// <summary>Parses <see cref="Reference"/>; false for anything else.</summary>
    public static bool TryParseReference(string? reference, out Guid documentKey, out string sourceCulture, out string targetCulture)
    {
        documentKey = Guid.Empty;
        sourceCulture = targetCulture = string.Empty;
        if (reference is null || !reference.StartsWith(ReferencePrefix, StringComparison.Ordinal))
        {
            return false;
        }
        var parts = reference[ReferencePrefix.Length..].Split(':');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "D", out documentKey) || parts[1].Trim() == string.Empty || parts[2].Trim() == string.Empty)
        {
            documentKey = Guid.Empty;
            return false;
        }
        sourceCulture = parts[1];
        targetCulture = parts[2];
        return true;
    }

    /// <summary>
    /// The Supertext code the language mapping resolves, else the culture with <c>_</c> replaced by <c>-</c>:
    /// a file may go to any translator, so an unmapped culture doesn't block the export.
    /// </summary>
    public static string LanguageCode(Func<string> resolve, string culture)
    {
        try
        {
            return resolve();
        }
        catch (SupertextException)
        {
            return culture.Replace('_', '-');
        }
    }

    /// <summary>One job: the document's texts in the source culture for one target culture.</summary>
    public static FileJob Job(Guid documentKey, string sourceCulture, string targetCulture, string sourceCode, string targetCode, TranslationSegments segments)
        => new(PluginName, Reference(documentKey, sourceCulture, targetCulture), sourceCode, targetCode, segments.FileSegments());

    /// <summary>
    /// The file: XLIFF with one <c>&lt;file&gt;</c> per culture, or HTML for exactly one culture. Named
    /// <c>umbraco-&lt;key&gt;-&lt;culture&gt;.xlf</c> (<c>.html</c>), or <c>umbraco-&lt;key&gt;.xlf</c> for several cultures.
    /// </summary>
    public static ExportedFile Export(string format, Guid documentKey, IReadOnlyList<string> targetCultures, IReadOnlyList<FileJob> jobs)
    {
        var baseName = ShortName + "-" + documentKey.ToString("D") + (targetCultures.Count == 1 ? "-" + SafeName(targetCultures[0]) : string.Empty);
        if (format == FormatHtml)
        {
            if (jobs.Count != 1)
            {
                throw new TranslationException("htmlOneLanguage", "An HTML file holds one language: choose exactly one.");
            }
            return new ExportedFile(baseName + ".html", "text/html; charset=utf-8", TranslationFiles.ExportHtml(jobs[0]));
        }
        return new ExportedFile(baseName + ".xlf", "application/x-xliff+xml", TranslationFiles.ExportXliff(jobs));
    }

    /// <summary>Checks the upload's size first, then reads its jobs. Errors are <see cref="TranslationException"/>s for the backoffice.</summary>
    public static IReadOnlyList<TranslationFile> Read(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            throw new TranslationException("chooseFile", "Choose a file to import.");
        }
        if (content.Length > MaxUploadBytes || Encoding.UTF8.GetByteCount(content) > MaxUploadBytes)
        {
            throw new TranslationException("fileTooLarge", "The file is too large (at most 10 MB).");
        }
        try
        {
            return TranslationFiles.Read(content);
        }
        catch (TranslationFileException e)
        {
            throw FileError(e);
        }
    }

    /// <summary>Checks that a job read from a file is this package's; returns its reference's parts.</summary>
    public static (Guid DocumentKey, string SourceCulture, string TargetCulture) Locate(TranslationFile file)
    {
        if (!string.Equals(file.Plugin, PluginName, StringComparison.Ordinal))
        {
            throw new TranslationException("otherSystem", $"This file belongs to another system ({file.Plugin}).", [file.Plugin]);
        }
        if (!TryParseReference(file.Reference, out var key, out var source, out var target))
        {
            throw new TranslationException("fileDocumentNotFound", "The document this file was exported from no longer exists.");
        }
        return (key, source, target);
    }

    /// <summary>
    /// Imports a file's translations into the current segments (collected again from the document): <c>ok</c>
    /// texts, and <c>source_changed</c> ones when <paramref name="includeChanged"/>, become results; <c>untranslated</c>,
    /// <c>invalid_markup</c> and <c>missing</c> keep the source text.
    /// </summary>
    public static ImportCounts Import(TranslationFile file, TranslationSegments current, bool includeChanged)
    {
        int imported = 0, changed = 0, untranslated = 0, invalid = 0, missing = 0;
        IReadOnlyList<ImportedSegment> results;
        try
        {
            results = TranslationFiles.ImportTranslations(file, current.FileSegments());
        }
        catch (TranslationFileException e)
        {
            throw FileError(e);
        }
        foreach (var result in results)
        {
            switch (result.Status)
            {
                case ImportStatuses.Ok:
                case ImportStatuses.SourceChanged when includeChanged:
                    current.SetResult(result.Key, result.Text);
                    imported++;
                    break;
                case ImportStatuses.SourceChanged:
                    changed++;
                    break;
                case ImportStatuses.Untranslated:
                    untranslated++;
                    break;
                case ImportStatuses.InvalidMarkup:
                    invalid++;
                    break;
                default:
                    missing++;
                    break;
            }
        }
        return new ImportCounts(imported, changed, untranslated, invalid, missing);
    }

    /// <summary>A translation file error of the SDK as a backoffice error (<c>supertext_error_…</c>).</summary>
    public static TranslationException FileError(TranslationFileException e) => e.Code switch
    {
        TranslationFileErrorCodes.MissingMetadata => new TranslationException("missingMetadata", "This file wasn't exported by the Supertext package, or its header was removed.", inner: e),
        TranslationFileErrorCodes.TooLarge => new TranslationException("fileTooLarge", "The file is too large (at most 10 MB).", inner: e),
        TranslationFileErrorCodes.InvalidJob => new TranslationException("nothingToTranslate", "Nothing to translate.", inner: e),
        _ => new TranslationException("invalidFile", "This file isn't a valid XLIFF or HTML translation file.", inner: e),
    };

    private static string SafeName(string value)
        => new(value.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray());
}
