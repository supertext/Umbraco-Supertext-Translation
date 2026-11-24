using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.PluginHealth;
using Supertext.Umbraco.Translation.Sdk;
using Supertext.Umbraco.Translation.Sdk.V1;
using Umbraco.Cms.Core.Services;

namespace Supertext.Umbraco.Translation.Services;

/// <summary>Where the version reporter keeps its state (JSON of Supertext Plugin Health's <see cref="ReporterState"/>, the same as the SDK 0.2 reporter stored).</summary>
public interface IReporterStateStore
{
    ReporterState Load();

    void Save(ReporterState state);
}

/// <summary>Keeps the reporter's state in Umbraco's key-value table (<c>umbracoKeyValue</c>), one row.</summary>
public sealed class KeyValueReporterStateStore(IKeyValueService keyValueService) : IReporterStateStore
{
    public const string Key = "Supertext.Umbraco.Translation.ReporterState";

    public ReporterState Load() => ReporterState.FromJson(keyValueService.GetValue(Key));

    public void Save(ReporterState state) => keyValueService.SetValue(Key, state.ToJson());
}

/// <summary>The versions a report names: the package's (from its .csproj) and Umbraco's.</summary>
public sealed record InstallationInfo(string PackageVersion, string? UmbracoVersion);

public enum ReportAction
{
    /// <summary>A back-end request that runs the package's code: checks a changed key, reports an upgrade.</summary>
    OnRequest,

    /// <summary>The dashboard's "Check API key" button.</summary>
    TestConnection,

    /// <summary>A translation was rejected with 401/403.</summary>
    AuthenticationFailed,
}

/// <summary>
/// The package's side of the Supertext Commando Center (Commando-Center-Supertext-Translation → docs/DEVELOPER.md →
/// Report protocol): version reports, the automatic API key check and the callback ping. The protocol is Supertext Plugin
/// Health's (<see cref="InstallationReporter"/>, <see cref="PingResponse"/>, bundled in lib/supertext-plugin-health) with
/// the SDK's <see cref="SupertextClient.CheckApiKeyAsync"/> as key check; this class gives it what Umbraco knows (site
/// URL, versions) and keeps its state.
///
/// Settings live in appsettings.json / the environment, so there is no "settings saved": the key check runs on
/// back-end requests when the key changed (fingerprint), on "Check API key" and after a 401/403 from Supertext.
/// Nothing here ever throws into a request or a translation.
/// </summary>
public sealed class SupertextReporting(
    IOptionsMonitor<SupertextOptions> options,
    SupertextClientFactory clients,
    IReporterStateStore store,
    InstallationInfo installation,
    ILogger<SupertextReporting> logger)
{
    /// <summary>The GitHub repository name (the center reads the latest release from it).</summary>
    public const string PluginName = "Umbraco-Supertext-Translation";

    /// <summary>Path of the public ping route below the site URL.</summary>
    public const string PingPath = "umbraco/supertext/ping";

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>For tests: where reports go (default: SUPERTEXT_REPORT_URL, else Plugin Health's default).</summary>
    internal string? ReportUrl { get; init; }

    /// <summary>For tests: the HttpClient for reports.</summary>
    internal HttpClient? ReportHttpClient { get; init; }

    /// <summary>For tests: the clock.</summary>
    internal TimeProvider? Clock { get; init; }

    /// <summary>"Send version information to Supertext": <c>Supertext:SendVersionReports</c>; <c>SUPERTEXT_VERSION_REPORTS</c> wins.</summary>
    public bool Enabled => Reporting.ReportsEnabled(options.CurrentValue.SendVersionReports);

    /// <summary>Whether <c>SUPERTEXT_VERSION_REPORTS</c> decides (for the dashboard).</summary>
    public static bool SetByEnvironment => DecidedBy(Environment.GetEnvironmentVariable("SUPERTEXT_VERSION_REPORTS"));

    /// <summary>Whether an environment value decides on its own (a value Plugin Health understands), whatever the setting.</summary>
    internal static bool DecidedBy(string? env) => Reporting.ReportsEnabled(true, env) == Reporting.ReportsEnabled(false, env);

    public InstallationInfo Installation => installation;

    /// <summary>The ping route's answer (404 while reports are off).</summary>
    public PingResponse Ping(string? challenge) => PingResponse.For(challenge, PluginName, Enabled);

    /// <summary>
    /// The site's base URL without trailing slash: Umbraco's <c>WebRouting:UmbracoApplicationUrl</c> if set, else the
    /// address of the current request. Null when neither is an absolute http(s) URL.
    /// </summary>
    public static string? SiteUrl(string? umbracoApplicationUrl, string? scheme, string? host, string? pathBase)
    {
        if (Uri.TryCreate((umbracoApplicationUrl ?? string.Empty).Trim(), UriKind.Absolute, out var configured)
            && (configured.Scheme == Uri.UriSchemeHttp || configured.Scheme == Uri.UriSchemeHttps))
        {
            return configured.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }
        if (string.IsNullOrWhiteSpace(host) || (scheme != "http" && scheme != "https"))
        {
            return null;
        }
        return $"{scheme}://{host}{pathBase}".TrimEnd('/');
    }

    /// <summary>The absolute URL of the ping route for a site URL.</summary>
    public static string PingUrl(string siteUrl) => siteUrl.TrimEnd('/') + "/" + PingPath;

    /// <param name="languages">The language mapping, for <see cref="SendConfigurationAsync"/> only.</param>
    public InstallationReporter Reporter(string siteUrl, IReadOnlyList<PluginHealth.LanguageMapping>? languages = null)
    {
        var clientOptions = clients.ClientOptions(Clock);
        return new(new ReporterOptions
        {
            Enabled = Enabled,
            SiteUrl = siteUrl,
            Plugin = new NameVersion(PluginName, installation.PackageVersion),
            System = new NameVersion("Umbraco", installation.UmbracoVersion),
            SdkVersion = SdkInfo.Version,
            CallbackUrl = PingUrl(siteUrl),
            KeyChecker = KeyChecker.FromDelegates(
                async (key, ct) =>
                {
                    var c = await new SupertextClient(key, clientOptions).CheckApiKeyAsync(ct).ConfigureAwait(false);
                    return new KeyCheck(c.Status, c.CheckedAt, c.HttpStatus, c.ErrorCode, c.Fingerprint);
                },
                ApiKeyCheck.FingerprintOf),
            ReportUrl = ReportUrl,
            HttpClient = ReportHttpClient ?? clientOptions.HttpClient,
            TimeProvider = Clock,
            Languages = languages is null ? null : () => languages,
        });
    }

    /// <summary>
    /// "Send configuration to Supertext" (Settings → Supertext, administrators): one <c>extensions</c> report with
    /// the installed packages and the language mapping, sent now (also within the hour after a failed report).
    /// Nothing with reports off or without a site URL. Never throws.
    /// </summary>
    public async Task<ReporterResult?> SendConfigurationAsync(string? siteUrl, IEnumerable<Extension> extensions, IReadOnlyList<PluginHealth.LanguageMapping> languages)
    {
        if (siteUrl is null)
        {
            return null;
        }
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var result = await Reporter(siteUrl, languages).SendExtensionsAsync(store.Load(), extensions).ConfigureAwait(false);
            store.Save(result.State);
            return result;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Supertext: configuration not sent.");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Runs one reporter action with the stored state and stores the result. One at a time; never throws.</summary>
    public async Task<ReporterResult?> RunAsync(ReportAction action, string? siteUrl, int httpStatus = 401)
    {
        if (siteUrl is null)
        {
            return null;
        }
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var reporter = Reporter(siteUrl);
            var state = store.Load();
            var apiKey = clients.ApiKey;
            var result = action switch
            {
                ReportAction.AuthenticationFailed => await reporter.AuthenticationFailedAsync(state, apiKey, httpStatus).ConfigureAwait(false),
                ReportAction.TestConnection => await reporter.TestConnectionAsync(state, apiKey).ConfigureAwait(false),
                _ => await reporter.OnRequestAsync(state, apiKey).ConfigureAwait(false),
            };
            store.Save(result.State);
            if (result.Event is not null)
            {
                logger.LogDebug("Supertext: version report {Event}: {Sent}.", result.Event, result.Sent ? "sent" : "not sent");
            }
            return result;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Supertext: version report skipped.");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>For back-end requests: runs in the background, so the request never waits for Supertext or the center.</summary>
    public void InBackground(ReportAction action, string? siteUrl, int httpStatus = 401)
    {
        if (siteUrl is null)
        {
            return;
        }
        // No ambient Umbraco scope or request state may flow into the background task.
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(() => RunAsync(action, siteUrl, httpStatus));
        }
    }

    /// <summary>The latest stored key check (for the dashboard), or null.</summary>
    public KeyCheck? LastCheck()
    {
        try
        {
            return store.Load().KeyCheck;
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Supertext: could not read the reporter state.");
            return null;
        }
    }
}
