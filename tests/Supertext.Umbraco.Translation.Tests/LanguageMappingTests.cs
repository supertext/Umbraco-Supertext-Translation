using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Services;
using Xunit;

namespace Supertext.Umbraco.Translation.Tests;

/// <summary>Umbraco cultures → Supertext codes: the mapping, else an exact Supertext code, else the SDK's default, else nothing.</summary>
public class LanguageMappingTests
{
    private static SupertextOptions With(params (string Culture, string? Code, string? Politeness)[] languages)
    {
        var options = new SupertextOptions();
        foreach (var (culture, code, politeness) in languages)
        {
            options.Languages[culture] = new SupertextLanguageOptions { Code = code, Politeness = politeness };
        }
        return options;
    }

    [Theory]
    [InlineData("de-CH", "de-CH")]
    [InlineData("de_ch", "de-CH")]
    [InlineData("es", "es")]
    [InlineData("zh-hans", "zh-Hans")]
    public void CulturesThatAreSupertextCodesAreUsedAsTheyAre(string culture, string expected) =>
        Assert.Equal(expected, LanguageMapping.Target(culture, new SupertextOptions()));

    [Theory]
    [InlineData("de", "de-DE")]       // the SDK's default targets (SDK 0.4.0)
    [InlineData("es-ES", "es")]       // a region of a bare target
    [InlineData("zh-CN", "zh-Hans")]
    public void ObviousDefaultsAreUsed(string culture, string expected)
    {
        Assert.Equal(expected, LanguageMapping.Target(culture, new SupertextOptions()));
        var resolved = LanguageMapping.Resolve(culture, new SupertextOptions());
        Assert.Equal(expected, resolved.Code);
        Assert.True(resolved.FromDefault);
        Assert.False(resolved.Mapped);
        Assert.False(LanguageMapping.Resolve("de-CH", new SupertextOptions()).FromDefault);
    }

    [Theory]
    [InlineData("en-IE")]   // no obvious default: needs a mapping
    [InlineData("de-LI")]
    [InlineData("gsw")]
    public void OtherCulturesAreNotGuessed(string culture)
    {
        var e = Assert.Throws<SupertextException>(() => LanguageMapping.Target(culture, new SupertextOptions()));
        Assert.Equal(SupertextErrorCodes.UnsupportedLanguage, e.Code);
        Assert.Equal(culture, e.TargetLang);
        Assert.Null(LanguageMapping.Resolve(culture, new SupertextOptions()).Code);
    }

    [Fact]
    public void TheMappingWinsInBothDirections()
    {
        var options = With(("de", "de-CH", null), ("es-ES", "es", null), ("en-US", " en-US ", null));
        Assert.Equal("de-CH", LanguageMapping.Target("de", options));
        Assert.Equal("es", LanguageMapping.Target("es-ES", options));
        Assert.Equal("de", LanguageMapping.Source("de", options));      // de-CH in its source form
        Assert.Equal("en", LanguageMapping.Source("en-US", options));
        var resolved = LanguageMapping.Resolve("de", options);
        Assert.Equal("de-CH", resolved.Code);
        Assert.True(resolved.Mapped);
        Assert.False(LanguageMapping.Resolve("fr-CH", options).Mapped);
    }

    [Fact]
    public void EntriesWithoutCodeAreNoMapping()
    {
        var options = With(("de", null, "more"), ("fr-CH", "  ", null));
        Assert.Equal("de-DE", LanguageMapping.Target("de", options)); // no code: the default
        Assert.Equal("fr-CH", LanguageMapping.Target("fr-CH", options));
        Assert.Equal(Politeness.More, LanguageMapping.PolitenessFor("de", options));
    }

    [Fact]
    public void SourcesWithoutSupertextLanguageAreRejected()
    {
        var e = Assert.Throws<SupertextException>(() => LanguageMapping.Source("xx-YY", new SupertextOptions()));
        Assert.Equal(SupertextErrorCodes.UnsupportedLanguage, e.Code);
        Assert.Equal("en", LanguageMapping.Source("en-GB", new SupertextOptions()));
    }

    [Fact]
    public void ToneIsShownWithWhetherSupertextOffersIt()
    {
        var options = With(("de-CH", null, "more"), ("en-US", null, "less"), ("it-CH", null, "nonsense"));
        Assert.Equal(new LanguageResolution("de-CH", false, "more", true), LanguageMapping.Resolve("de-CH", options));
        Assert.Equal(new LanguageResolution("en-US", false, "less", false), LanguageMapping.Resolve("en-US", options));
        Assert.Equal("default", LanguageMapping.Resolve("it-CH", options).Politeness);
        // Sent as configured: Supertext answers unsupported_feature for en-US.
        Assert.Equal(Politeness.Less, LanguageMapping.PolitenessFor("en_us", options));
    }
}
