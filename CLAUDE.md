# Working on this repository

Part of Supertext's "translation plugins for the top 20 open source CMS" project. Each CMS has its own repo named `Supertext/<CMS>-Supertext-Translation`.

## Documentation rule (always)

Every plugin repo keeps three guides, and **every change that affects behaviour, settings, installation or the code structure updates them in the same commit**:

| File | Audience | Must cover |
| --- | --- | --- |
| `docs/INSTALLATION.md` | Administrators | Requirements, install/update/uninstall, API key, language setup, all settings, troubleshooting |
| `docs/USER_GUIDE.md` | Editors | How to translate and review in the CMS's own UI, what is and isn't translated, what errors mean |
| `docs/DEVELOPER.md` | Developers | Architecture, Supertext API protocol, local setup, tests, CI/deploy, releasing, known limitations/roadmap |

Also: `README.md` stays a short overview linking the three guides, and `CHANGELOG.md` gets an entry under *Unreleased* for every user-visible change. Before finishing any task, check the docs still match the code.

## Supertext account and API key links (always)

Everywhere an administrator enters or is told about the API key — the settings field's help text, the "no API key" / "authentication failed" messages, `docs/INSTALLATION.md`, `README.md` and the demo's `.env.example` — show both links (same as the WordPress plugin):

- Create a Supertext account (or log in): https://www.supertext.com/person/en/account/signin
- Generate the AI API key: https://www.supertext.com/en/integrations/api (supertext.com → Integrations → API; requires the **Admin** role)

Wording: "No Supertext account yet? Create one at supertext.com. Generate your API key at supertext.com → Integrations → API (requires the Admin role)." In the UI, links open in a new tab (`target="_blank" rel="noopener"`); where the CMS shows plain text only, use the bare URLs. New screens or messages that mention the key get the links too.

## UI languages (always)

The plugin's own UI (buttons, panels, dialogs, settings, permissions, messages) is available in English, German, French and Italian through the CMS's own translation mechanism, so it follows the user's back-end language. New or changed strings get all four languages in the same commit. Formal address (Sie, vous, Lei), the CMS's own terms in each language, "Supertext", placeholders and URLs never translated.

## Plugin list (always)

`README.md` ends with the shared list of all Supertext plugins (between the `<!-- supertext-plugins:start -->` and `<!-- supertext-plugins:end -->` markers). It is identical in every Supertext plugin repo: when a plugin is added, renamed or its description changes, update the list in **all** repos, not just this one.

## Releases (always)

Releases are published by `.github/workflows/release.yml`: never tag or create a GitHub release by hand. To release, follow `docs/DEVELOPER.md` → *Releasing* (new version section in `CHANGELOG.md`, same number in the version files) and push to `main`. A push without a new version releases nothing.

## Repo setup (always)

Every Supertext plugin repo has, and a new one gets from the start:

- `LICENSE` matching the license its manifest declares (`composer.json`, `package.json`, `pyproject.toml`, `.csproj`, plugin header).
- `SECURITY.md`: report vulnerabilities privately through GitHub's private vulnerability reporting or support@supertext.com, never in public issues.
- `.github/dependabot.yml`: weekly updates for its package ecosystem and GitHub Actions, minor and patch updates grouped into one pull request.
- `.github/workflows/checks.yml` (actionlint + zizmor on every push and PR, dependency review on PRs) and `.github/workflows/links.yml` (lychee weekly and on docs changes; broken links open the issue "Broken links in the docs"). Third-party actions are pinned to commit SHAs.
- `.github/workflows/dependabot-merge.yml`: merges Dependabot's minor and patch package updates once all other checks pass (built-in token, so GitHub Actions updates and majors stay for a person). See `docs/DEVELOPER.md` → *Dependency updates*.
- On GitHub: the About box filled in (one-sentence description, website https://www.supertext.com, topics), `main` protected against force-pushes and deletion, Wiki and Projects off, Dependabot alerts and private vulnerability reporting on, secret scanning with push protection and CodeQL default setup on, and the Supertext social preview image.
- A row in the plugin list (see *Plugin list*) and in the org profile (`Supertext/.github` → `profile/README.md`).

Claude sessions can't change GitHub repo settings (HTTP 403): add a new repo to Remy's setup script (`set-github-about`) instead of trying.

## Checks and alerts (always)

Before starting work in a repo, look at its open findings and fix what the task touches or what is quick: code scanning alerts (`gh api 'repos/Supertext/<Repo>/code-scanning/alerts?state=open'`), secret scanning alerts (`…/secret-scanning/alerts?state=open`), open Dependabot PRs and the issue "Broken links in the docs". New workflows and workflow changes must pass actionlint and zizmor; PHP code must pass PHPStan at the repo's level. See `docs/DEVELOPER.md` → *Code quality and security checks*.

## Demo accounts rule (always)

Every demo must be usable right after deployment, without anyone registering in a browser. On **every start**, the demo creates these accounts if they don't exist yet:

| Variables | Account |
| --- | --- |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` | Full administrator (for Supertext staff) |
| `DEMO_EDITOR_EMAIL`, `DEMO_EDITOR_PASSWORD` | Editor-level account that can translate content in every demo language; used for automated tests and screenshots. Where the CMS has no editor role that works out of the box, use the closest role and document it. |

- Existing accounts are never modified: no password resets from variables, no duplicates on restart.
- A password that doesn't meet the CMS's own password rules skips that account with a clear warning in the log. The demo still starts.
- Values live only in the hosting platform's variables (Railway). Never in the repo, in chat or in logs. Log the variable name, never the password.
- If the CMS has a first-run "create admin" screen, these accounts replace it. Document that once `DEMO_*` is set, the screen no longer appears.
- If a demo already used CMS-specific names (e.g. `TYPO3_ADMIN_*`, `PAYLOAD_ADMIN_*`), keep them as fallbacks for `DEMO_ADMIN_*`.
- The demo also seeds its target languages and at least one sample entry in the source language, and makes sure the editor account can access every target language.
- Document the variables in `docs/DEVELOPER.md` (demo section) and in the demo's `.env.example`.

## Screenshots rule (always)

The user guide and installation guide of every plugin include screenshots of the real UI: at least the translate action before and after translating, a translated result, the overwrite or retranslate warning if there is one, the plugin's settings or configuration screen, and the CMS's language setup. Screenshots are taken from the repo's own demo with the headless browser, by a committed script (e.g. `npm run docs:screenshots`), against a stand-in API that returns real translations for the sample content, so the guides never show placeholder text. Use no real customer data, no secrets, no local URLs (show the live API endpoint). Keep the images small (1× scale, cropped to the relevant part), store them in `docs/images/`, give each one descriptive alt text, and regenerate them in the same commit whenever the UI they show changes.

## Shared Supertext protocol

AI file translation API v1, same as the WordPress plugin: POST HTML file → poll status → GET translation → DELETE. Details in `docs/DEVELOPER.md`. Never commit API keys; use the `SUPERTEXT_API_KEY` environment variable or the CMS's settings.

Lessons from testing against the live API (October 2026), to apply in every plugin:

- **Auth header:** `Authorization: Supertext-Auth-Key <key>`. The header name must be `Authorization` (`Authentication` gets 403; no prefix gets 400). Supertext shows the key with the prefix, so strip a pasted `Supertext-Auth-Key ` and always send exactly one.
- **Rate limit:** the API limits requests per second per key (HTTP 429, `RATE_LIMIT_EXCEEDED`). Translating into several languages at once hits it. Retry a 429 up to 4 times (`Retry-After`, else 1/2/4/8 s with jitter).
- **Rich text:** each element carrying `data-st-id` is translated on its own. Send a whole paragraph (heading, list item) as **one** `data-st-id` element with formatting and links as inline tags (`<b>`, `<i>`, `<a href>`), and map them back to the CMS's rich-text nodes. Never give each formatted run its own `data-st-id`: sentences break at the formatting (lower-case starts, words moved outside the tags).
- **Languages:** only the codes Supertext's `GET features` lists (the SDK's `Languages`, `Supertext.Umbraco.Translation.Sdk.V1`): bare sources (`en`); targets regional only for de/en/fr/it/pt (`de-CH`, `en-US`), `zh-Hans`/`zh-Hant`, `sr-Latn`/`sr-Cyrl`, `gsw-u-sd-chzh`/`chbe`, bare for the rest (`es`, `nl`, `sl`). **Defaults only where obvious:** the plugin's mapping (a combo box per Umbraco language on *Settings › Supertext* with the SDK's `TargetOptions()`, preselected with `SuggestTarget`, stored in `umbracoKeyValue` by `Services/LanguageSettings`; `Supertext:Languages:<culture>:Code` in appsettings.json overrides it), else an exact match, else the SDK default (de→de-DE, en→en-US, fr→fr-FR, it→it-IT, pt→pt-PT, zh-CN→zh-Hans, zh-TW→zh-Hant, es-ES→es …), else the error `unsupported_language` (en-IE, de-LI, gsw need a choice). See `docs/DEVELOPER.md` → *Language codes*.

## Umbraco specifics

- Supertext API calls go through the bundled SDK in `lib/supertext-sdk/` (namespace `Supertext.Umbraco.Translation.Sdk`, compiled into the package). Never edit it: fix the SDK in Supertext-API-SDK-and-Guide, then `scripts/vendor-sdk.sh`. Likewise Supertext Plugin Health in `lib/supertext-plugin-health/` (namespace `Supertext.Umbraco.Translation.PluginHealth`, compiled into the package): never edit it, fix it in Commando-Center-Supertext-Translation/plugin-health, then `scripts/vendor-plugin-health.sh <tag>`.
- Translation files (*Export for translation* / *Import translation*, XLIFF 1.2 and HTML via the SDK's `TranslationFiles`): bottom of the dialog, `POST …/supertext/export` and `…/import` in `SupertextTranslationController`, Umbraco-free glue in `Services/TranslationFileExchange` and `TranslationSegments` (unit-tested), collection and write-back in `ContentTranslator` (`CollectCulture`/`ApplyCulture`, the same as API translations); reference `document:<key>:<source>:<target>`. See `docs/DEVELOPER.md` → *Translation files*.
- Explicit action, not a hook: Umbraco has no "create language version" step, so editors use **Translate with Supertext** (workspace button and document actions menu). The backoffice extension is plain ES modules in `src/Supertext.Umbraco.Translation/wwwroot/App_Plugins/SupertextTranslation/` (no build step); the server side is `ContentTranslator` behind `SupertextTranslationController` (Management API, `/umbraco/management/api/v1/supertext/...`).
- Block values: rich text inside blocks arrives double-encoded (a JSON string containing JSON) and `editorAlias` is usually null, so the element type decides the editor. Keep the `(object?)` casts in `CollectBlockNode`: `JsonNode` converts implicitly from string.
- The demo's Clean starter kit is single-language; `DemoSetupService` makes it multilingual on every start. Clean writes its views only during its first-boot migration, so `demo/SupertextDemo/Views/` is committed.
- Commando Center (version reports, API key check, ping): `Services/SupertextReporting.cs` with Plugin Health's `InstallationReporter` and the SDK's `CheckApiKeyAsync` as key checker; state in `umbracoKeyValue`; public `GET /umbraco/supertext/ping` (`SupertextPingController`); the *Settings › Supertext* dashboard (`settings-dashboard.js`, `SupertextSettingsController`; the language mapping is edited there, everything else is read-only). Other settings stay in appsettings.json / the environment (`Supertext:SendVersionReports`, `SUPERTEXT_VERSION_REPORTS`). See `docs/DEVELOPER.md` → *Version reports and the API key check*.
- *Translation review* per target culture: the checkbox next to the combo box on *Settings › Supertext* (`SavedLanguage.Review`, `Supertext:Languages:<culture>:Review` wins); `ContentTranslator` takes `SupertextClientFactory.Create(review)`, base URL `ReviewUrl` / `SUPERTEXT_REVIEW_URL` (default `SupertextClientFactory.ReviewUrl`, `""` = off). Key check, reports and translation files never use it.
- UI strings live in `wwwroot/App_Plugins/SupertextTranslation/lang/` (`en.js`, `de.js`, `fr.js`, `it.js`); server errors shown to editors are `TranslationException` codes (the SDK's error codes are mapped in `TranslationException.SdkCodes`) with a `supertext_error_<code>` text there. `node Tests/Localization/check-keys.mjs` checks them.
- Test against the local stand-in (`Tests/Docs/stand-in.mjs`, `STAND_IN_PREFIX=1` marks untranslated text) before the live API.
- Docs screenshots: `Tests/Docs/screenshots.mjs`; the README's demo movie: `npm run movie` (`Tests/Docs/movie.mjs`, shared kit in `Tests/Docs/movie/`); the install and configuration movie: `npm run movie:install` (`Tests/Docs/movie-install.mjs`, same kit, MP4 only). See `docs/DEVELOPER.md` → *Demo movie*.
