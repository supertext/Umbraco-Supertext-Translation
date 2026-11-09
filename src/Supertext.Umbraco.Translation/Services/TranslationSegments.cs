using System.Globalization;
using Supertext.Umbraco.Translation.Sdk.Files;
using Segment = Supertext.Umbraco.Translation.Sdk.V1.Segment;

namespace Supertext.Umbraco.Translation.Services;

/// <summary>
/// The translatable texts of one document culture, each under a stable key (its path in the document:
/// <c>name</c>, <c>&lt;alias&gt;</c>, <c>&lt;alias&gt;/markup</c>, <c>&lt;alias&gt;/&lt;block key&gt;/&lt;alias&gt;</c>),
/// and the results once they are in (from the API or an imported file). No Umbraco dependency.
/// </summary>
public sealed class TranslationSegments
{
    private readonly List<(string Key, Segment Segment)> _segments = [];
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _results = new(StringComparer.Ordinal);

    public int Count => _segments.Count;

    /// <summary>Number of texts with a result.</summary>
    public int TranslatedCount => _results.Count;

    /// <summary>Adds a text; returns its key (the path, made unique with <c>#2</c>, <c>#3</c>… if needed).</summary>
    public string Add(string path, string text, bool isHtml)
    {
        var key = path;
        for (var n = 2; !_keys.Add(key); n++)
        {
            key = path + "#" + n.ToString(CultureInfo.InvariantCulture);
        }
        _segments.Add((key, new Segment(text, isHtml)));
        return key;
    }

    /// <summary>Stores a result; blank results are ignored (the source text stays).</summary>
    public void SetResult(string key, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            _results[key] = text;
        }
    }

    public string? Result(string key) => _results.TryGetValue(key, out var t) ? t : null;

    /// <summary>The texts by key, for the SDK client's <c>TranslateTextsAsync</c>.</summary>
    public IEnumerable<KeyValuePair<string, Segment>> Keyed() => _segments.Select(s => KeyValuePair.Create(s.Key, s.Segment));

    /// <summary>The texts as the SDK's translation file segments (same keys).</summary>
    public IReadOnlyList<FileSegment> FileSegments() => _segments.Select(s => new FileSegment(s.Key, s.Segment.Text, s.Segment.Html)).ToList();
}
