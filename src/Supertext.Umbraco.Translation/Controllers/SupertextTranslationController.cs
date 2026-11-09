using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Supertext.Umbraco.Translation.Api;
using Supertext.Umbraco.Translation.Configuration;
using Supertext.Umbraco.Translation.Sdk.Files;
using Supertext.Umbraco.Translation.Sdk.V1;
using Supertext.Umbraco.Translation.Services;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace Supertext.Umbraco.Translation.Controllers;

/// <summary>Backoffice API used by the "Translate with Supertext" action (translate, export for translation, import translation).</summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("supertext")]
[ApiExplorerSettings(GroupName = "Supertext")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class SupertextTranslationController(
    ContentTranslator translator,
    SupertextClientFactory clients,
    IAuthorizationService authorizationService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
    SupertextReporting reporting,
    LanguageSettingsProvider languageSettings,
    IOptionsMonitor<WebRoutingSettings> webRouting) : ManagementApiControllerBase
{
    /// <param name="SupertextCode">the Supertext code it is sent as; null when the language isn't mapped (can't be translated)</param>
    public sealed record LanguageModel(string IsoCode, string Name, bool IsDefault, bool Exists, string? SupertextCode);

    public sealed record StatusModel(bool HasApiKey, string Endpoint);

    public sealed record TranslateRequest(Guid DocumentId, string SourceCulture, string[] TargetCultures, bool Overwrite);

    /// <param name="Error">English message</param>
    /// <param name="ErrorCode">key suffix of <c>supertext_error_…</c> in the backoffice localization</param>
    public sealed record CultureResultModel(string Culture, string Status, int Fields, string? Error, string? ErrorCode, string[]? ErrorArgs, string? ErrorDetail);

    public sealed record ExportRequest(Guid DocumentId, string SourceCulture, string[] TargetCultures, string Format);

    /// <param name="Content">the uploaded file's text (the dialog checks the size before reading it; the server again)</param>
    public sealed record ImportRequest(string Content, bool IncludeChanged, bool Overwrite);

    /// <param name="Status">translated (written as a draft), exists (not written: the language has content and overwrite wasn't requested), nothing (no text imported), failed</param>
    public sealed record ImportJobModel(
        string? DocumentId, string? DocumentName, string Culture, string Status,
        int Imported, int SourceChanged, int Untranslated, int InvalidMarkup, int Missing,
        string? Error, string? ErrorCode, string[]? ErrorArgs);

    [HttpGet("status")]
    [ProducesResponseType<StatusModel>(StatusCodes.Status200OK)]
    public IActionResult Status()
    {
        Report();
        return Ok(new StatusModel(clients.HasApiKey, clients.Endpoint));
    }

    [HttpGet("languages")]
    [ProducesResponseType<IEnumerable<LanguageModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Languages(Guid documentId)
    {
        try
        {
            var languages = await translator.GetLanguagesAsync(documentId);
            Report();
            var settings = languageSettings.Current();
            return Ok(languages.Select(l => new LanguageModel(l.IsoCode, l.Name, l.IsDefault, l.Exists, LanguageMapping.Resolve(l.IsoCode, settings).Code)));
        }
        catch (TranslationException e)
        {
            return NotFound(ProblemFor(e));
        }
    }

    [HttpPost("translate")]
    [ProducesResponseType<IEnumerable<CultureResultModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Translate([FromBody] TranslateRequest request, CancellationToken ct)
    {
        if (request.TargetCultures is not { Length: > 0 } || string.IsNullOrWhiteSpace(request.SourceCulture))
        {
            return BadRequest(ProblemFor(new TranslationException("chooseLanguages", "Choose a source language and at least one target language.")));
        }

        // The editor needs update rights on the document in the target languages.
        var authorized = await authorizationService.AuthorizeResourceAsync(
            User,
            ContentPermissionResource.WithKeys(ActionUpdate.ActionLetter, request.DocumentId, request.TargetCultures),
            AuthorizationPolicies.ContentPermissionByResource);
        if (!authorized.Succeeded)
        {
            return Forbidden();
        }

        var userId = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Id ?? Constants.Security.SuperUserId;
        try
        {
            var results = await translator.TranslateAsync(request.DocumentId, request.SourceCulture, request.TargetCultures, request.Overwrite, userId, ct);
            // Supertext rejected the key: the key check becomes "invalid" (reported once, when it changes).
            var rejected = results.Select(r => r.Exception?.InnerException).OfType<SupertextException>()
                .FirstOrDefault(e => e.Code == SupertextErrorCodes.AuthenticationFailure);
            if (rejected is not null)
            {
                reporting.InBackground(ReportAction.AuthenticationFailed, SiteUrl(), rejected.Status ?? 401);
            }
            else
            {
                Report();
            }
            return Ok(results.Select(r => new CultureResultModel(r.Culture, r.Status.ToString().ToLowerInvariant(), r.Fields, r.Error, r.Exception?.Code, r.Exception?.Args, r.Exception?.Detail)));
        }
        catch (TranslationException e)
        {
            return BadRequest(ProblemFor(e));
        }
    }

    /// <summary>Export for translation: the document's texts as an XLIFF file (one or more languages) or an HTML file (one language).</summary>
    [HttpPost("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromBody] ExportRequest request)
    {
        var targets = (request.TargetCultures ?? []).Where(t => !string.IsNullOrWhiteSpace(t) && !string.Equals(t, request.SourceCulture, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (targets.Count == 0 || string.IsNullOrWhiteSpace(request.SourceCulture))
        {
            return BadRequest(ProblemFor(new TranslationException("chooseLanguages", "Choose a source language and at least one target language.")));
        }
        var format = request.Format == TranslationFileExchange.FormatHtml ? TranslationFileExchange.FormatHtml : TranslationFileExchange.FormatXliff;
        if (format == TranslationFileExchange.FormatHtml && targets.Count != 1)
        {
            return BadRequest(ProblemFor(new TranslationException("htmlOneLanguage", "An HTML file holds one language: choose exactly one.")));
        }
        // Same rights as translating: update on the document in the target languages.
        if (!await CanUpdateAsync(request.DocumentId, targets))
        {
            return Forbidden();
        }
        try
        {
            var segments = translator.CollectForExport(request.DocumentId, request.SourceCulture);
            var o = languageSettings.Current();
            var sourceCode = TranslationFileExchange.LanguageCode(() => LanguageMapping.Source(request.SourceCulture, o), request.SourceCulture);
            var jobs = targets.Select(t => TranslationFileExchange.Job(request.DocumentId, request.SourceCulture, t, sourceCode,
                TranslationFileExchange.LanguageCode(() => LanguageMapping.Target(t, o), t), segments)).ToList();
            var file = TranslationFileExchange.Export(format, request.DocumentId, targets, jobs);
            return File(System.Text.Encoding.UTF8.GetBytes(file.Content), file.ContentType, file.FileName);
        }
        catch (TranslationException e)
        {
            return BadRequest(ProblemFor(e));
        }
        catch (TranslationFileException e)
        {
            return BadRequest(ProblemFor(TranslationFileExchange.FileError(e)));
        }
    }

    /// <summary>
    /// Import translation: reads a translated XLIFF or HTML file (in memory, never stored) and writes each job's
    /// texts into its document and language as a draft, like a translation with Supertext.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(ImportRequestLimit)]
    [ProducesResponseType<IEnumerable<ImportJobModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import([FromBody] ImportRequest request)
    {
        IReadOnlyList<TranslationFile> files;
        try
        {
            files = TranslationFileExchange.Read(request.Content);
        }
        catch (TranslationException e)
        {
            return BadRequest(ProblemFor(e));
        }

        var userId = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Id ?? Constants.Security.SuperUserId;
        var results = new List<ImportJobModel>();
        foreach (var file in files)
        {
            Guid? documentKey = null;
            var culture = file.TargetLanguage;
            try
            {
                var (key, source, target) = TranslationFileExchange.Locate(file);
                documentKey = key;
                culture = target;
                if (!await CanUpdateAsync(key, [target]))
                {
                    throw new TranslationException("notAllowed", "You may not edit this document in this language.");
                }
                var result = await translator.ImportAsync(file, key, source, target, request.IncludeChanged, request.Overwrite, userId);
                var c = result.Counts;
                var status = result.Status switch
                {
                    TranslationStatus.Translated => "translated",
                    TranslationStatus.Exists => "exists",
                    _ => "nothing",
                };
                results.Add(new ImportJobModel(key.ToString("D"), translator.DocumentName(key, target), result.Culture, status,
                    c?.Imported ?? 0, c?.SourceChanged ?? 0, c?.Untranslated ?? 0, c?.InvalidMarkup ?? 0, c?.Missing ?? 0, null, null, null));
            }
            catch (TranslationException e)
            {
                var name = documentKey is { } k ? translator.DocumentName(k, culture) : null;
                var code = e.Code == "documentNotFound" ? "fileDocumentNotFound" : e.Code;
                results.Add(new ImportJobModel(documentKey?.ToString("D"), name, culture, "failed", 0, 0, 0, 0, 0, e.Message, code, e.Args));
            }
        }
        Report();
        return Ok(results);
    }

    /// <summary>The JSON request limit for imports: the 10 MB file, JSON-escaped, plus room.</summary>
    private const long ImportRequestLimit = 3 * TranslationFileExchange.MaxUploadBytes;

    private async Task<bool> CanUpdateAsync(Guid documentKey, IEnumerable<string> cultures)
    {
        var authorized = await authorizationService.AuthorizeResourceAsync(
            User,
            ContentPermissionResource.WithKeys(ActionUpdate.ActionLetter, documentKey, cultures),
            AuthorizationPolicies.ContentPermissionByResource);
        return authorized.Succeeded;
    }

    /// <summary>Version report and key check of a back-end request, in the background (Services/SupertextReporting.cs).</summary>
    private void Report() => reporting.InBackground(ReportAction.OnRequest, SiteUrl());

    private string? SiteUrl() =>
        SupertextReporting.SiteUrl(webRouting.CurrentValue.UmbracoApplicationUrl, Request.Scheme, Request.Host.Value, Request.PathBase.Value);

    /// <summary>English title plus code, arguments and detail, so the backoffice can localize it.</summary>
    private static ProblemDetails ProblemFor(TranslationException e) => new()
    {
        Title = e.Message,
        Extensions =
        {
            ["supertextCode"] = e.Code,
            ["supertextArgs"] = e.Args,
            ["supertextDetail"] = e.Detail,
        },
    };
}
