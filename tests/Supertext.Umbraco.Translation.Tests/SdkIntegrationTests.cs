using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Services;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Configuration;
using Xunit;

namespace Supertext.Umbraco.Translation.Tests;

/// <summary>
/// The glue between the package and the Supertext SDK. The SDK has its own tests for the
/// protocol, HTML packing and language codes.
/// </summary>
public class SdkIntegrationTests
{
    [Fact]
    public void EverySdkErrorCodeHasABackofficeCode()
    {
        var sdkCodes = typeof(SupertextErrorCodes).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!);
        foreach (var code in sdkCodes)
        {
            Assert.True(TranslationException.SdkCodes.ContainsKey(code), $"no backoffice code for {code}");
        }
    }

    [Fact]
    public void SdkErrorsKeepTheirDetailOnce()
    {
        var e = TranslationException.From(new SupertextException(
            SupertextErrorCodes.UnexpectedStatus, "Supertext answered with an unexpected HTTP status.", status: 418, detail: "teapot"));
        Assert.Equal("http", e.Code);
        Assert.Equal(["418"], e.Args);
        Assert.Equal("teapot", e.Detail);
        Assert.Equal("Supertext answered with an unexpected HTTP status. (teapot)", e.Message);
    }

    [Fact]
    public async Task FactoryClientTranslatesThroughTheNamedHttpClient()
    {
        var handler = new FakeSupertext();
        var options = new SupertextOptions { ApiKey = "Supertext-Auth-Key test-key", Endpoint = "https://api.supertext.com/v1", PollIntervalSeconds = 1 };
        var factory = new SupertextClientFactory(new FakeHttpClientFactory(handler), new FakeOptions(options));
        Assert.True(factory.HasApiKey);
        Assert.Equal("https://api.supertext.com/v1/", factory.Endpoint);

        var result = await factory.Create().TranslateTextsAsync(new Dictionary<string, Segment>
        {
            ["0"] = new("Fish & Chips\nfor two"),
            ["1"] = new("<p>We ship <strong>Swiss chocolate</strong>.</p>", Html: true),
        }, LanguageMapping.Target("de_ch", options), LanguageMapping.Source("en-US", options), Politeness.More);

        Assert.Equal("» Fish & Chips\n» for two", result["0"]);
        Assert.Equal("<p>» We ship <strong>» Swiss chocolate</strong>» .</p>", result["1"]);
        Assert.Equal("Supertext-Auth-Key test-key", handler.Authorization);
        Assert.Contains("Umbraco-Supertext-Translation/", handler.UserAgent);
        Assert.Contains("name=\"target_lang\"\r\n\r\nde-CH", handler.Upload);
        Assert.Contains("name=\"source_lang\"\r\n\r\nen\r\n", handler.Upload);
        Assert.Contains("name=\"politeness\"\r\n\r\nmore", handler.Upload);
        Assert.True(handler.Deleted);
    }

    [Fact]
    public async Task UnknownLanguageShowsTheLanguagePairMessage()
    {
        // The live API answers a language it doesn't know at all with HTTP 404 INVALID_LANGUAGE_PAIR.
        var factory = new SupertextClientFactory(
            new FakeHttpClientFactory(new UnknownLanguage()),
            new FakeOptions(new SupertextOptions { ApiKey = "test-key", Endpoint = "https://api.supertext.com/v1", PollIntervalSeconds = 1 }));
        var sdk = await Assert.ThrowsAsync<SupertextException>(() => factory.Create().TranslateTextsAsync(
            new Dictionary<string, Segment> { ["0"] = new("Hello") }, "xx-YY", "en"));
        Assert.Equal(SupertextErrorCodes.InvalidLanguagePair, sdk.Code);
        Assert.Equal("languagePair", TranslationException.From(sdk).Code);
    }

    [Fact]
    public void UnsupportedLanguageAndFeatureHaveBackofficeCodes()
    {
        var unmapped = Assert.Throws<SupertextException>(() => LanguageMapping.Target("en-IE", new SupertextOptions()));
        var error = TranslationException.From(unmapped);
        Assert.Equal("unsupportedLanguage", error.Code);
        Assert.Equal(["en-IE"], error.Args);

        var feature = TranslationException.From(new SupertextException(SupertextErrorCodes.UnsupportedFeature, "No politeness.", status: 400, feature: "politeness"));
        Assert.Equal("unsupportedFeature", feature.Code);
    }

    private sealed class UnknownLanguage : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error_code\":\"INVALID_LANGUAGE_PAIR\",\"source_lang\":\"en\",\"target_lang\":\"xx-YY\"}", Encoding.UTF8, "application/json"),
            });
    }

    /// <summary>Answers like the Supertext API; "translates" by putting "» " before every text node.</summary>
    private sealed class FakeSupertext : HttpMessageHandler
    {
        public string Upload { get; private set; } = "";
        public string? Authorization { get; private set; }
        public string UserAgent { get; private set; } = "";
        public bool Deleted { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                Upload = await request.Content!.ReadAsStringAsync(cancellationToken);
                Authorization = request.Headers.TryGetValues("Authorization", out var auth) ? auth.Single() : null;
                UserAgent = request.Headers.UserAgent.ToString();
                return Json("{\"file_id\":\"f1\"}");
            }
            if (request.Method == HttpMethod.Delete)
            {
                Deleted = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (path.EndsWith("/status"))
            {
                return Json("{\"status\":\"done\"}");
            }
            var start = Upload.IndexOf("<body>", StringComparison.Ordinal);
            var body = Upload[start..(Upload.IndexOf("</body>", StringComparison.Ordinal) + 7)];
            var translated = System.Text.RegularExpressions.Regex.Replace(body, ">([^<]+)<", m => ">» " + m.Groups[1].Value + "<");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>" + translated + "</html>", Encoding.UTF8, "text/html") };
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
