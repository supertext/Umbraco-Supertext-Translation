using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Sdk.Files;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Configuration;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Supertext.Umbraco.Translation.Services;

public enum TranslationStatus
{
    Translated,
    /// <summary>The target language already exists and overwrite was not requested.</summary>
    Exists,
    Failed,
}

public sealed record CultureTranslationResult(string Culture, TranslationStatus Status, int Fields, string? Error, TranslationException? Exception = null);

/// <summary>One job of an imported translation file: Translated (written), Exists (target exists, overwrite not requested) or Failed (nothing imported).</summary>
public sealed record FileImportResult(string Culture, TranslationStatus Status, ImportCounts? Counts);

/// <summary>
/// Translates a document from one culture into others: copies every culture-variant
/// property value from the source culture, translates the text in it (text boxes, text
/// areas, rich text and the text inside block list / block grid blocks, including rich
/// text blocks), and saves the document. The target languages are saved as drafts; the
/// editor reviews and publishes them.
/// </summary>
public sealed class ContentTranslator(
    IContentService contentService,
    IContentTypeService contentTypeService,
    ILanguageService languageService,
    SupertextClientFactory clients,
    LanguageSettingsProvider languageSettings,
    IOptionsMonitor<SupertextOptions> options,
    ILogger<ContentTranslator> logger)
{
    private SupertextOptions Options => options.CurrentValue;

    /// <summary>
    /// Loads a document by key through <see cref="IContentServiceBase{TItem}"/>: Umbraco 18 removed
    /// <c>IContentService.GetById(Guid)</c> (it is only on the base interface now), so a package built
    /// against Umbraco 17 that calls it directly fails on 18 with MissingMethodException.
    /// </summary>
    private IContent? GetDocument(Guid key) => ((IContentServiceBase<IContent>)contentService).GetById(key);

    public async Task<IReadOnlyList<CultureTranslationResult>> TranslateAsync(
        Guid documentKey, string sourceCulture, IReadOnlyCollection<string> targetCultures, bool overwrite, int userId, CancellationToken ct = default)
    {
        var content = GetDocument(documentKey) ?? throw new TranslationException("documentNotFound", "Document not found.");
        if (!content.ContentType.VariesByCulture())
        {
            throw new TranslationException("invariantDocumentType", $"The document type \"{content.ContentType.Name}\" does not vary by culture, so it has no separate language versions to translate into.", [content.ContentType.Name ?? content.ContentType.Alias]);
        }
        if (!content.IsCultureAvailable(sourceCulture))
        {
            throw new TranslationException("noSourceVersion", $"The document has no {sourceCulture} version to translate from.", [sourceCulture]);
        }

        var results = new List<CultureTranslationResult>();
        var changed = false;
        foreach (var target in targetCultures.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(target, sourceCulture, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (content.IsCultureAvailable(target) && !overwrite)
            {
                results.Add(new CultureTranslationResult(target, TranslationStatus.Exists, 0, null));
                continue;
            }
            try
            {
                var fields = await TranslateCultureAsync(content, sourceCulture, target, ct);
                results.Add(new CultureTranslationResult(target, TranslationStatus.Translated, fields, null));
                changed = true;
                logger.LogInformation("Supertext: translated {Fields} field(s) of document {Document} from {Source} to {Target}.", fields, documentKey, sourceCulture, target);
            }
            catch (Exception e) when (e is SupertextException or TranslationException or JsonException)
            {
                var error = e is SupertextException sdkError ? TranslationException.From(sdkError) : e as TranslationException;
                results.Add(new CultureTranslationResult(target, TranslationStatus.Failed, 0, error?.Message ?? e.Message, error));
                logger.LogError(e, "Supertext: translating document {Document} from {Source} to {Target} failed.", documentKey, sourceCulture, target);
            }
        }

        if (changed)
        {
            var saved = contentService.Save(content, userId);
            if (!saved.Success)
            {
                throw new TranslationException("saveFailed", "Umbraco could not save the translated document.", detail: $"{saved.Result}");
            }
        }
        return results;
    }

    /// <returns>number of translated fields</returns>
    private async Task<int> TranslateCultureAsync(IContent content, string source, string target, CancellationToken ct)
    {
        var collected = CollectCulture(content, source);
        var segments = collected.Segments;
        if (segments.Count > 0)
        {
            // The mapping (Settings › Supertext, appsettings wins), an exact Supertext code or the SDK's default,
            // else unsupported_language (nothing is sent).
            var languages = languageSettings.Current();
            var code = LanguageMapping.Target(target, languages);
            var sourceCode = LanguageMapping.Source(source, languages);
            var politeness = LanguageMapping.PolitenessFor(target, languages);
            // One document per language; the SDK splits only above ~900k characters.
            // Translation review on: through Supertext's review service (the whole round trip on that client).
            var translated = await clients.Create(languages.Review(target)).TranslateTextsAsync(segments.Keyed(), code, sourceCode, politeness, cancellationToken: ct);
            foreach (var (key, text) in translated)
            {
                segments.SetResult(key, text);
            }
        }
        return ApplyCulture(content, collected, target);
    }

    /// <summary>The texts of a document culture and how to write their results into another culture.</summary>
    private sealed record CollectedCulture(TranslationSegments Segments, string Name, List<(string Alias, Func<object?> Value)> Writes);

    /// <summary>
    /// Collects the translatable texts of the source culture (the same for API translations and
    /// translation files), keyed by their path: <c>name</c>, the property alias, <c>…/markup</c> for
    /// rich text, <c>…/&lt;block key&gt;/&lt;alias&gt;</c> inside blocks.
    /// </summary>
    private CollectedCulture CollectCulture(IContent content, string source)
    {
        var segments = new TranslationSegments();
        var writes = new List<(string Alias, Func<object?> Value)>();

        var name = content.GetCultureName(source) ?? content.Name ?? string.Empty;
        segments.Add(NameKey, name, false);

        foreach (var property in content.Properties)
        {
            if (!property.PropertyType.VariesByCulture())
            {
                continue; // shared by all languages
            }
            var value = property.GetValue(source);
            var editor = property.PropertyType.PropertyEditorAlias;
            var rebuild = Options.ExcludedProperties.Contains(property.Alias, StringComparer.OrdinalIgnoreCase)
                ? () => value
                : Collect(value, editor, segments, property.Alias);
            writes.Add((property.Alias, rebuild));
        }
        return new CollectedCulture(segments, name, writes);
    }

    /// <summary>Writes the collected values with their results into the target culture (texts without a result keep the source); returns the number of results.</summary>
    private static int ApplyCulture(IContent content, CollectedCulture collected, string target)
    {
        var translatedName = collected.Segments.Result(NameKey);
        content.SetCultureName(string.IsNullOrWhiteSpace(translatedName) ? collected.Name : translatedName, target);
        foreach (var (alias, rebuild) in collected.Writes)
        {
            content.SetValue(alias, rebuild(), target);
        }
        return collected.Segments.TranslatedCount;
    }

    private const string NameKey = "name";

    /// <summary>
    /// Export for translation: the document's texts in the source culture (checked like a translation).
    /// The caller turns them into one job per target culture.
    /// </summary>
    /// <exception cref="TranslationException">documentNotFound, invariantDocumentType, noSourceVersion, nothingToTranslate</exception>
    public TranslationSegments CollectForExport(Guid documentKey, string sourceCulture)
    {
        var content = LoadTranslatable(documentKey, sourceCulture);
        var segments = CollectCulture(content, sourceCulture).Segments;
        if (!segments.FileSegments().Any(s => !string.IsNullOrWhiteSpace(s.Text)))
        {
            throw new TranslationException("nothingToTranslate", "Nothing to translate.");
        }
        return segments;
    }

    /// <summary>
    /// Import translation: one job of a translation file. Collects the document's texts again, imports the
    /// file's translations (<see cref="TranslationFileExchange.Import"/>) and, if any were imported, writes them
    /// into the target culture and saves the document as a draft, like <see cref="TranslateAsync"/>.
    /// An existing target culture is only replaced with <paramref name="overwrite"/>.
    /// </summary>
    /// <exception cref="TranslationException">fileDocumentNotFound, invariantDocumentType, noSourceVersion, noLanguage, saveFailed, file errors</exception>
    public async Task<FileImportResult> ImportAsync(TranslationFile file, Guid documentKey, string sourceCulture, string targetCulture, bool includeChanged, bool overwrite, int userId)
    {
        if (GetDocument(documentKey) is null)
        {
            throw new TranslationException("fileDocumentNotFound", "The document this file was exported from no longer exists.");
        }
        var languages = await languageService.GetAllAsync();
        var target = languages.FirstOrDefault(l => string.Equals(l.IsoCode, targetCulture, StringComparison.OrdinalIgnoreCase))?.IsoCode;
        if (target is null || string.Equals(target, sourceCulture, StringComparison.OrdinalIgnoreCase))
        {
            throw new TranslationException("noLanguage", $"The site has no language {targetCulture}.", [targetCulture]);
        }
        var content = LoadTranslatable(documentKey, sourceCulture);
        if (content.IsCultureAvailable(target) && !overwrite)
        {
            return new FileImportResult(target, TranslationStatus.Exists, null);
        }

        var collected = CollectCulture(content, sourceCulture);
        var counts = TranslationFileExchange.Import(file, collected.Segments, includeChanged);
        if (counts.Imported == 0)
        {
            return new FileImportResult(target, TranslationStatus.Failed, counts);
        }
        ApplyCulture(content, collected, target);
        var saved = contentService.Save(content, userId);
        if (!saved.Success)
        {
            throw new TranslationException("saveFailed", "Umbraco could not save the translated document.", detail: $"{saved.Result}");
        }
        logger.LogInformation("Supertext: imported {Fields} field(s) of document {Document} into {Target} from a translation file.", counts.Imported, documentKey, target);
        return new FileImportResult(target, TranslationStatus.Translated, counts);
    }

    /// <summary>The name of a document (in the culture if it has one), for the import summary.</summary>
    public string? DocumentName(Guid documentKey, string culture)
    {
        var content = GetDocument(documentKey);
        return content is null ? null : content.GetCultureName(culture) ?? content.Name;
    }

    private IContent LoadTranslatable(Guid documentKey, string sourceCulture)
    {
        var content = GetDocument(documentKey) ?? throw new TranslationException("documentNotFound", "Document not found.");
        if (!content.ContentType.VariesByCulture())
        {
            throw new TranslationException("invariantDocumentType", $"The document type \"{content.ContentType.Name}\" does not vary by culture, so it has no separate language versions to translate into.", [content.ContentType.Name ?? content.ContentType.Alias]);
        }
        if (!content.IsCultureAvailable(sourceCulture))
        {
            throw new TranslationException("noSourceVersion", $"The document has no {sourceCulture} version to translate from.", [sourceCulture]);
        }
        return content;
    }

    /// <summary>
    /// Registers the translatable text in a property value and returns a function that
    /// builds the translated value once the results are in.
    /// </summary>
    private Func<object?> Collect(object? value, string editorAlias, TranslationSegments segments, string path)
    {
        if (value is null)
        {
            return () => null;
        }
        if (Options.PlainTextEditors.Contains(editorAlias, StringComparer.OrdinalIgnoreCase))
        {
            return CollectText(value, false, segments, path);
        }
        if (Options.RichTextEditors.Contains(editorAlias, StringComparer.OrdinalIgnoreCase))
        {
            return CollectRichText(value, segments, path);
        }
        if (Options.BlockEditors.Contains(editorAlias, StringComparer.OrdinalIgnoreCase))
        {
            return CollectBlocks(value, segments, path);
        }
        return () => value; // images, toggles, pickers…: copied as they are
    }

    private static Func<object?> CollectText(object value, bool isHtml, TranslationSegments segments, string path)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text) || (!isHtml && NotTranslatable(text)))
        {
            return () => value;
        }
        var key = segments.Add(path, text, isHtml);
        return () => segments.Result(key) ?? text;
    }

    /// <summary>A bare URL, e-mail address, path or number: copied, never translated.</summary>
    private static bool NotTranslatable(string text)
    {
        var t = text.Trim();
        return !t.Any(char.IsLetter)
            || (!t.Contains(' ') && (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith('/')
                || (t.Contains('@') && t.Contains('.'))));
    }

    /// <summary>
    /// Inside blocks Umbraco may hand values over encoded twice: a JSON string whose content is
    /// JSON ("{\u0022markup\u0022: …"). Unwraps that level and returns a function that wraps
    /// the result back the same way.
    /// </summary>
    private static (string Raw, Func<string, object> Wrap) Unwrap(object value)
    {
        var raw = value as string ?? (value as JsonNode)?.ToJsonString() ?? string.Empty;
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith('"'))
        {
            try
            {
                if (JsonSerializer.Deserialize<string>(trimmed) is { } inner)
                {
                    return (inner, result => JsonSerializer.Serialize(result));
                }
            }
            catch (JsonException)
            {
                // not double-encoded after all
            }
        }
        return (raw, result => result);
    }

    /// <summary>Rich text: {"markup": "...", "blocks": {...}} since Umbraco 14, or plain HTML.</summary>
    private Func<object?> CollectRichText(object value, TranslationSegments segments, string path)
    {
        var (raw, wrap) = Unwrap(value);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return () => value;
        }
        if (!raw.TrimStart().StartsWith('{'))
        {
            var html = CollectText(raw, true, segments, path);
            return () => html() is string t ? wrap(t) : value;
        }
        var node = JsonNode.Parse(raw) as JsonObject;
        if (node is null)
        {
            return () => value;
        }
        var markup = node["markup"]?.GetValue<string>();
        var markupKey = string.IsNullOrWhiteSpace(markup) ? null : segments.Add(path + "/markup", markup, true);
        var blocks = node["blocks"] is JsonObject b ? CollectBlockNode(b, segments, path) : null;
        return () =>
        {
            if (markupKey is not null && segments.Result(markupKey) is { } translated)
            {
                node["markup"] = translated;
            }
            blocks?.Invoke();
            return wrap(node.ToJsonString());
        };
    }

    /// <summary>Block list / grid / single block: JSON with contentData[].values[].</summary>
    private Func<object?> CollectBlocks(object value, TranslationSegments segments, string path)
    {
        var (raw, wrap) = Unwrap(value);
        if (string.IsNullOrWhiteSpace(raw) || JsonNode.Parse(raw) is not JsonObject node)
        {
            return () => value;
        }
        var apply = CollectBlockNode(node, segments, path);
        return () =>
        {
            apply();
            return wrap(node.ToJsonString());
        };
    }

    private Action CollectBlockNode(JsonObject node, TranslationSegments segments, string path)
    {
        var applies = new List<Action>();
        // Only content is translated; settingsData holds layout options.
        if (node["contentData"] is not JsonArray items)
        {
            return () => { };
        }
        var position = 0;
        foreach (var item in items.OfType<JsonObject>())
        {
            // The block's key (Umbraco 14+) or UDI keeps the path stable when blocks are reordered.
            var blockId = (item["key"] as JsonValue)?.ToString() ?? (item["udi"] as JsonValue)?.ToString() ?? position.ToString(System.Globalization.CultureInfo.InvariantCulture);
            position++;
            if (!Guid.TryParse(item["contentTypeKey"]?.GetValue<string>(), out var typeKey)
                || contentTypeService.Get(typeKey) is not { } elementType
                || item["values"] is not JsonArray values)
            {
                continue;
            }
            var propertyTypes = elementType.CompositionPropertyTypes.ToDictionary(p => p.Alias, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in values.OfType<JsonObject>())
            {
                var alias = entry["alias"]?.GetValue<string>();
                if (alias is null || !propertyTypes.TryGetValue(alias, out var propertyType)
                    || Options.ExcludedProperties.Contains(alias, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
                var editor = entry["editorAlias"]?.GetValue<string>() ?? propertyType.PropertyEditorAlias;
                var inner = entry["value"];
                // The arms are cast to object on purpose: JsonNode converts implicitly from
                // string, so without the casts the switch is typed JsonNode and the string
                // turns back into a JSON value.
                object? innerValue = inner switch
                {
                    JsonValue v when v.GetValueKind() == JsonValueKind.String => (object?)v.GetValue<string>(),
                    JsonObject or JsonArray => (object?)inner,
                    _ => null,
                };
                if (innerValue is null)
                {
                    continue;
                }
                var rebuild = Collect(innerValue, editor, segments, path + "/" + blockId + "/" + alias);
                var wasString = innerValue is string;
                applies.Add(() =>
                {
                    var result = rebuild();
                    entry["value"] = result switch
                    {
                        null => null,
                        string s when wasString => JsonValue.Create(s),
                        string s => JsonNode.Parse(s),
                        JsonNode n => n.DeepClone(),
                        _ => JsonValue.Create(result.ToString()),
                    };
                });
            }
        }
        return () => applies.ForEach(a => a());
    }

    /// <summary>Languages a document can be translated into, for the dialog.</summary>
    public async Task<IReadOnlyList<(string IsoCode, string Name, bool IsDefault, bool Exists)>> GetLanguagesAsync(Guid documentKey)
    {
        var content = GetDocument(documentKey) ?? throw new TranslationException("documentNotFound", "Document not found.");
        var languages = await languageService.GetAllAsync();
        var settings = languageSettings.Current();
        return languages
            .Where(l => settings.Enabled(l.IsoCode))
            .Select(l => (l.IsoCode, l.CultureName, l.IsDefault, content.IsCultureAvailable(l.IsoCode)))
            .ToList();
    }
}
