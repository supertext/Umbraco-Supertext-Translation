namespace Supertext.Umbraco.Translation.Configuration;

/// <summary>
/// Settings from the "Supertext" section of appsettings.json. The environment
/// variables SUPERTEXT_API_KEY and SUPERTEXT_API_ENDPOINT win over the file.
/// </summary>
public sealed class SupertextOptions
{
    public const string SectionName = "Supertext";

    /// <summary>
    /// Supertext API key; shown by Supertext as "Supertext-Auth-Key &lt;key&gt;", either form works.
    /// Create an account at https://www.supertext.com/person/en/account/signin and generate the key at
    /// https://www.supertext.com/en/integrations/api (supertext.com → Integrations → API; requires the Admin role).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>API base URL. Live: https://api.supertext.com/v1/ (also api.staging… / api.testing…).</summary>
    public string Endpoint { get; set; } = "https://api.supertext.com/v1/";

    /// <summary>
    /// Supertext's review service for languages with <i>Translation review</i> on (Settings › Supertext):
    /// translations into them go there instead of <see cref="Endpoint"/>. Empty turns review off for every
    /// language. The environment variable SUPERTEXT_REVIEW_URL wins (an empty one keeps this value).
    /// </summary>
    public string ReviewUrl { get; set; } = Api.SupertextClientFactory.ReviewUrl;

    /// <summary>Seconds between status checks.</summary>
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>Maximum seconds to wait for one translated document.</summary>
    public int PollTimeoutSeconds { get; set; } = 240;

    /// <summary>
    /// "Send version information to Supertext" (Commando Center): site URL, package, Umbraco, .NET
    /// and SDK versions and the API key check result, never content, the key or personal data.
    /// The environment variable SUPERTEXT_VERSION_REPORTS (0/false/off or 1/true/on) wins.
    /// </summary>
    public bool SendVersionReports { get; set; } = true;

    /// <summary>
    /// Per-language settings keyed by Umbraco culture (ISO code, e.g. "de-CH"). <c>Code</c> is the
    /// language mapping: without it, a culture that is itself a Supertext code (de-CH, es, apart from
    /// case and _/-) is used as it is, else the SDK's default target where one is obvious (de → de-DE, es-ES → es),
    /// and any other culture can't be translated.
    /// </summary>
    public Dictionary<string, SupertextLanguageOptions> Languages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Property editors whose string values are translated as plain text.</summary>
    public List<string> PlainTextEditors { get; set; } = ["Umbraco.TextBox", "Umbraco.TextArea"];

    /// <summary>Property editors whose values are translated as HTML (rich text, including its blocks).</summary>
    public List<string> RichTextEditors { get; set; } = ["Umbraco.RichText", "Umbraco.TinyMCE"];

    /// <summary>Block editors whose blocks are searched for translatable properties.</summary>
    public List<string> BlockEditors { get; set; } = ["Umbraco.BlockList", "Umbraco.BlockGrid", "Umbraco.SingleBlock"];

    /// <summary>Property aliases that are never translated (copied as they are).</summary>
    public List<string> ExcludedProperties { get; set; } = [];

    /// <summary>The language mapping (Umbraco culture → Supertext code) from <c>Languages.&lt;culture&gt;.Code</c>.</summary>
    internal IReadOnlyDictionary<string, string> LanguageMapping =>
        Languages.Where(l => !string.IsNullOrWhiteSpace(l.Value.Code))
            .ToDictionary(l => l.Key, l => l.Value.Code!.Trim(), StringComparer.OrdinalIgnoreCase);

    internal string EffectiveApiKey
    {
        get
        {
            var fromEnv = Environment.GetEnvironmentVariable("SUPERTEXT_API_KEY");
            return (string.IsNullOrWhiteSpace(fromEnv) ? ApiKey : fromEnv).Trim();
        }
    }

    /// <summary>The review service's base URL (with a trailing slash), or null when review is off (empty, or the same as the API).</summary>
    internal string? EffectiveReviewUrl
    {
        get
        {
            var fromEnv = Environment.GetEnvironmentVariable("SUPERTEXT_REVIEW_URL");
            var url = (string.IsNullOrWhiteSpace(fromEnv) ? ReviewUrl : fromEnv).Trim();
            if (url == string.Empty)
            {
                return null;
            }
            url = url.TrimEnd('/') + "/";
            return string.Equals(url, EffectiveEndpoint, StringComparison.OrdinalIgnoreCase) ? null : url;
        }
    }

    internal string EffectiveEndpoint
    {
        get
        {
            var fromEnv = Environment.GetEnvironmentVariable("SUPERTEXT_API_ENDPOINT");
            var url = string.IsNullOrWhiteSpace(fromEnv) ? Endpoint : fromEnv.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                url = "https://api.supertext.com/v1/";
            }
            return url.TrimEnd('/') + "/";
        }
    }
}

public sealed class SupertextLanguageOptions
{
    /// <summary>
    /// Supertext code for this culture, e.g. "de-CH" for the culture "de" (one of Supertext's codes, see
    /// <c>Languages.Targets</c> of the SDK). Default: the culture itself if it is a Supertext code. As source,
    /// its source form is sent (de-CH → de).
    /// </summary>
    public string? Code { get; set; }

    /// <summary>"more" (formal: Sie/vous/Lei), "less" (informal: du/tu) or "default". Not every target offers it (English, Swiss German, Romansh, Albanian, Serbian don't).</summary>
    public string? Politeness { get; set; }

    /// <summary>
    /// <i>Translation review</i>: true sends translations into this culture through Supertext's review service
    /// (<see cref="SupertextOptions.ReviewUrl"/>), false directly to Supertext. Not set: the checkbox on Settings › Supertext.
    /// </summary>
    public bool? Review { get; set; }

    /// <summary>False hides the language from the translate dialog.</summary>
    public bool Enabled { get; set; } = true;
}
