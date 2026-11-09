using System.Reflection;
using Microsoft.Extensions.Options;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Configuration;

namespace Supertext.Umbraco.Translation.Api;

/// <summary>
/// Creates the bundled Supertext SDK client (<see cref="SupertextClient"/>, namespace <c>Supertext.Umbraco.Translation.Sdk.V1</c>)
/// from the current options. The SDK handles the protocol: authentication header, multipart upload,
/// 429 retries, polling, download, delete and HTML packing. Languages with <i>Translation review</i> on get a
/// client for Supertext's review service (<see cref="Create(bool)"/>): same key, timeouts, HttpClient and User-Agent.
/// </summary>
public sealed class SupertextClientFactory(IHttpClientFactory httpClientFactory, IOptionsMonitor<SupertextOptions> options)
{
    /// <summary>
    /// Supertext's review service (translation proxy of Supertext Localization Review). It speaks the same
    /// API v1 and forwards to the live Supertext API with the site's key, keeping every translation so
    /// reviewers can correct it on the website. Default of <c>Supertext:ReviewUrl</c>.
    /// </summary>
    public const string ReviewUrl = "https://localizationreview-production.up.railway.app/v1/";

    /// <summary>Name of the <see cref="HttpClient"/> registered for Supertext.</summary>
    public const string HttpClientName = "Supertext";

    /// <summary>The package version (the NuGet version from the .csproj, without build metadata).</summary>
    public static readonly string PackageVersion = ReadPackageVersion();

    private static readonly string UserAgent = "Umbraco-Supertext-Translation/" + PackageVersion;

    public bool HasApiKey => options.CurrentValue.EffectiveApiKey != string.Empty;

    public string ApiKey => options.CurrentValue.EffectiveApiKey;

    public string Endpoint => options.CurrentValue.EffectiveEndpoint;

    /// <summary>The review service's base URL, or null when review is off (<c>Supertext:ReviewUrl</c> empty or the same as the API).</summary>
    public string? ReviewEndpoint => options.CurrentValue.EffectiveReviewUrl;

    /// <summary>Client options for the SDK (also used by the version reporter's API key check, which never uses the review service).</summary>
    /// <param name="clock">For tests: the clock (default: the system clock).</param>
    public SupertextClientOptions ClientOptions(TimeProvider? clock = null) => ClientOptions(options.CurrentValue.EffectiveEndpoint, clock);

    private SupertextClientOptions ClientOptions(string baseUrl, TimeProvider? clock = null)
    {
        var o = options.CurrentValue;
        return new SupertextClientOptions
        {
            BaseUrl = baseUrl,
            PollInterval = TimeSpan.FromSeconds(Math.Max(1, o.PollIntervalSeconds)),
            Timeout = TimeSpan.FromSeconds(Math.Max(10, o.PollTimeoutSeconds)),
            UserAgent = UserAgent,
            HttpClient = httpClientFactory.CreateClient(HttpClientName),
            TimeProvider = clock,
        };
    }

    /// <summary>
    /// A client for one translation. <paramref name="review"/>: the target language has <i>Translation review</i> on,
    /// so the client talks to the review service (unless review is off, then directly to Supertext). A job is
    /// submitted, polled, downloaded and deleted with the same client.
    /// </summary>
    public SupertextClient Create(bool review = false)
    {
        var reviewUrl = review ? options.CurrentValue.EffectiveReviewUrl : null;
        return new(options.CurrentValue.EffectiveApiKey, ClientOptions(reviewUrl ?? options.CurrentValue.EffectiveEndpoint));
    }

    private static string ReadPackageVersion()
    {
        var assembly = typeof(SupertextClientFactory).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
