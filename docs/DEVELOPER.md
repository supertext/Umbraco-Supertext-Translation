# Developer guide — Supertext Translation for Umbraco

How the package is built, how to work on it, and how the demo is deployed.

## Architecture

```
Backoffice: "Translate with Supertext" (workspace action + entity action)
   └─ dialog (translate-modal.js) ── GET  /umbraco/management/api/v1/supertext/status
                                  ── GET  …/supertext/languages?documentId=
                                  ── POST …/supertext/translate {documentId, sourceCulture, targetCultures[], overwrite}
                                  ── POST …/supertext/export {documentId, sourceCulture, targetCultures[], format} → file (blob)
                                  ── POST …/supertext/import {content, includeChanged, overwrite} → summary per job
Server: SupertextTranslationController (Management API, SectionAccessContent + Update permission per culture)
   └─ ContentTranslator ── per target culture: LanguageMapping (culture → Supertext code) → collect segments
                        │   → bundled Supertext SDK TranslateTextsAsync (one HTML document)
                        └─ SetCultureName / SetValue(culture) → IContentService.Save (draft)
                        export/import: CollectCulture (same keys) → TranslationFileExchange (SDK TranslationFiles) → ApplyCulture → Save
Backoffice: Settings › Supertext (settings-dashboard.js) ── GET  …/supertext/settings
                                                         ── POST …/supertext/settings/check-key
                                                         ── PUT  …/supertext/settings/languages {languages: [{isoCode, code, politeness}]}
Server: SupertextSettingsController (Management API, SectionAccessSettings)
Public: GET /umbraco/supertext/ping?challenge=  (SupertextPingController, anonymous; Commando Center)
All back-office endpoints → SupertextReporting (Plugin Health InstallationReporter, in the background) → state in umbracoKeyValue
```

| Part | Role |
| --- | --- |
| `wwwroot/App_Plugins/SupertextTranslation/` | Backoffice extension, plain ES modules (no build step): `umbraco-package.json` (workspace action on `Umb.Workspace.Document`, entity action for `document`, sidebar modal), `workspace-action.js`, `entity-action.js`, `open-dialog.js` (modal token), `translate-modal.js` (Lit element on `UmbModalBaseElement`, calls the API with `umbHttpClient` and bearer security). |
| `wwwroot/App_Plugins/SupertextTranslation/lang/` | UI strings: `en.js`, `de.js`, `fr.js`, `it.js`, registered as `localization` extensions in `umbraco-package.json`. See *UI strings* below. |
| `Controllers/SupertextTranslationController` | `ManagementApiControllerBase`, `[VersionedApiBackOfficeRoute("supertext")]`. Checks `ContentPermissionResource.WithKeys(ActionUpdate.ActionLetter, document, targetCultures)`. |
| `Services/ContentTranslator` | Copies every culture-variant property from the source culture to each target; registers translatable text as segments with "rebuild" closures; translates them per target language with the SDK's `TranslateTextsAsync` (one document; the SDK splits only above ~900k characters); writes values and the culture name; saves once. Existing target languages are skipped unless `overwrite` (the dialog asks). |
| `wwwroot/App_Plugins/SupertextTranslation/settings-dashboard.js` | The *Supertext* dashboard in the Settings section (`dashboard` extension, condition `Umb.Section.Settings`): version, endpoint, API key set, latest key check with *Check API key*, the version reports setting and ping URL, and the *Languages* form: per Umbraco language a `uui-select` with `targets` (the SDK's `TargetOptions()`, names from `Intl.DisplayNames` in the backoffice language, Supertext's name as fallback and for the Swiss German dialects) preselected with the saved code, else `suggestedCode`, else *— choose —*, a tone select where the chosen target supports politeness, and a *Translation review* `uui-checkbox` (with a note when `reviewUrl` is null: review off); values from appsettings.json are shown read-only (*set in appsettings.json*). *Save* sends `PUT settings/languages`. Other settings are read-only (appsettings.json / the environment). |
| `Controllers/SupertextSettingsController` | `GET settings` (dashboard data, with `targets` and per language the effective, saved, suggested and appsettings values; also reports `onRequest`), `POST settings/check-key` (`testConnection`), `PUT settings/languages` (checked by `LanguageForm`, 400 for an unknown culture or a code Supertext doesn't offer; answers with the languages). `SectionAccessSettings`. |
| `Controllers/SupertextPingController` | Plain `ControllerBase`, `[AllowAnonymous]`, `GET /umbraco/supertext/ping`: copies status, headers and body of Plugin Health's `PingResponse.For(challenge, "Umbraco-Supertext-Translation", enabled)`. |
| `Services/SupertextReporting` | The Commando Center glue (see *Version reports and the API key check*). |
| `Services/TranslationSegments` | The texts of one document culture under stable path keys (`name`, `<alias>`, `<alias>/markup`, `<alias>/<block key>/<alias>`) and their results, from the API or a file. No Umbraco dependency. |
| `Services/TranslationFileExchange` | *Export for translation* / *Import translation* without Umbraco (unit-tested): reference format, `FileJob`s, file names, upload size check, `plugin`/`reference` check, applying `ok` (and optionally `source_changed`) results. See *Translation files*. |
| `Services/LanguageMapping` | Culture → Supertext code with the SDK's `Languages` (see *Language codes*) on a `LanguageSettings`; `Resolve` for the dashboard and the dialog. |
| `Services/LanguageSettings` | `SavedLanguage` (code, politeness, review per culture), `KeyValueLanguageSettingsStore` (one JSON row in `umbracoKeyValue`, key `Supertext.Umbraco.Translation.Languages`), `LanguageSettings` (saved values with `Supertext:Languages:<culture>` from appsettings over them, value by value; `Mapping` for the SDK), `LanguageSettingsProvider` (current settings, read per use, so a save applies at once), `LanguageForm` (checks and normalises the form, Umbraco-free). |
| `Api/SupertextClientFactory` | Creates the SDK's `Supertext.Umbraco.Translation.Sdk.V1.SupertextClient` from the current options (key, endpoint, poll interval and timeout, User-Agent `Umbraco-Supertext-Translation/<version>`) on the named `HttpClient` "Supertext"; `Create(review)` gives a client for the review service for languages with *Translation review* (see *Translation review*); `PackageVersion` is the assembly's informational version (the `.csproj` `Version`). |
| `Api/TranslationException` | Errors for the backoffice: `Code` is the suffix of `supertext_error_…` in `lang/*.js`. `From()` maps the SDK's error codes (`SupertextErrorCodes`) to these codes. |
| `Configuration/SupertextOptions` | `Supertext` section; `SUPERTEXT_API_KEY` / `SUPERTEXT_API_ENDPOINT` / `SUPERTEXT_REVIEW_URL` win (`ReviewUrl`, default `SupertextClientFactory.ReviewUrl`; `EffectiveReviewUrl` is null when empty or equal to the endpoint); `SendVersionReports` (`SUPERTEXT_VERSION_REPORTS` wins, via the SDK); `Languages.<culture>` (`Code`, `Politeness`, `Enabled`) and `Review` override the saved language settings. |
| `Composing/SupertextComposer` | Options, named `HttpClient`, client factory, scoped translator, language settings store and provider, reporter state store, `InstallationInfo` (package and Umbraco version), `SupertextReporting` (singletons). |

**Segment keys** are paths in the document (`name`, the property alias, `…/markup` for rich text, `…/<block key>/<alias>` inside blocks, `#2` appended to a duplicate), so they stay stable when blocks are added or reordered: the API translation uses them to match results, translation files as their unit ids.

**Value formats** the translator understands:

- Text box / text area: strings (plain text segments; bare URLs, e-mails, paths and numbers are skipped).
- Rich text: `{"markup": "<p>…</p>", "blocks": {…}}` since Umbraco 14 (or legacy plain HTML). The whole markup is **one** HTML segment; blocks inside it are walked like block lists.
- Block list / grid / single block: `{"contentData": [{"contentTypeKey", "values": [{"alias", "value", "editorAlias", "culture"}]}], "settingsData": […], …}`. `editorAlias` is usually `null`, so the element type (looked up by `contentTypeKey`) decides each value's editor. Only `contentData` is translated. Values may arrive **encoded twice** (a JSON string whose content is JSON); `Unwrap` decodes one level and re-encodes the result the same way.
- In `CollectBlockNode`, keep the `(object?)` casts on the switch arms: `JsonNode` has an implicit conversion from `string`, so without them the switch is typed `JsonNode` and strings silently become JSON values again (block text was skipped because of this).

### UI strings

All text the backoffice shows comes from the localization files in `wwwroot/App_Plugins/SupertextTranslation/lang/` (Umbraco's backoffice localization: one `localization` extension per culture in `umbraco-package.json`, `meta.culture` `en`/`de`/`fr`/`it`; Umbraco matches the user's UI culture by language, so `de-CH` gets `de`, and falls back to `en`).

- Keys live in the `supertext` section: `this.localize.term('supertext_translate')` in the modal, `#supertext_translateWithSupertext` for manifest labels. Texts with arguments are functions (`overwriteWarning: (languages, count) => …`). `noApiKeyHtml` is rendered with `unsafeHTML` (our own text, with the two links).
- Server errors: `TranslationException` (own errors, and the SDK's `SupertextException` mapped by `TranslationException.From`) carries an error `Code`, `Args` and the untranslated `Detail` from Supertext besides the English `Message` (logs and API clients). The controller returns them as `supertextCode`/`supertextArgs`/`supertextDetail` in the problem details (and `errorCode`/`errorArgs`/`errorDetail` per language in the translate result); the modal shows `supertext_error_<code>` and falls back to the English text for unknown codes.
- **New or changed strings need all four languages** in the same commit: formal address (Sie, vous, Lei), Umbraco's own terms in each language (its backoffice language files: *Entwurf*, *brouillon*, *bozza*, *Dokumenttyp*, …), "Supertext", placeholders and URLs untranslated; French uses a non-breaking space (`\u00a0`) before `?`, `!`, `:` and `;`. `node Tests/Localization/check-keys.mjs` (also in CI) fails when a language misses a key, a function takes a different number of arguments, the code uses an undefined key or a server error code has no text.

## Supertext SDK

The SDK provides the API client and the API key check (`CheckApiKeyAsync`, `ApiKeyCheck.FingerprintOf`, `SdkInfo.Version`); version reports and the ping answer come from Supertext Plugin Health (next section). The package talks to Supertext through the official **Supertext SDK for .NET** ([Supertext-API-SDK-and-Guide](https://github.com/Supertext/Supertext-API-SDK-and-Guide/tree/main/sdk/dotnet), MIT). The SDK implements the protocol below, the HTML packing (one `data-st-id` element per field), the language code rules, the 429 retries and stable error codes; it is tested against the shared test vectors of all Supertext SDKs. The package only collects values, maps languages and writes the results back.

**Bundled copy:** the SDK's sources live in `lib/supertext-sdk/` (`src/SdkVersion.cs` and `src/V1/*.cs`, `LICENSE`, `COMMIT` with the SDK commit, `VERSION` with the SDK's package version, a short README), with the namespace changed from `Supertext.Sdk` to `Supertext.Umbraco.Translation.Sdk` (the API v1 classes are in `…Sdk.V1`; only `SdkInfo.Version` is outside it), and are compiled into `Supertext.Umbraco.Translation.dll` (`<Compile Include>` in the `.csproj`). Sites install only this package; a site that also references the `Supertext.Sdk` NuGet package can't get a clash or version conflict. This stays so after the SDK is on nuget.org.

- Update it with `scripts/vendor-sdk.sh [commit-or-branch]` (default `main`; `SDK_DIR=<clone>` uses a local clone; it copies every `.cs` file below `sdk/dotnet/src/Supertext.Sdk/` with its folder, `bin/` and `obj/` excluded), run the tests, and commit with a CHANGELOG entry. The `.csproj` compiles `lib/supertext-sdk/src/**/*.cs`.
- Never edit the copy: fix the SDK in its own repository, then re-run the script.

## Supertext API protocol

AI file translation API v1, shared with all Supertext plugins:

1. `POST translate/ai/file` — multipart: `file` (`content.html`, part `Content-Type: text/html` exactly), `target_lang` (`de-CH`), `source_lang` (primary subtag, `en`), optional `politeness` (`more`/`less`) → `{file_id}`
2. `GET translate/ai/file/{id}/status` until `done` (`error`, `limit_exceeded`, `deleted` fail)
3. `GET translate/ai/file/{id}/translation` → translated HTML
4. `DELETE translate/ai/file/{id}` (files also expire after 24 h)

Header `Authorization: Supertext-Auth-Key <key>` (the SDK strips a pasted prefix). HTTP 429 is retried up to 4 times (`Retry-After`, else 1/2/4/8 s with jitter). Target languages are translated one after the other to stay under the per-second limit.

## Translation review

*Translation review* per target culture (`SavedLanguage.Review`, the checkbox on Settings › Supertext; `Supertext:Languages:<culture>:Review` wins): `ContentTranslator` asks `SupertextClientFactory.Create(languages.Review(target))`, which builds the SDK client with the same key, poll interval, timeout, named `HttpClient` and User-Agent but the base URL `ReviewUrl` (default `SupertextClientFactory.ReviewUrl`, the translation proxy of [Supertext Localization Review](https://github.com/Supertext/Localization-Review-Supertext-Translation)). `TranslateTextsAsync` submits, polls, downloads and deletes on that one client, so a job is always collected where it was submitted. The proxy speaks the same API v1, forwards to the live Supertext API with the site's key, stores every segment per key and serves reviewers' corrections on the next upload, so nothing else changes in the package. `ReviewUrl: ""` (or the same URL as `Endpoint`) means `Create(true)` returns a direct client. The key check (`ClientOptions()`), the version reports and file export/import never use the review service. `TranslationReviewTests` cover the four calls going to the review service for a review language and to the API for the others, the default URL, an empty URL, the environment variable, appsettings over the checkbox and the stored flag.

## Translation files (XLIFF and HTML)

*Export for translation* and *Import translation* (bottom of the dialog) use the SDK's `TranslationFiles` (SDK 0.5.0, namespace `Supertext.Umbraco.Translation.Sdk.Files`; integration guide → *Translation files: XLIFF and HTML*, vectors `spec/translation-file.json`). Nothing here calls the API, so no API key is needed.

- **Segments:** exactly what the API translation sends: `ContentTranslator.CollectCulture` for the source culture, keyed by path (`TranslationSegments`). Import collects the document again and writes through the same rebuild closures (`ApplyCulture`), then saves once, like a translation; texts without a result keep the source.
- **Job:** `plugin` = `Umbraco-Supertext-Translation`; `reference` = `document:<document key>:<source culture>:<target culture>` (e.g. `document:5f2c3a8e-1d2b-4c3d-9e8f-0a1b2c3d4e5f:en-US:de-CH`; the source culture too, so an import collects the same texts again); `sourceLanguage`/`targetLanguage` = the codes `LanguageMapping` resolves, or the culture with `_` → `-` when it has none (unmapped languages don't block an export).
- **Export** (`POST …/supertext/export`): XLIFF with one `<file>` per target culture (`umbraco-<key>-<culture>.xlf`, or `umbraco-<key>.xlf` for several), `application/x-xliff+xml`; HTML for exactly one culture (`umbraco-<key>-<culture>.html`, `text/html; charset=utf-8`); both as attachments. The dialog fetches it with `umbHttpClient` (`parseAs: 'blob'`, bearer auth) and saves it through an object URL with the server's file name. Permission: *Update* on the document in the target cultures. Nothing to translate → `nothingToTranslate`.
- **Import** (`POST …/supertext/import`): the dialog refuses files over 10 MB before reading them and sends the file's text as JSON (`umbHttpClient` has no multipart helper); the server refuses more than 10 MB (UTF-8) again (`[RequestSizeLimit]` 30 MB for the JSON) and parses in memory. Per job: `plugin` must be ours (`otherSystem`), the reference must parse and the document exist (`fileDocumentNotFound`), the target culture must be an Umbraco language other than the source (`noLanguage`), the user needs *Update* on the document in it (`notAllowed`), an existing culture needs `overwrite` (`exists`). `ok` results are written, `source_changed` only with `includeChanged`; `untranslated`, `invalid_markup`, `missing` are not. Nothing is saved when nothing was imported (`nothing`). SDK errors (`invalid_file`, `missing_metadata`, `too_large`) become `invalidFile`, `missingMetadata`, `fileTooLarge`.

## Language codes

Supertext accepts exactly the codes of its `GET features` list (the SDK's `Languages.Targets` / `Languages.Sources`, from the live API): bare sources (`en`, `de`); targets in one exact form each, regional for German, English, French, Italian and Portuguese (`de-CH`, `en-US`, `pt-BR`), with a script for Chinese and Serbian (`zh-Hans`, `sr-Latn`), a dialect for Swiss German (`gsw-u-sd-chzh`), bare for the others (`es`, `nl`, `sl`). Anything else is HTTP 404 `INVALID_LANGUAGE_PAIR` (`invalid_language_pair`); formal/informal for a target without it (`en-US`, `rm`, …) is HTTP 400 `FEATURE_ERROR` (`unsupported_feature`).

`Services/LanguageMapping.cs` resolves Umbraco cultures with the SDK (the same rules in every Supertext plugin; nothing is guessed beyond the SDK's obvious defaults):

- **The mapping** is the combo box per Umbraco language on Settings › Supertext (saved by `PUT settings/languages`, validated with `Languages.MatchTarget` and stored in its exact form), with `Supertext:Languages:<culture>:Code` from appsettings.json winning per culture (`LanguageSettings.Mapping`). The combo box preselects `Languages.SuggestTarget(culture)` (exact match, else default) while nothing is saved, and shows *— choose —* when there is none.
- **Target** (`Languages.Target(culture, mapping)`): the mapping wins (sent as it is, trimmed); else a culture that equals a Supertext code apart from case and `_`/`-` (`de-CH`, `fr_ch` → `fr-CH`, `es`); else the SDK's default target (SDK 0.4.0: `de` → `de-DE`, `en` → `en-US`, `es-ES` → `es`, `zh-CN` → `zh-Hans`; `Resolve` marks it `FromDefault`, the dashboard shows *Supertext default*); else the SDK throws `unsupported_language` (with `TargetLang`) and nothing is sent for that language (`en-IE`, `de-LI`, `gsw` need a mapping).
- **Source** (`Languages.Source(culture, mapping)`): the same mapping, sent in its source form (`de-CH` → `de`, done by the SDK since 0.2.1), else the culture's Supertext source (`en-US` → `en`), else `unsupported_language`.
- **Politeness** (appsettings, else the screen) is sent as configured. The screen offers it only where `Languages.SupportsPoliteness(code)` is true and `LanguageForm` drops it otherwise; for a value from appsettings the dashboard warns where Supertext has none (rather than silently dropping the administrator's value).
- `TranslationException` maps `unsupported_language` to `unsupportedLanguage` (argument: the culture) and `unsupported_feature` to `unsupportedFeature`; the dialog marks unmapped languages *not mapped* (`supertextCode: null` in `GET languages`) and doesn't preselect them.
- Translating within one language (`en-US` → `en-GB`) is allowed.

The stand-in (`Tests/Docs/stand-in.mjs`) accepts only the codes of Supertext's list, like the live API.

## Supertext Plugin Health

Version reports to the Supertext Commando Center, the API key check result and the ping answer come from **Supertext Plugin Health** ([Commando-Center-Supertext-Translation → `plugin-health/`](https://github.com/Supertext/Commando-Center-Supertext-Translation/tree/main/plugin-health), MIT), a dependency-free library; the package passes it the SDK's key check (`KeyChecker.FromDelegates` around `CheckApiKeyAsync` and `ApiKeyCheck.FingerprintOf`, in `SupertextReporting.Reporter`) and `SdkInfo.Version`.

**Bundled copy:** its sources live in `lib/supertext-plugin-health/` (`src/*.cs`, `LICENSE`, `COMMIT`, `VERSION` with the Plugin Health version, a short README), with the namespace changed from `Supertext.PluginHealth` to `Supertext.Umbraco.Translation.PluginHealth`, compiled into `Supertext.Umbraco.Translation.dll` like the SDK copy (`<Compile Include="../../lib/supertext-plugin-health/src/**/*.cs">` in the `.csproj`).

- Update it with `scripts/vendor-plugin-health.sh <tag>` (e.g. `plugin-health-v0.1.0`; default `main`; `PLUGIN_HEALTH_DIR=<clone>` uses a local clone of the Commando Center repository), run the tests, and commit with a CHANGELOG entry.
- Never edit the copy: fix it in Commando-Center-Supertext-Translation/plugin-health, then re-run the script.
- Its `ReporterState` JSON is the one the SDK 0.2 reporter stored, so sites that update keep their state and send no new `installed` report.

## Version reports and the API key check

The package implements the Commando Center's *Report protocol* (Commando-Center-Supertext-Translation → `docs/DEVELOPER.md`) with Supertext Plugin Health's `InstallationReporter`, `PingResponse` and `Reporting.ReportsEnabled` and the SDK's `CheckApiKeyAsync` as key check; `Services/SupertextReporting.cs` only supplies what Umbraco knows and stores the state.

- **Reporter options:** plugin `Umbraco-Supertext-Translation` (the GitHub repository name) with `SupertextClientFactory.PackageVersion`; system `Umbraco` with `IUmbracoVersion.SemanticVersion` (without build); runtime from Plugin Health's default, `sdk_version` = the bundled SDK's `SdkInfo.Version`; site URL = `WebRouting:UmbracoApplicationUrl`, else the request's scheme, host and path base; callback = `<site URL>/umbraco/supertext/ping`; the client options (endpoint, User-Agent, the named `HttpClient`) for the SDK's key check; reports go over the same named `HttpClient`. `SUPERTEXT_REPORT_URL` overrides the center's URL (tests, local demo).
- **When:** settings live in appsettings.json / the environment, so there is no "settings saved" (and no `afterSettingsSaved`). `OnRequest` runs on every back-office endpoint of the package (`status`, `languages`, `translate`, `settings`): it checks the key only when its fingerprint changed and reports only what changed (`installed`, `upgraded`, `api_key_check`). `AuthenticationFailed` runs when a translation got 401/403 (instead of `OnRequest`). `TestConnection` runs on the dashboard's *Check API key* and returns the check. There is no `check` CLI command.
- **Never blocking:** back-office requests start the reporter with `InBackground` (`Task.Run` with the execution context suppressed, so no ambient Umbraco scope flows in); only *Check API key* waits for it. Calls are serialized (`SemaphoreSlim`), and `RunAsync` catches everything (Plugin Health never throws; storage errors are logged at debug level and swallowed).
- **State:** Plugin Health's `ReporterState` JSON (the same format the SDK 0.2 reporter stored) in Umbraco's key-value table (`IKeyValueService`, key `Supertext.Umbraco.Translation.ReporterState`): last reported version set, key status, last failure, the latest key check (status, time, HTTP status, error code, key fingerprint). Never the key.
- **Setting:** `Supertext:SendVersionReports` (default `true`); `SUPERTEXT_VERSION_REPORTS` (`0`/`false`/`off`, `1`/`true`/`on`) wins, through `Reporting.ReportsEnabled`. Off: nothing is sent and the ping answers 404; the key check still runs.
- **Ping:** `SupertextPingController` (anonymous, outside the Management API, `ApiExplorerSettings(IgnoreApi)`), answering exactly Plugin Health's `PingResponse` (`Cache-Control: no-store`). `/umbraco/` is one of Umbraco's reserved paths, so content routing never takes it.
- **Dashboard texts:** the key check messages (`keycheck_<status>`) and the setting's label and help text are the center's, in en/de/fr/it (help text adapted: there is no settings save, reports follow updates and key changes).

## Local development

.NET 10 SDK and Node 20+ (stand-in API, screenshots).

```bash
cd Tests/Docs && npm install && STAND_IN_PREFIX=1 node stand-in.mjs &   # untranslated text comes back as "[de-CH] …"; also a fake center (POST /report)
cd demo/SupertextDemo
export DEMO_ADMIN_EMAIL=admin@example.com DEMO_ADMIN_PASSWORD='choose-10+chars' \
       DEMO_EDITOR_EMAIL=editor@example.com DEMO_EDITOR_PASSWORD='choose-10+chars' \
       Umbraco__CMS__Unattended__UnattendedUserEmail=$DEMO_ADMIN_EMAIL \
       Umbraco__CMS__Unattended__UnattendedUserPassword=$DEMO_ADMIN_PASSWORD \
       Umbraco__CMS__WebRouting__UmbracoApplicationUrl=http://127.0.0.1:8095 \
       Umbraco__CMS__Global__UseHttps=false \
       SUPERTEXT_API_KEY=test SUPERTEXT_API_ENDPOINT=http://127.0.0.1:8765/v1/ \
       SUPERTEXT_REPORT_URL=http://127.0.0.1:8765/report   # version reports go to the stand-in, never the live center
dotnet run --urls http://127.0.0.1:8095
```

The first start installs Umbraco (SQLite in `umbraco/Data`), imports Clean, and `DemoSetupService` makes it multilingual (watch for `[demo] setup complete` in `umbraco/Logs`). Delete `umbraco/Data` for a fresh start.

Two Umbraco 17 settings matter for local HTTP:

- `UmbracoApplicationUrl`: Umbraco 17 doesn't auto-detect its URL; without it the backoffice login fails (the OAuth redirect address is unknown).
- `Global:UseHttps=false`: otherwise OpenIddict refuses the login over plain HTTP ("This server only accepts HTTPS requests"), also in Development. On Railway, HTTPS comes through the proxy instead (see Demo).

## Tests

```bash
dotnet test tests/Supertext.Umbraco.Translation.Tests    # SDK glue: error code mapping, client factory round trip, language mapping,
                                                         # reporting (actions, state, ping, site URL) against fake Supertext/center HTTP
node Tests/Localization/check-keys.mjs                   # UI strings complete in en, de, fr, it
```

CI (`.github/workflows/ci.yml`) builds the package and the demo, runs the tests, syntax-checks the backoffice modules (and their language files), checks the UI strings and the demo entrypoint.

`LanguageMappingTests` cover the language rules (exact codes, the SDK's defaults, mapping in both directions, unmapped cultures, tone support); `TranslationReviewTests` the review service client (see *Translation review*); `LanguageSettingsTests` the saved mapping, appsettings winning per value, the form's checks (exact codes stored, unknown cultures and codes refused, tone dropped where Supertext has none, other languages kept) and the stored JSON; `TranslationFileExchangeTests` the translation files glue (reference round trip, export with the collector's keys and the mapping, `ok` written, `source_changed` only with the option, untranslated/invalid/missing not written, other plugin and unknown reference refused, too large and bad files refused); `ReportingTests` run `SupertextReporting` with an in-memory state store and a fake `HttpMessageHandler` for `GET features` and the center's `POST report` (installed report with versions and callback, no report without a change, 401/403 once, *Check API key*, reports off, broken storage, ping answers, site URL).

End to end (manual, before a release): fresh demo with the stand-in (`STAND_IN_PREFIX=1`), translate *Features* into all three languages as the editor, check every text field and block text carries the marker (and URLs and code don't), translate again (overwrite warning), publish German and open `/de/funktionen/`; export *Features* as XLIFF and HTML and import the HTML file again (summary, German draft). `Tests/Docs/screenshots.mjs` runs most of this automatically.

## Docs screenshots

`docs/images/` is generated by `Tests/Docs/screenshots.mjs` from a **fresh** local demo whose package talks to the stand-in (without `STAND_IN_PREFIX`). The stand-in returns real German for the texts the flow touches (`Tests/Docs/sample-de.json`: *Home* and *Features*), so the guides never show placeholder text. The flow translates and publishes *Home* in German first, because German subpages need a published German home page to get a URL.

```bash
cd Tests/Docs && npm install && node stand-in.mjs &
# start the demo fresh (see Local development, with SUPERTEXT_REPORT_URL), then:
BASE_URL=http://127.0.0.1:8095 DEMO_EDITOR_EMAIL=… DEMO_EDITOR_PASSWORD=… DEMO_ADMIN_EMAIL=… DEMO_ADMIN_PASSWORD=… npm run screenshots
```

`ONLY=dashboard npm run screenshots` takes only the Settings › Supertext dashboard (needs no fresh demo): its *Languages* box as `supertext-languages.png` (used in the installation guide) and the configuration box as `supertext-dashboard.png` (not committed: it shows the local stand-in's endpoint and ping URL). New texts in the flow: run the stand-in with `STAND_IN_DUMP=<dir>` and translate what it writes to `<dir>/de-CH.json` into `sample-de.json`. `CHROMIUM_PATH` points Playwright at an installed Chromium.

## Demo (Railway)

The public demo is a container built from `demo/Dockerfile`: Umbraco 17 LTS with the **Clean** starter kit (English) made multilingual — German, French and Italian (Switzerland) — and this package by project reference. It runs on Railway in the `supertext-cms-demos` project, service `umbraco`, region EU West (Amsterdam): <https://umbraco-production.up.railway.app/> (backoffice: `/umbraco`). German, French and Italian URLs (`/de`, `/fr`, `/it`) return 404 until a page is translated and published in that language.

**Deploys:** Railway watches `main` and rebuilds on every push.

| File | Purpose |
| --- | --- |
| `demo/Dockerfile` | `dotnet publish` of `demo/SupertextDemo` (SDK image), run on the ASP.NET image with `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` |
| `demo/entrypoint.sh` | Links `umbraco/Data` (SQLite), `umbraco/Logs` and `wwwroot/media` to the volume at `/data`; maps `DEMO_ADMIN_*` to the unattended install; sets `UmbracoApplicationUrl` from `APP_URL` or `RAILWAY_PUBLIC_DOMAIN` |
| `demo/SupertextDemo/` | The Umbraco project: Umbraco 17.8, Clean 7.0.8, the package, `appsettings.json` (SQLite, unattended install, `ExcludedProperties: ["code"]`) |
| `demo/SupertextDemo/DemoSetup/DemoSetupService.cs` | On every start, once Umbraco runs: adds de-CH/fr-CH/it-CH, makes non-element document types and their text/rich-text/block-list properties vary by culture, moves composition values to en-US (Umbraco only moves values of the type's own properties), sets `/de` `/fr` `/it` on the home page, creates the demo accounts, and saves formal address for the three languages as Settings › Supertext would (only while nothing is saved there) |
| `demo/SupertextDemo/Views/` | Clean's views, **committed**: Clean writes them only during its first-boot package migration, so a container restart would otherwise lose them |
| `demo/.env.example` | All variables |

**State:** the SQLite database, logs and media live on a Railway volume at `/data`. Everything else comes from the image. To reset the demo, delete the files on the volume and redeploy.

**Accounts** (created on every start if missing; existing ones are never changed; the e-mail address is the username; Umbraco requires at least 10 characters, shorter passwords skip the account with a warning in the log):

| Variables | Account |
| --- | --- |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` | *Administrators* group; also the unattended-install user (required on first boot) |
| `DEMO_EDITOR_EMAIL`, `DEMO_EDITOR_PASSWORD` | *Editors* group — can update, translate and publish in all languages |

Umbraco's installer screen never appears: the install is unattended.

**Service variables:** `DEMO_*` (above), `SUPERTEXT_API_KEY`, optional `SUPERTEXT_API_ENDPOINT`, optional `SUPERTEXT_REVIEW_URL` (another review service; to try review, tick *Translation review* on Settings › Supertext instead), optional `SUPERTEXT_VERSION_REPORTS=0` (the demo reports to the Commando Center like any installation), `APP_URL` (public URL; defaults to `https://$RAILWAY_PUBLIC_DOMAIN`), `PORT=8080` (the domain's target port), `RAILWAY_DOCKERFILE_PATH=demo/Dockerfile`.

**Run it locally:**

```bash
docker build -f demo/Dockerfile -t supertext-umbraco-demo .
docker run --rm -p 8080:8080 -v umbracodemo:/data \
  -e DEMO_ADMIN_EMAIL=admin@example.com -e DEMO_ADMIN_PASSWORD='choose-10+chars' \
  -e APP_URL=http://localhost:8080 -e Umbraco__CMS__Global__UseHttps=false \
  -e SUPERTEXT_API_KEY=... supertext-umbraco-demo
```

## Code quality and security checks

- **Checks** workflow (`.github/workflows/checks.yml`): [actionlint](https://github.com/rhysd/actionlint) and [zizmor](https://docs.zizmor.sh/) lint the workflows on every push and pull request. Dependency review fails a pull request that adds a package with a known vulnerability (moderate or worse). Actions are pinned to commit SHAs; Dependabot keeps the pins up to date. To run the workflow lint locally: `pip install actionlint-py zizmor`, then `actionlint` and `zizmor .github/workflows` in the repo root.
- **Links** workflow (`.github/workflows/links.yml`): [lychee](https://lychee.cli.rs/) checks the links in all Markdown files weekly and whenever docs change on `main`. Broken links open (or update) the issue "Broken links in the docs". Links that can't work from CI (local URLs, placeholders, pages behind a login) are excluded in `.lycheeignore`.
- C# and the backoffice JavaScript are analysed by CodeQL (see below), so this repo runs no separate static analyser.
- GitHub settings (set by Remy's setup script, not stored in the repo): **secret scanning with push protection** (a push containing a known token format is rejected; findings under *Security → Secret scanning*) and **CodeQL default setup** (findings under *Security → Code scanning* and as comments on pull requests; PHP isn't covered by CodeQL, which is why the PHP plugins run PHPStan).
- `.github/workflows/support-triage.yml`: support requests from the Supertext integrations website arrive as issues labelled `support` (contact details stay in the Supertext CMS). Claude classifies each one: answers questions (marked `<!-- supertext:reply -->`, emailed to the sender by the CMS) and closes them, fixes bugs as pull requests for review (never merged automatically), labels feature requests `feature-request`. Only people with triage rights can add labels, so outsiders can't start it. Needs the organisation secret `ANTHROPIC_API_KEY`; the workflow is the same in every Supertext repo.

Before starting work in this repo, look at its open findings: code scanning alerts, secret scanning alerts, Dependabot PRs and the "Broken links in the docs" issue.

## Releasing

Releases are published by `.github/workflows/release.yml` when the version is officially bumped; nobody tags or creates releases by hand.

1. Move the *Unreleased* entries in `CHANGELOG.md` under a new `## X.Y.Z — YYYY-MM-DD` section, and keep an empty *Unreleased* above it.
2. Set the same version in:
   - `src/Supertext.Umbraco.Translation/Supertext.Umbraco.Translation.csproj`: `Version`, the NuGet package version
   - `src/Supertext.Umbraco.Translation/wwwroot/App_Plugins/SupertextTranslation/umbraco-package.json`: `version`, shown under Settings → Packages
3. Push to `main`. The workflow checks that the version files match `CHANGELOG.md`, then tags `vX.Y.Z` and creates the GitHub release with the CHANGELOG section as notes (0.x versions as pre-releases). A push that adds no new version does nothing, and a version that is already released is skipped. After fixing a failed run, start it again with *Run workflow* on the *Release* workflow.

Publishing to NuGet stays manual: `dotnet pack src/Supertext.Umbraco.Translation -c Release`, then push the package (the `umbraco-marketplace` tag lists it on the Umbraco Marketplace).

## Known limitations / roadmap

- Translation runs inside the request (up to `PollTimeoutSeconds` per language). Planned: a background job with progress for very large pages and many languages.
- Unsaved changes in the open page are not translated (the dialog says so).
- No sync of later source changes; translate again to refresh a language. A translation memory and reviewers' corrections come from the review service for languages with *Translation review*; corrections reach Umbraco only with the next translation (no push).
- Segment-level variants and block-level variance (element types that vary by culture inside an invariant block list) are not handled yet.
- Media alt texts, dictionary items and tags are not translated.
- *Export for translation* works on one page at a time (no bulk export yet). The docs have no screenshots of the export and import parts of the dialog yet (the local demo didn't start in the session that added them; `Tests/Docs/screenshots.mjs` should add them).
- Human (professional) translation orders are not supported yet.
- Tested with the stand-in API on Umbraco 17.8 and, with the package built against 17 as published, on Umbraco 18.3 (`Tests/Docs/screenshots.mjs` flow); not yet against the live API. The package is built against Umbraco 17 and the same DLL runs on 18, so it must only call members that exist in both: Umbraco 18 moved some members, so code that compiles against both can still fail at run time, e.g. `IContentService.GetById(Guid)` (only on `IContentServiceBase<IContent>` now; a call compiled against 17 fails on 18 with `MissingMethodException`). The demo stays on Umbraco 17; moving it to 18 also needs Clean 8 and updated views in `demo/SupertextDemo/Views/` (Umbraco 18 removed `BlockListItem.ContentUdi`).
- The language mapping and tone are edited on Settings › Supertext; the other settings (API key, endpoint, version reports) are read from appsettings.json / the environment and only shown there. Editing those would add `afterSettingsSaved` reports.
