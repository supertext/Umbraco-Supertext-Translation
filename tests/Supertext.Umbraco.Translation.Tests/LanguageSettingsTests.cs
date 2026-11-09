using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Services;
using Xunit;

namespace Supertext.Umbraco.Translation.Tests;

/// <summary>The mapping saved on Settings › Supertext, appsettings.json over it, and the form's checks.</summary>
public class LanguageSettingsTests
{
    private static Dictionary<string, SavedLanguage> Saved(params (string Culture, string? Code, string? Politeness)[] rows) =>
        rows.ToDictionary(r => r.Culture, r => new SavedLanguage { Code = r.Code, Politeness = r.Politeness }, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void TheSavedMappingIsUsedInBothDirections()
    {
        var settings = new LanguageSettings(new SupertextOptions(), Saved(("de", "de-CH", "more"), ("en-IE", "en-GB", null)));
        Assert.Equal("de-CH", LanguageMapping.Target("de", settings));
        Assert.Equal("de", LanguageMapping.Source("de", settings));
        Assert.Equal("en-GB", LanguageMapping.Target("en_IE", settings));
        Assert.Equal(Politeness.More, LanguageMapping.PolitenessFor("de", settings));
        var resolved = LanguageMapping.Resolve("de", settings);
        Assert.True(resolved.Mapped);
        Assert.False(resolved.FromConfig);
        Assert.Equal("de-CH", settings.SavedCode("de"));
        Assert.Null(settings.ConfiguredCode("de"));
    }

    [Fact]
    public void AppsettingsWinsOverTheScreenPerValue()
    {
        var options = new SupertextOptions();
        options.Languages["de"] = new SupertextLanguageOptions { Code = "de-AT" };
        options.Languages["fr-CH"] = new SupertextLanguageOptions { Politeness = "less", Enabled = false };
        var settings = new LanguageSettings(options, Saved(("de", "de-CH", "more"), ("fr-CH", "fr-FR", "more")));

        Assert.Equal("de-AT", LanguageMapping.Target("de", settings));
        Assert.Equal(Politeness.More, LanguageMapping.PolitenessFor("de", settings)); // tone from the screen
        Assert.True(LanguageMapping.Resolve("de", settings).FromConfig);
        Assert.Equal("fr-FR", LanguageMapping.Target("fr-CH", settings));          // code from the screen
        Assert.Equal(Politeness.Less, LanguageMapping.PolitenessFor("fr-CH", settings));
        Assert.Equal("less", settings.ConfiguredPoliteness("fr-CH"));
        Assert.False(settings.Enabled("fr-CH"));
        Assert.True(settings.Enabled("de"));
    }

    [Fact]
    public void WithoutAnythingSavedTheSdkRulesApply()
    {
        var settings = new LanguageSettings(new SupertextOptions(), Saved());
        Assert.Equal("de-DE", LanguageMapping.Target("de", settings));
        Assert.True(LanguageMapping.Resolve("de", settings).FromDefault);
        var e = Assert.Throws<SupertextException>(() => LanguageMapping.Target("gsw", settings));
        Assert.Equal(SupertextErrorCodes.UnsupportedLanguage, e.Code);
    }

    [Fact]
    public void TheFormStoresExactCodesAndKeepsOtherLanguages()
    {
        var saved = LanguageForm.ToSaved(
            [new LanguageInput("de", "de_ch", "more"), new LanguageInput("en-US", "en-US", "more"), new LanguageInput("gsw", "", null)],
            ["en-US", "de", "gsw"],
            Saved(("it", "it-CH", null)));

        Assert.Equal("de-CH", saved["de"].Code);
        Assert.Equal("more", saved["de"].Politeness);
        Assert.Null(saved["en-US"].Politeness);   // English has no formal/informal
        Assert.Null(saved["gsw"].Code);
        Assert.Equal("it-CH", saved["it"].Code);    // not submitted: kept

        var json = KeyValueLanguageSettingsStore.Serialize(saved);
        var back = KeyValueLanguageSettingsStore.Parse(json);
        Assert.Equal("de-CH", back["DE"].Code);
        Assert.Empty(KeyValueLanguageSettingsStore.Parse("not json"));
        Assert.Empty(KeyValueLanguageSettingsStore.Parse(null));
    }

    [Fact]
    public void TheFormRefusesUnknownLanguagesAndCodes()
    {
        Assert.Throws<ArgumentException>(() => LanguageForm.ToSaved([new LanguageInput("xx", "de-CH", null)], ["de"], Saved()));
        Assert.Throws<ArgumentException>(() => LanguageForm.ToSaved([new LanguageInput("de", "de-LI", null)], ["de"], Saved()));
        Assert.Throws<ArgumentException>(() => LanguageForm.ToSaved([new LanguageInput("es-ES", "es-ES", null)], ["es-ES"], Saved()));
    }
}
