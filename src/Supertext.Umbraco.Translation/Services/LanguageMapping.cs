using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.Sdk.V1;

namespace Supertext.Umbraco.Translation.Services;

/// <summary>What the package sends to Supertext for one Umbraco culture (for the Supertext dashboard and the dialog).</summary>
/// <param name="Code">The Supertext target code, or null when the culture has no Supertext language (it can't be translated).</param>
/// <param name="Mapped">True when the code comes from the mapping (Settings › Supertext or appsettings), false for an exact match or a default.</param>
/// <param name="Politeness">"more", "less" or "default", as configured.</param>
/// <param name="PolitenessSupported">Whether Supertext offers formal/informal for the code (null: a code the SDK doesn't list).</param>
/// <param name="FromDefault">True when the code is the SDK's default target for the culture (de → de-DE), not a mapping or an exact match.</param>
/// <param name="FromConfig">True when the code comes from <c>Supertext:Languages:&lt;culture&gt;:Code</c> in appsettings.json (it wins over the screen).</param>
public sealed record LanguageResolution(string? Code, bool Mapped, string Politeness, bool? PolitenessSupported, bool FromDefault = false, bool FromConfig = false);

/// <summary>
/// Umbraco cultures → Supertext codes, with the Supertext SDK's rules (the same in every Supertext plugin):
/// the mapping (the combo box on Settings › Supertext, overridden by <c>Supertext:Languages:&lt;culture&gt;:Code</c>)
/// wins; else a culture that is a Supertext code apart from case and <c>_</c>/<c>-</c> (de-CH, es); else the
/// SDK's default target where one is obvious (de → de-DE, es-ES → es, zh-CN → zh-Hans); else the SDK throws
/// <c>unsupported_language</c> (en-IE, de-LI, gsw) and nothing is sent. One mapping serves both directions:
/// as source, a mapped code is sent in its source form (de-CH → de).
/// </summary>
public static class LanguageMapping
{
    /// <exception cref="SupertextException"><c>unsupported_language</c> when the culture isn't mapped, isn't a Supertext code and has no default.</exception>
    public static string Target(string culture, LanguageSettings settings) => Languages.Target(culture, settings.Mapping);

    /// <exception cref="SupertextException"><c>unsupported_language</c> when the culture isn't mapped and has no Supertext source language.</exception>
    public static string Source(string culture, LanguageSettings settings) => Languages.Source(culture, settings.Mapping);

    /// <summary>The configured tone for a target culture; sent as configured (Supertext answers <c>unsupported_feature</c> where it has none).</summary>
    public static Politeness PolitenessFor(string culture, LanguageSettings settings) => settings.Politeness(culture) switch
    {
        "more" => Politeness.More,
        "less" => Politeness.Less,
        _ => Politeness.Default,
    };

    public static LanguageResolution Resolve(string culture, LanguageSettings settings)
    {
        var mapping = settings.Mapping;
        string? code;
        try
        {
            code = Languages.Target(culture, mapping);
        }
        catch (SupertextException)
        {
            code = null;
        }
        var mapped = code is not null && mapping.Keys.Any(k => Same(k, culture));
        var fromDefault = code is not null && !mapped && Languages.MatchTarget(culture) is null;
        var fromConfig = code is not null && settings.ConfiguredCode(culture) is not null;
        return new LanguageResolution(code, mapped, settings.Politeness(culture), code is null ? null : Languages.SupportsPoliteness(code), fromDefault, fromConfig);
    }

    // appsettings.json only (tests, code that has no saved settings).
    public static string Target(string culture, SupertextOptions options) => Target(culture, new LanguageSettings(options));

    public static string Source(string culture, SupertextOptions options) => Source(culture, new LanguageSettings(options));

    public static Politeness PolitenessFor(string culture, SupertextOptions options) => PolitenessFor(culture, new LanguageSettings(options));

    public static LanguageResolution Resolve(string culture, SupertextOptions options) => Resolve(culture, new LanguageSettings(options));

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim().Replace('_', '-'), b.Trim().Replace('_', '-'), StringComparison.OrdinalIgnoreCase);
}
