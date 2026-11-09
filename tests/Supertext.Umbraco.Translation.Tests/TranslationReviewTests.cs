using System.Net;
using System.Text;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Services;
using Xunit;

namespace Supertext.Umbraco.Translation.Tests;

/// <summary>Translation review: languages with it on go through the review service, the others directly to Supertext.</summary>
[Collection("environment")]
public class TranslationReviewTests
{
    private static SupertextClientFactory Factory(RecordingSupertext handler, string? reviewUrl = null)
    {
        var options = new SupertextOptions { ApiKey = "test-key", Endpoint = "https://api.test/v1", PollIntervalSeconds = 1 };
        if (reviewUrl is not null)
        {
            options.ReviewUrl = reviewUrl;
        }
        return new SupertextClientFactory(new FakeHttpClientFactory(handler), new FakeOptions(options));
    }

    private static Task<IReadOnlyDictionary<string, string>> Translate(SupertextClientFactory factory, bool review) =>
        factory.Create(review).TranslateTextsAsync(new Dictionary<string, Segment> { ["0"] = new("Hello") }, "de-CH", "en");

    [Fact]
    public void TheDefaultIsSupertextsReviewService()
    {
        Assert.Equal("https://localizationreview-production.up.railway.app/v1/", SupertextClientFactory.ReviewUrl);
        Assert.Equal(SupertextClientFactory.ReviewUrl, new SupertextOptions().ReviewUrl);
        Assert.Equal(SupertextClientFactory.ReviewUrl, Factory(new RecordingSupertext()).ReviewEndpoint);
    }

    [Fact]
    public async Task ReviewLanguagesGoThroughTheReviewServiceAndOthersDirectly()
    {
        var handler = new RecordingSupertext();
        var factory = Factory(handler, "https://review.test/v1");
        var settings = new LanguageSettings(new SupertextOptions(), new Dictionary<string, SavedLanguage>
        {
            ["de-CH"] = new() { Review = true },
            ["fr-CH"] = new() { Review = false },
        });

        await Translate(factory, settings.Review("de-CH"));
        Assert.Equal(["POST review.test", "GET review.test", "GET review.test", "DELETE review.test"], handler.Calls);
        Assert.All(handler.Keys, k => Assert.Equal("Supertext-Auth-Key test-key", k));
        Assert.All(handler.Agents, a => Assert.Contains("Umbraco-Supertext-Translation/" + SupertextClientFactory.PackageVersion, a));

        handler.Calls.Clear();
        await Translate(factory, settings.Review("fr-CH"));
        Assert.Equal(["POST api.test", "GET api.test", "GET api.test", "DELETE api.test"], handler.Calls);
        Assert.False(settings.Review("it-CH"));
    }

    [Fact]
    public async Task AnEmptyReviewUrlTurnsReviewOff()
    {
        var handler = new RecordingSupertext();
        var factory = Factory(handler, "");
        Assert.Null(factory.ReviewEndpoint);
        await Translate(factory, review: true);
        Assert.All(handler.Calls, c => Assert.EndsWith("api.test", c));
        Assert.Null(Factory(handler, "https://api.test/v1/").ReviewEndpoint); // same as the API: no second client
    }

    [Fact]
    public void TheKeyCheckNeverUsesTheReviewService() =>
        Assert.Equal("https://api.test/v1/", Factory(new RecordingSupertext(), "https://review.test/v1").ClientOptions().BaseUrl);

    [Fact]
    public void TheEnvironmentVariableWinsUnlessEmpty()
    {
        try
        {
            Environment.SetEnvironmentVariable("SUPERTEXT_REVIEW_URL", "https://other-review.test/v1");
            Assert.Equal("https://other-review.test/v1/", Factory(new RecordingSupertext(), "").ReviewEndpoint);
            Environment.SetEnvironmentVariable("SUPERTEXT_REVIEW_URL", " ");
            Assert.Equal("https://review.test/v1/", Factory(new RecordingSupertext(), "https://review.test/v1").ReviewEndpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SUPERTEXT_REVIEW_URL", null);
        }
    }

    [Fact]
    public void AppsettingsWinsOverTheCheckbox()
    {
        var options = new SupertextOptions();
        options.Languages["de-CH"] = new SupertextLanguageOptions { Review = false };
        options.Languages["fr-CH"] = new SupertextLanguageOptions { Review = true };
        var settings = new LanguageSettings(options, new Dictionary<string, SavedLanguage> { ["de-CH"] = new() { Review = true } });
        Assert.False(settings.Review("de-CH"));
        Assert.True(settings.Review("fr_ch"));
        Assert.False(settings.ConfiguredReview("de-CH"));
        Assert.Null(settings.ConfiguredReview("it-CH"));

        var saved = LanguageForm.ToSaved([new LanguageInput("it-CH", null, null, Review: true)], ["it-CH"], new Dictionary<string, SavedLanguage>());
        Assert.True(saved["it-CH"].Review);
        Assert.True(KeyValueLanguageSettingsStore.Parse(KeyValueLanguageSettingsStore.Serialize(saved))["it-CH"].Review);
    }

    private sealed class RecordingSupertext : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];
        public List<string> Keys { get; } = [];
        public List<string> Agents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add($"{request.Method} {request.RequestUri!.Host}");
            Keys.Add(request.Headers.TryGetValues("Authorization", out var auth) ? auth.Single() : "");
            Agents.Add(request.Headers.UserAgent.ToString());
            var path = request.RequestUri.AbsolutePath;
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(Json("{\"file_id\":\"f1\"}"));
            }
            if (request.Method == HttpMethod.Delete)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }
            if (path.EndsWith("/status"))
            {
                return Task.FromResult(Json("{\"status\":\"done\"}"));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body><div data-st-id=\"0\">Hallo</div></body></html>", Encoding.UTF8, "text/html"),
            });
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
