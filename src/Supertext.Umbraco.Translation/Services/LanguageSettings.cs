using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Supertext.Umbraco.Translation.Configuration;
using Umbraco.Cms.Core.Services;

namespace Supertext.Umbraco.Translation.Services;

/// <summary>What an administrator chose for one Umbraco culture on Settings › Supertext.</summary>
public sealed class SavedLanguage
{
    /// <summary>The Supertext target code (one of the SDK's <c>Languages.Targets</c>), or null: not chosen.</summary>
    public string? Code { get; set; }

    /// <summary>"more", "less" or null/"default".</summary>
    public string? Politeness { get; set; }

    /// <summary><i>Translation review</i>: translations into this culture go through Supertext's review service.</summary>
    public bool Review { get; set; }
}

/// <summary>Where the language settings saved on Settings › Supertext are kept.</summary>
public interface ILanguageSettingsStore
{
    /// <summary>The saved settings per culture (empty when nothing was saved yet).</summary>
    IReadOnlyDictionary<string, SavedLanguage> Load();

    void Save(IReadOnlyDictionary<string, SavedLanguage> languages);
}

/// <summary>Keeps the saved language settings in Umbraco's key-value table (<c>umbracoKeyValue</c>), one row of JSON.</summary>
public sealed class KeyValueLanguageSettingsStore(IKeyValueService keyValueService) : ILanguageSettingsStore
{
    public const string Key = "Supertext.Umbraco.Translation.Languages";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public IReadOnlyDictionary<string, SavedLanguage> Load() => Parse(keyValueService.GetValue(Key));

    public void Save(IReadOnlyDictionary<string, SavedLanguage> languages) => keyValueService.SetValue(Key, Serialize(languages));

    public static string Serialize(IReadOnlyDictionary<string, SavedLanguage> languages) => JsonSerializer.Serialize(languages, Json);

    /// <summary>The stored JSON; empty for nothing stored or something unreadable.</summary>
    public static IReadOnlyDictionary<string, SavedLanguage> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, SavedLanguage>(StringComparer.OrdinalIgnoreCase);
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, SavedLanguage>>(json, Json);
            return parsed is null
                ? new Dictionary<string, SavedLanguage>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, SavedLanguage>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, SavedLanguage>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

/// <summary>
/// The language settings in effect: what was saved on Settings › Supertext, overridden per culture and
/// per value by <c>Supertext:Languages:&lt;culture&gt;</c> in appsettings.json (for automated setups).
/// Cultures are compared apart from case and <c>_</c>/<c>-</c>.
/// </summary>
public sealed class LanguageSettings
{
    private readonly Dictionary<string, SupertextLanguageOptions> _configured = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SavedLanguage> _saved = new(StringComparer.OrdinalIgnoreCase);

    public LanguageSettings(SupertextOptions options, IReadOnlyDictionary<string, SavedLanguage>? saved = null)
    {
        foreach (var (culture, entry) in options.Languages)
        {
            _configured[Key(culture)] = entry;
        }
        foreach (var (culture, entry) in saved ?? new Dictionary<string, SavedLanguage>())
        {
            _saved[Key(culture)] = entry;
        }
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, entry) in _saved)
        {
            if (!string.IsNullOrWhiteSpace(entry.Code))
            {
                mapping[key] = entry.Code.Trim();
            }
        }
        foreach (var (key, entry) in _configured)
        {
            if (!string.IsNullOrWhiteSpace(entry.Code))
            {
                mapping[key] = entry.Code.Trim();
            }
        }
        Mapping = mapping;
    }

    /// <summary>Culture → Supertext code (saved, then appsettings; keys with <c>-</c>), for the SDK's <c>Languages.Target/Source</c>.</summary>
    public IReadOnlyDictionary<string, string> Mapping { get; }

    /// <summary>The code from appsettings.json (it wins over the screen), or null.</summary>
    public string? ConfiguredCode(string culture) => Blank(Configured(culture)?.Code);

    /// <summary>The code saved on Settings › Supertext, or null.</summary>
    public string? SavedCode(string culture) => Blank(Saved(culture)?.Code);

    /// <summary>The tone from appsettings.json (it wins over the screen), or null.</summary>
    public string? ConfiguredPoliteness(string culture) => Blank(Configured(culture)?.Politeness);

    /// <summary>"more", "less" or "default": appsettings, else the screen, else default.</summary>
    public string Politeness(string culture) =>
        Normalize(ConfiguredPoliteness(culture) ?? Saved(culture)?.Politeness);

    /// <summary><i>Translation review</i> from appsettings.json (it wins over the screen), or null.</summary>
    public bool? ConfiguredReview(string culture) => Configured(culture)?.Review;

    /// <summary><i>Translation review</i>: appsettings, else the screen, else off.</summary>
    public bool Review(string culture) => ConfiguredReview(culture) ?? Saved(culture)?.Review ?? false;

    /// <summary>False hides the language from the dialog (<c>Supertext:Languages:&lt;culture&gt;:Enabled</c>).</summary>
    public bool Enabled(string culture) => Configured(culture)?.Enabled ?? true;

    internal static string Normalize(string? politeness) => (politeness ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "more" => "more",
        "less" => "less",
        _ => "default",
    };

    internal static string Key(string culture) => culture.Trim().Replace('_', '-');

    private SupertextLanguageOptions? Configured(string culture) => _configured.GetValueOrDefault(Key(culture));

    private SavedLanguage? Saved(string culture) => _saved.GetValueOrDefault(Key(culture));

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>The current <see cref="LanguageSettings"/>: appsettings (live) over the saved settings.</summary>
public sealed class LanguageSettingsProvider(IOptionsMonitor<SupertextOptions> options, ILanguageSettingsStore store)
{
    public LanguageSettings Current() => new(options.CurrentValue, store.Load());

    /// <summary>What is saved (without the appsettings overrides).</summary>
    public IReadOnlyDictionary<string, SavedLanguage> Stored() => store.Load();

    public void Save(IReadOnlyDictionary<string, SavedLanguage> languages) => store.Save(languages);
}

/// <summary>One row of the language form on Settings › Supertext.</summary>
/// <param name="Code">the chosen Supertext target code; empty: none chosen (the culture's exact match or default applies)</param>
/// <param name="Politeness">more, less or default</param>
/// <param name="Review">Translation review on</param>
public sealed record LanguageInput(string IsoCode, string? Code, string? Politeness, bool Review = false);

/// <summary>Checks and converts what the language form sends (Umbraco-free, unit-tested).</summary>
public static class LanguageForm
{
    /// <summary>
    /// The settings to save: <paramref name="previous"/> with the submitted rows replaced. Codes must be
    /// Supertext target codes (stored in their exact form, e.g. <c>de_ch</c> → <c>de-CH</c>); a tone is kept
    /// only where the language's code (chosen, else its suggestion) offers formal/informal.
    /// </summary>
    /// <exception cref="ArgumentException">an unknown Umbraco language or a code Supertext doesn't offer</exception>
    public static Dictionary<string, SavedLanguage> ToSaved(
        IEnumerable<LanguageInput> rows, IEnumerable<string> cultures, IReadOnlyDictionary<string, SavedLanguage> previous)
    {
        var known = cultures.ToDictionary(LanguageSettings.Key, c => c, StringComparer.OrdinalIgnoreCase);
        var saved = new Dictionary<string, SavedLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var (culture, entry) in previous)
        {
            saved[LanguageSettings.Key(culture)] = entry;
        }
        foreach (var row in rows)
        {
            if (!known.TryGetValue(LanguageSettings.Key(row.IsoCode ?? string.Empty), out var culture))
            {
                throw new ArgumentException($"The site has no language {row.IsoCode}.");
            }
            string? code = null;
            if (!string.IsNullOrWhiteSpace(row.Code))
            {
                code = Sdk.V1.Languages.MatchTarget(row.Code) ?? throw new ArgumentException($"Supertext offers no target language {row.Code}.");
            }
            var politeness = LanguageSettings.Normalize(row.Politeness);
            var effective = code ?? Sdk.V1.Languages.SuggestTarget(culture);
            if (politeness == "default" || effective is null || Sdk.V1.Languages.SupportsPoliteness(effective) != true)
            {
                politeness = "default";
            }
            saved[LanguageSettings.Key(culture)] = new SavedLanguage
            {
                Code = code,
                Politeness = politeness == "default" ? null : politeness,
                Review = row.Review,
            };
        }
        return saved;
    }
}
