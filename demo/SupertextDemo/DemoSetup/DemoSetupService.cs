using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace SupertextDemo.DemoSetup;

/// <summary>
/// Turns the single-language Clean starter kit into a multilingual demo, on every start
/// (each step only changes what is missing):
///  1. languages German, French and Italian (Switzerland) next to English (US);
///  2. page document types and their text, rich text and block list properties vary by culture;
///  3. culture URLs on the home page: / (English), /de, /fr, /it;
///  4. demo accounts from DEMO_ADMIN_* / DEMO_EDITOR_* (created if missing, never changed).
/// Runs in the background once Umbraco has installed and run the Clean package migration.
/// </summary>
public sealed class DemoSetupService(
    IServiceProvider services,
    IRuntimeState runtimeState,
    ILogger<DemoSetupService> logger) : BackgroundService
{
    public static readonly (string IsoCode, string Name, string Path)[] TargetLanguages =
    [
        ("de-CH", "German (Switzerland)", "/de"),
        ("fr-CH", "French (Switzerland)", "/fr"),
        ("it-CH", "Italian (Switzerland)", "/it"),
    ];

    private static readonly string[] VariantEditors =
        ["Umbraco.TextBox", "Umbraco.TextArea", "Umbraco.RichText", "Umbraco.BlockList", "Umbraco.BlockGrid", "Umbraco.Tags"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the install / package migrations (Clean imports its content in the background).
        for (var i = 0; i < 300 && !stoppingToken.IsCancellationRequested; i++)
        {
            if (runtimeState.Level == RuntimeLevel.Run)
            {
                using var scope = services.CreateScope();
                if (scope.ServiceProvider.GetRequiredService<IContentTypeService>().Get("home") is not null
                    && scope.ServiceProvider.GetRequiredService<IContentService>().GetRootContent().Any())
                {
                    break;
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        try
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            await EnsureLanguagesAsync(sp.GetRequiredService<ILanguageService>());
            await EnsureCultureVariationAsync(sp.GetRequiredService<IContentTypeService>());
            MoveSharedValuesToEnglish(sp.GetRequiredService<IContentService>());
            await EnsureDomainsAsync(sp.GetRequiredService<IContentService>(), sp.GetRequiredService<IDomainService>());
            await EnsureAccountsAsync(sp.GetRequiredService<IUserService>());
            EnsureLanguageSettings(sp.GetRequiredService<Supertext.Umbraco.Translation.Services.ILanguageSettingsStore>());
            logger.LogInformation("[demo] setup complete");
        }
        catch (Exception e)
        {
            logger.LogError(e, "[demo] setup failed");
        }
    }

    private async Task EnsureLanguagesAsync(ILanguageService languageService)
    {
        var existing = (await languageService.GetAllAsync()).Select(l => l.IsoCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (iso, name, _) in TargetLanguages)
        {
            if (existing.Contains(iso))
            {
                continue;
            }
            var result = await languageService.CreateAsync(new Language(iso, name), Constants.Security.SuperUserKey);
            logger.LogInformation("[demo] language {Iso}: {Status}", iso, result.Status);
        }
    }

    /// <summary>
    /// Formal address for the target languages, as if chosen on Settings › Supertext (their Supertext
    /// language is their own code). Only while nothing is saved there, so changes made on the screen stay.
    /// </summary>
    private void EnsureLanguageSettings(Supertext.Umbraco.Translation.Services.ILanguageSettingsStore store)
    {
        if (store.Load().Count > 0)
        {
            return;
        }
        store.Save(TargetLanguages.ToDictionary(
            t => t.IsoCode,
            t => new Supertext.Umbraco.Translation.Services.SavedLanguage { Politeness = "more" },
            StringComparer.OrdinalIgnoreCase));
        logger.LogInformation("[demo] Supertext languages: formal address for {Languages}", string.Join(", ", TargetLanguages.Select(t => t.IsoCode)));
    }

    /// <summary>
    /// Pages and their compositions vary by culture; text-like properties too. Element types
    /// used inside blocks stay invariant: the block list property varies as a whole.
    /// </summary>
    private async Task EnsureCultureVariationAsync(IContentTypeService contentTypeService)
    {
        foreach (var contentType in contentTypeService.GetAll().Where(t => !t.IsElement))
        {
            var changed = false;
            if (!contentType.VariesByCulture())
            {
                contentType.Variations = ContentVariation.Culture;
                changed = true;
            }
            foreach (var propertyType in contentType.PropertyTypes)
            {
                if (VariantEditors.Contains(propertyType.PropertyEditorAlias) && !propertyType.VariesByCulture())
                {
                    propertyType.Variations = ContentVariation.Culture;
                    changed = true;
                }
            }
            if (changed)
            {
                var saved = await contentTypeService.UpdateAsync(contentType, Constants.Security.SuperUserKey);
                logger.LogInformation("[demo] document type {Alias} now varies by culture: {Status}", contentType.Alias, saved.Result);
            }
        }
    }

    /// <summary>
    /// Umbraco moves existing values to the default language only for properties defined on
    /// the document type itself. Clean defines most fields on compositions, so their values
    /// stay stored as language-neutral and become invisible. Move them to en-US here, then
    /// save and republish through the content service (which keeps all caches consistent).
    /// </summary>
    private void MoveSharedValuesToEnglish(IContentService contentService)
    {
        const string english = "en-US";
        var moved = 0;
        foreach (var root in contentService.GetRootContent())
        {
            foreach (var content in new[] { root }.Concat(AllDescendants(contentService, root.Id)))
            {
                var changed = false;
                foreach (var property in content.Properties.Where(p => p.PropertyType.VariesByCulture()))
                {
                    var shared = property.Values.FirstOrDefault(v => v.Culture is null && v.Segment is null);
                    var hasEnglish = property.Values.Any(v => string.Equals(v.Culture, english, StringComparison.OrdinalIgnoreCase) && v.EditedValue is not null);
                    if (shared?.EditedValue is { } value && !hasEnglish)
                    {
                        content.SetValue(property.Alias, value, english);
                        changed = true;
                    }
                }
                if (!changed)
                {
                    continue;
                }
                var result = content.Published
                    ? contentService.SaveAndPublish(content, [english], Constants.Security.SuperUserId)
                    : (object)contentService.Save(content, Constants.Security.SuperUserId);
                moved++;
            }
        }
        if (moved > 0)
        {
            logger.LogInformation("[demo] moved language-neutral values of {Count} document(s) to English", moved);
        }
    }

    private static IEnumerable<IContent> AllDescendants(IContentService contentService, int id)
    {
        long page = 0, total;
        do
        {
            var items = contentService.GetPagedDescendants(id, page++, 100, out total, null, null).ToList();
            foreach (var item in items)
            {
                yield return item;
            }
            if (items.Count == 0)
            {
                break;
            }
        }
        while (page * 100 < total);
    }

    private async Task EnsureDomainsAsync(IContentService contentService, IDomainService domainService)
    {
        var home = contentService.GetRootContent().FirstOrDefault(c => c.ContentType.Alias == "home");
        if (home is null)
        {
            return;
        }
        var current = (await domainService.GetAssignedDomainsAsync(home.Key, true)).ToList();
        if (TargetLanguages.All(t => current.Any(d => string.Equals(d.DomainName, t.Path, StringComparison.OrdinalIgnoreCase))))
        {
            return;
        }
        var result = await domainService.UpdateDomainsAsync(home.Key, new DomainsUpdateModel
        {
            DefaultIsoCode = "en-US",
            Domains = TargetLanguages.Select(t => new DomainModel { DomainName = t.Path, IsoCode = t.IsoCode }).ToArray(),
        });
        logger.LogInformation("[demo] culture URLs on the home page: {Status}", result.Status);
    }

    private async Task EnsureAccountsAsync(IUserService userService)
    {
        await EnsureAccountAsync(userService, "DEMO_ADMIN", "Demo Administrator", Constants.Security.AdminGroupKey);
        await EnsureAccountAsync(userService, "DEMO_EDITOR", "Demo Editor", Constants.Security.EditorGroupKey);
    }

    private async Task EnsureAccountAsync(IUserService userService, string prefix, string name, Guid groupKey)
    {
        var email = Environment.GetEnvironmentVariable(prefix + "_EMAIL")?.Trim();
        var password = Environment.GetEnvironmentVariable(prefix + "_PASSWORD");
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            logger.LogInformation("[demo] {Prefix}_EMAIL / {Prefix}_PASSWORD not set, skipping that account", prefix, prefix);
            return;
        }
        if (userService.GetByEmail(email) is not null)
        {
            logger.LogInformation("[demo] {Prefix} account already exists, leaving it unchanged", prefix);
            return;
        }
        var created = await userService.CreateAsync(Constants.Security.SuperUserKey, new UserCreateModel
        {
            Email = email,
            UserName = email,
            Name = name,
            UserGroupKeys = new HashSet<Guid> { groupKey },
        }, approveUser: true);
        if (!created.Success || created.Result.CreatedUser is not { } user)
        {
            logger.LogWarning("[demo] WARNING: {Prefix} account not created: {Status}", prefix, created.Status);
            return;
        }
        var changed = await userService.ChangePasswordAsync(Constants.Security.SuperUserKey, new ChangeUserPasswordModel
        {
            UserKey = user.Key,
            NewPassword = password,
        });
        if (!changed.Success)
        {
            // e.g. too short for Umbraco's password rules; the message never contains the password.
            await userService.DeleteAsync(Constants.Security.SuperUserKey, user.Key);
            logger.LogWarning("[demo] WARNING: {Prefix} account not created, password rejected: {Status} {Error}", prefix, changed.Status, changed.Result?.Error?.ErrorMessage);
            return;
        }
        logger.LogInformation("[demo] created {Prefix} account", prefix);
    }
}

public sealed class DemoSetupComposer : IComposer
{
    public void Compose(Umbraco.Cms.Core.DependencyInjection.IUmbracoBuilder builder)
        => builder.Services.AddHostedService<DemoSetupService>();
}
