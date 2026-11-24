using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.PluginHealth;
using Supertext.Umbraco.Translation.Sdk;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Services;
using Xunit;

namespace Supertext.Umbraco.Translation.Tests;

/// <summary>
/// The package's side of the Commando Center: which reporter action runs, what is reported, where the state goes,
/// the ping answers. Against fakes of the Supertext API and the center; never the live services.
/// </summary>
public class ReportingTests
{
    private const string Site = "https://www.example.ch";
    private const string ReportUrl = "https://center.test/api/v1/report";

    private static (SupertextReporting Reporting, FakeServices Http, MemoryStateStore Store) Create(
        string apiKey = "test-key", bool reports = true, MemoryStateStore? store = null)
    {
        var http = new FakeServices();
        var options = new SupertextOptions { ApiKey = apiKey, SendVersionReports = reports, Endpoint = "https://api.supertext.com/v1/" };
        store ??= new MemoryStateStore();
        var reporting = new SupertextReporting(
            new FakeOptions(options),
            new SupertextClientFactory(new FakeHttpClientFactory(http), new FakeOptions(options)),
            store,
            new InstallationInfo("1.2.3", "17.7.1"),
            NullLogger<SupertextReporting>.Instance)
        {
            ReportUrl = ReportUrl,
            ReportHttpClient = new HttpClient(http, disposeHandler: false),
        };
        return (reporting, http, store);
    }

    [Fact]
    public async Task FirstBackOfficeRequestReportsTheInstallation()
    {
        var (reporting, http, store) = Create();
        var result = await reporting.RunAsync(ReportAction.OnRequest, Site);

        Assert.NotNull(result);
        Assert.Equal(ReportEvents.Installed, result.Event);
        Assert.True(result.Sent);
        var report = Assert.Single(http.Reports);
        Assert.Equal("Umbraco-Supertext-Translation", report["plugin"]!["name"]!.GetValue<string>());
        Assert.Equal("1.2.3", report["plugin"]!["version"]!.GetValue<string>());
        Assert.Equal("Umbraco", report["system"]!["name"]!.GetValue<string>());
        Assert.Equal("17.7.1", report["system"]!["version"]!.GetValue<string>());
        Assert.Equal(Site, report["site_url"]!.GetValue<string>());
        Assert.Equal(Site + "/umbraco/supertext/ping", report["callback"]!["url"]!.GetValue<string>());
        Assert.Equal("ok", report["api_key_check"]!["status"]!.GetValue<string>());
        Assert.True(report["api_key_configured"]!.GetValue<bool>());
        Assert.Equal(SdkInfo.Version, report["sdk_version"]!.GetValue<string>());
        Assert.DoesNotContain("test-key", report.ToJsonString());
        Assert.DoesNotContain("test-key", store.Json);
        Assert.Equal(KeyCheckStatus.Ok, reporting.LastCheck()?.Status);
    }

    [Fact]
    public async Task LaterRequestsReportOnlyChanges()
    {
        var (reporting, http, _) = Create();
        await reporting.RunAsync(ReportAction.OnRequest, Site);
        var second = await reporting.RunAsync(ReportAction.OnRequest, Site);

        Assert.Null(second?.Event);
        Assert.Single(http.Reports);
        Assert.Equal(1, http.KeyChecks); // the key didn't change: not checked again
    }

    [Fact]
    public async Task RejectedTranslationMarksTheKeyInvalidOnce()
    {
        var (reporting, http, _) = Create();
        await reporting.RunAsync(ReportAction.OnRequest, Site);
        var result = await reporting.RunAsync(ReportAction.AuthenticationFailed, Site, 403);
        await reporting.RunAsync(ReportAction.AuthenticationFailed, Site, 403);

        Assert.Equal(ReportEvents.ApiKeyCheck, result?.Event);
        Assert.Equal(KeyCheckStatus.Invalid, reporting.LastCheck()?.Status);
        Assert.Equal(403, reporting.LastCheck()?.HttpStatus);
        Assert.Equal(2, http.Reports.Count); // installed + one api_key_check
        Assert.Equal("invalid", http.Reports[1]["api_key_check"]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task CheckApiKeyButtonRunsTheCheck()
    {
        var (reporting, http, _) = Create(apiKey: "bad-key");
        var result = await reporting.RunAsync(ReportAction.TestConnection, Site);

        Assert.Equal(KeyCheckStatus.Invalid, result?.Check?.Status);
        Assert.Equal(1, http.KeyChecks);
        Assert.Equal(KeyCheckStatus.Invalid, reporting.LastCheck()?.Status);
    }

    [Fact]
    public async Task MissingKeyIsCheckedWithoutARequest()
    {
        var (reporting, http, _) = Create(apiKey: "");
        var result = await reporting.RunAsync(ReportAction.TestConnection, Site);

        Assert.Equal(KeyCheckStatus.Missing, result?.Check?.Status);
        Assert.Equal(0, http.KeyChecks);
    }

    [Fact]
    public async Task OffMeansNoReportsButTheKeyIsStillChecked()
    {
        var (reporting, http, store) = Create(reports: false);
        if (SupertextReporting.SetByEnvironment)
        {
            return; // SUPERTEXT_VERSION_REPORTS is set where the tests run
        }
        Assert.False(reporting.Enabled);
        await reporting.RunAsync(ReportAction.OnRequest, Site);

        Assert.Empty(http.Reports);
        Assert.Equal(KeyCheckStatus.Ok, ReporterState.FromJson(store.Json).KeyCheck?.Status);
    }

    [Fact]
    public async Task BrokenStorageNeverBreaksTheRequest()
    {
        var (reporting, _, _) = Create(store: new MemoryStateStore { Fail = true });
        Assert.Null(await reporting.RunAsync(ReportAction.OnRequest, Site));
        Assert.Null(reporting.LastCheck());
    }

    [Fact]
    public async Task UnknownSiteUrlReportsNothing()
    {
        var (reporting, http, _) = Create();
        Assert.Null(await reporting.RunAsync(ReportAction.OnRequest, null));
        Assert.Empty(http.Reports);
    }

    [Fact]
    public void PingAnswersOnlyWhileReportsAreOn()
    {
        if (SupertextReporting.SetByEnvironment)
        {
            return;
        }
        var (on, _, _) = Create();
        var ok = on.Ping("abcdefghijklmnop_1234");
        Assert.Equal(200, ok.Status);
        Assert.Equal("application/json", ok.Headers["Content-Type"]);
        Assert.Equal("no-store", ok.Headers["Cache-Control"]);
        Assert.Equal("""{"ok":true,"plugin":"Umbraco-Supertext-Translation","challenge":"abcdefghijklmnop_1234"}""", ok.Body);
        Assert.Equal(400, on.Ping("<script>").Status);
        Assert.Equal(400, on.Ping(null).Status);

        var (off, _, _) = Create(reports: false);
        Assert.Equal(404, off.Ping("abcdefghijklmnop_1234").Status);
    }

    [Theory]
    [InlineData("https://www.example.ch/", "http", "localhost:8080", "", "https://www.example.ch")]
    [InlineData("https://www.example.ch/site/", null, null, null, "https://www.example.ch/site")]
    [InlineData("", "https", "demo.example.ch", "", "https://demo.example.ch")]
    [InlineData(null, "http", "127.0.0.1:8095", "/cms", "http://127.0.0.1:8095/cms")]
    [InlineData("not a url", null, null, null, null)]
    [InlineData(null, "https", "", "", null)]
    public void SiteUrlIsUmbracosApplicationUrlElseTheRequest(string? configured, string? scheme, string? host, string? pathBase, string? expected) =>
        Assert.Equal(expected, SupertextReporting.SiteUrl(configured, scheme, host, pathBase));

    [Theory]
    [InlineData("0", true)]
    [InlineData("off", true)]
    [InlineData("1", true)]
    [InlineData(null, false)]
    [InlineData("maybe", false)]
    public void EnvironmentDecidesOnlyWithAKnownValue(string? env, bool decides) =>
        Assert.Equal(decides, SupertextReporting.DecidedBy(env));

    [Fact]
    public async Task StateStoredByTheSdk02ReporterIsKeptWithoutANewInstalledReport()
    {
        // What the SDK 0.2 reporter stored in umbracoKeyValue before the switch to Plugin Health.
        var stored = "{\"reportedVersions\":\"1.2.3|17.7.1|" + Environment.Version + "\",\"reportedKeyStatus\":\"ok\",\"lastFailureAt\":null,"
            + "\"keyCheck\":{\"status\":\"ok\",\"checkedAt\":\"2026-10-14T09:00:00Z\",\"httpStatus\":200,\"errorCode\":null,\"fingerprint\":\"" + ApiKeyCheck.FingerprintOf("test-key") + "\"}}";
        var (reporting, http, store) = Create(store: new MemoryStateStore { Json = stored });
        // Plugin Health 0.2+ adds keyChecks and reportedKeyStatuses (null with one key); everything else is kept.
        Assert.Equal(stored[..^1] + ",\"keyChecks\":null,\"reportedKeyStatuses\":null}", store.Load().ToJson());

        var result = await reporting.RunAsync(ReportAction.OnRequest, Site);
        Assert.Null(result?.Event);
        Assert.Empty(http.Reports);
        Assert.Equal(0, http.KeyChecks);
    }

    [Fact]
    public async Task SendConfigurationSendsPackagesAndTheLanguageMapping()
    {
        var (reporting, http, _) = Create();
        Extension[] packages = [new("uSync", "17.0.2", true, "package")];
        PluginHealth.LanguageMapping[] languages = [new("en-US", "en-US"), new("de-CH", "de-CH"), new("en-IE", null)];

        var result = await reporting.SendConfigurationAsync(Site, packages, languages);

        Assert.Equal(ReportEvents.Extensions, result?.Event);
        Assert.True(result!.Sent);
        var report = Assert.Single(http.Reports);
        Assert.Equal("extensions", report["event"]!.GetValue<string>());
        Assert.Equal("uSync", report["extensions"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("de-CH", report["languages"]![1]!["supertext"]!.GetValue<string>());
        Assert.Null(report["languages"]![2]!["supertext"]);
        Assert.Equal(0, http.KeyChecks);
    }

    [Fact]
    public async Task SendConfigurationSendsNothingWithReportsOff()
    {
        var (reporting, http, _) = Create(reports: false);
        var result = await reporting.SendConfigurationAsync(Site, [new("uSync")], [new("de-CH", "de-CH")]);

        Assert.False(result?.Sent ?? false);
        Assert.Empty(http.Reports);
    }

    [Fact]
    public void StateIsStoredAsTheSdksJson()
    {
        var store = new MemoryStateStore();
        var state = ReporterState.Empty with { ReportedVersions = "1.2.3|17.7.1|10.0.0" };
        store.Save(state);
        Assert.Equal("1.2.3|17.7.1|10.0.0", store.Load().ReportedVersions);
        Assert.Equal(ReporterState.Empty, new MemoryStateStore { Json = "not json" }.Load());
    }

    /// <summary>The Supertext API (GET features: 401 for "bad-key") and the Commando Center (POST report: 202).</summary>
    private sealed class FakeServices : HttpMessageHandler
    {
        public List<JsonNode> Reports { get; } = [];
        public int KeyChecks { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (request.Method == HttpMethod.Post && url == ReportUrl)
            {
                Reports.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!);
                return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json") };
            }
            if (request.Method == HttpMethod.Get && url.EndsWith("/features", StringComparison.Ordinal))
            {
                KeyChecks++;
                var key = request.Headers.Authorization?.ToString() ?? "";
                return key.Contains("bad-key")
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}", Encoding.UTF8, "application/json") }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
