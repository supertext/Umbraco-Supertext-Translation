using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.PluginHealth;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Services;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;

namespace Supertext.Umbraco.Translation.Controllers;

/// <summary>
/// Backoffice API of the Supertext dashboard (Settings section): the configuration as the package sees it,
/// the latest API key check, the version reports setting and the language mapping. The language mapping
/// (Supertext language and tone per Umbraco language) is saved here (<c>PUT settings/languages</c>);
/// <c>Supertext:Languages</c> in appsettings.json overrides it. Other settings live in appsettings.json / the environment.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("supertext")]
[ApiExplorerSettings(GroupName = "Supertext")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public sealed partial class SupertextSettingsController(
    SupertextClientFactory clients,
    SupertextReporting reporting,
    ILanguageService languageService,
    LanguageSettingsProvider languageSettings,
    IOptionsMonitor<WebRoutingSettings> webRouting) : ManagementApiControllerBase
{
    public const string ReleasesUrl = "https://github.com/Supertext/Umbraco-Supertext-Translation/releases/tag/v";

    /// <param name="Status">ok, invalid, missing, unreachable or error</param>
    /// <param name="CheckedAt">ISO 8601</param>
    public sealed record KeyCheckModel(string Status, string CheckedAt, int? HttpStatus, string? ErrorCode);

    /// <param name="Enabled">whether reports are sent</param>
    /// <param name="FromEnvironment">true when SUPERTEXT_VERSION_REPORTS decides (else Supertext:SendVersionReports)</param>
    /// <param name="PingUrl">the public ping route (null while reports are off or the site URL is unknown)</param>
    public sealed record ReportsModel(bool Enabled, bool FromEnvironment, string? PingUrl);

    /// <param name="SupertextCode">the code it is sent as; null: no Supertext language (can't be translated)</param>
    /// <param name="Mapped">the code comes from the mapping (this screen or appsettings)</param>
    /// <param name="Politeness">more, less or default</param>
    /// <param name="PolitenessSupported">false: Supertext offers no formal/informal for the code</param>
    /// <param name="Enabled">false: hidden in the dialog</param>
    /// <param name="FromDefault">the code is Supertext's default for the culture (de → de-DE)</param>
    /// <param name="SavedCode">the code chosen on this screen (null: none)</param>
    /// <param name="SuggestedCode">the SDK's suggestion (exact match, else default; null: none), preselected while nothing is saved</param>
    /// <param name="ConfiguredCode">Supertext:Languages:&lt;culture&gt;:Code from appsettings.json; wins over the screen</param>
    /// <param name="ConfiguredPoliteness">Supertext:Languages:&lt;culture&gt;:Politeness from appsettings.json; wins over the screen</param>
    /// <param name="Review">Translation review on (translations into it go through the review service while ReviewUrl is set)</param>
    /// <param name="ConfiguredReview">Supertext:Languages:&lt;culture&gt;:Review from appsettings.json; wins over the screen</param>
    public sealed record LanguageModel(
        string IsoCode, string Name, bool IsDefault, string? SupertextCode, bool Mapped, string Politeness, bool? PolitenessSupported,
        bool Enabled, bool FromDefault, string? SavedCode, string? SuggestedCode, string? ConfiguredCode, string? ConfiguredPoliteness,
        bool Review, bool? ConfiguredReview);

    /// <param name="Politeness">Supertext offers formal/informal for it</param>
    public sealed record TargetModel(string Code, string Name, bool Politeness);

    public sealed record SaveLanguagesRequest(LanguageInput[] Languages);

    /// <param name="ReleaseUrl">GitHub release of this version (X.Y.Z versions only)</param>
    /// <param name="ReviewUrl">the review service for languages with Translation review on; null: review is off (Supertext:ReviewUrl empty)</param>
    public sealed record SettingsModel(
        string Version,
        string? ReleaseUrl,
        string? UmbracoVersion,
        string SdkVersion,
        bool HasApiKey,
        string Endpoint,
        string? ReviewUrl,
        KeyCheckModel? KeyCheck,
        ReportsModel Reports,
        IEnumerable<LanguageModel> Languages,
        IEnumerable<TargetModel> Targets);

    [HttpGet("settings")]
    [ProducesResponseType<SettingsModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Settings()
    {
        var siteUrl = SiteUrl();
        reporting.InBackground(ReportAction.OnRequest, siteUrl);
        var languages = await LanguagesAsync();
        var version = reporting.Installation.PackageVersion;
        return Ok(new SettingsModel(
            version,
            ReleaseVersion().IsMatch(version) ? ReleasesUrl + version : null,
            reporting.Installation.UmbracoVersion,
            Sdk.SdkInfo.Version,
            clients.HasApiKey,
            clients.Endpoint,
            clients.ReviewEndpoint,
            ToModel(reporting.LastCheck()),
            Reports(siteUrl),
            languages,
            Languages.TargetOptions().Select(t => new TargetModel(t.Code, t.Name, t.Politeness))));
    }

    /// <summary>Saves the language mapping (Supertext language and tone per Umbraco language); answers with the languages as they are now.</summary>
    [HttpPut("settings/languages")]
    [ProducesResponseType<IEnumerable<LanguageModel>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveLanguages(SaveLanguagesRequest request)
    {
        var cultures = (await languageService.GetAllAsync()).Select(l => l.IsoCode).ToList();
        try
        {
            var saved = LanguageForm.ToSaved(request.Languages ?? [], cultures, languageSettings.Stored());
            languageSettings.Save(saved);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new ProblemDetails { Title = e.Message, Status = StatusCodes.Status400BadRequest });
        }
        return Ok(await LanguagesAsync());
    }

    private async Task<List<LanguageModel>> LanguagesAsync()
    {
        var settings = languageSettings.Current();
        return (await languageService.GetAllAsync()).Select(l =>
        {
            var r = LanguageMapping.Resolve(l.IsoCode, settings);
            return new LanguageModel(
                l.IsoCode, l.CultureName, l.IsDefault, r.Code, r.Mapped, r.Politeness, r.PolitenessSupported, settings.Enabled(l.IsoCode), r.FromDefault,
                settings.SavedCode(l.IsoCode), Languages.SuggestTarget(l.IsoCode), settings.ConfiguredCode(l.IsoCode), settings.ConfiguredPoliteness(l.IsoCode),
                settings.Review(l.IsoCode), settings.ConfiguredReview(l.IsoCode));
        }).ToList();
    }

    /// <summary>"Check API key": the SDK's cost-free key check; stored, shown, and reported when its status changed.</summary>
    [HttpPost("settings/check-key")]
    [ProducesResponseType<KeyCheckModel>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckKey(CancellationToken ct)
    {
        var result = await reporting.RunAsync(ReportAction.TestConnection, SiteUrl());
        if (result?.Check is { } check)
        {
            return Ok(ToModel(check));
        }
        var sdkCheck = await clients.Create().CheckApiKeyAsync(ct);
        return Ok(new KeyCheckModel(sdkCheck.Status, sdkCheck.CheckedAt, sdkCheck.HttpStatus, sdkCheck.ErrorCode));
    }

    private ReportsModel Reports(string? siteUrl) =>
        new(reporting.Enabled, SupertextReporting.SetByEnvironment, reporting.Enabled && siteUrl is not null ? SupertextReporting.PingUrl(siteUrl) : null);

    private static KeyCheckModel? ToModel(KeyCheck? check) =>
        check is null ? null : new KeyCheckModel(check.Status, check.CheckedAt, check.HttpStatus, check.ErrorCode);

    private string? SiteUrl() =>
        SupertextReporting.SiteUrl(webRouting.CurrentValue.UmbracoApplicationUrl, Request.Scheme, Request.Host.Value, Request.PathBase.Value);

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex ReleaseVersion();
}
