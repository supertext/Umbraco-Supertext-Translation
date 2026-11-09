// English UI strings of the Supertext backoffice extension (also the fallback for every other language).
// Keys are used as `supertext_<key>` (e.g. this.localize.term('supertext_translate')) or `#supertext_<key>` in manifests.
// Every key here must exist in de.js, fr.js and it.js too (checked by Tests/Localization/check-keys.mjs).
const SIGNUP = 'https://www.supertext.com/person/en/account/signin';
const API_KEY = 'https://www.supertext.com/en/integrations/api';

export default {
  supertext: {
    translateWithSupertext: 'Translate with Supertext',
    translateFrom: 'Translate from',
    into: 'Into',
    sourceLanguage: 'Source language',
    translate: 'Translate',
    replaceAndTranslate: 'Replace and translate',
    hasContent: 'has content',
    translated: 'translated',
    failed: 'failed',
    hint: 'The page is translated as saved: save your changes first. Translations are saved as drafts for you to review and publish.',
    overwriteWarning: (languages, count) => `${languages} already ${count === 1 ? 'has' : 'have'} content.`,
    overwriteExplanation: (source) => `Translating replaces the current draft there with a new translation of the ${source} version.`,
    noApiKeyHtml:
      'No Supertext API key is configured. Ask your administrator to set <code>SUPERTEXT_API_KEY</code>. ' +
      `No Supertext account yet? <a href="${SIGNUP}" target="_blank" rel="noopener">Create one at supertext.com</a>. ` +
      `Generate your API key at <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (requires the Admin role).`,
    loadLanguagesFailed: 'Could not load the languages of this document.',
    startFailed: 'The translation could not be started.',
    translatedHeadline: 'Translated with Supertext',
    translatedMessage: (languages) => `${languages} saved as draft. Review and publish when ready.`,
    failedHeadline: 'Supertext translation failed',

    // Errors from the server (code → text); the server also sends the English text as a fallback.
    error_chooseLanguages: 'Choose a source language and at least one target language.',
    error_documentNotFound: 'Document not found.',
    error_invariantDocumentType: (name) =>
      `The document type "${name}" does not vary by culture, so it has no separate language versions to translate into.`,
    error_noSourceVersion: (culture) => `The document has no ${culture} version to translate from.`,
    error_saveFailed: 'Umbraco could not save the translated document.',
    error_noApiKey:
      'No Supertext API key is configured (Supertext:ApiKey or SUPERTEXT_API_KEY). ' +
      `No Supertext account yet? Create one at ${SIGNUP}. ` +
      `Generate your API key at ${API_KEY} (supertext.com → Integrations → API, requires the Admin role).`,
    error_authFailed:
      'Authentication failed. Please check the Supertext API key. ' +
      `No Supertext account yet? Create one at ${SIGNUP}. ` +
      `Generate your API key at ${API_KEY} (supertext.com → Integrations → API, requires the Admin role).`,
    error_unreachable: 'Could not reach Supertext.',
    error_languagePair: 'Supertext does not support this language pair. Check the language mapping (Supertext:Languages:<culture>:Code, see Settings › Supertext).',
    error_notFound: 'The requested Supertext resource was not found.',
    error_tooLarge: 'The content is too large for Supertext to translate in one go.',
    error_rateLimited: 'Too many requests to Supertext. Please try again shortly.',
    error_unavailable: 'The Supertext service is currently unavailable.',
    error_http: (status) => `Supertext answered with HTTP ${status}.`,
    error_noFileId: 'Supertext did not return a file id.',
    error_translationFailed: 'Supertext failed to translate the document.',
    error_limitExceeded: 'Your Supertext translation limit is exceeded.',
    error_fileDeleted: 'The Supertext file was deleted before it could be downloaded.',
    error_timeout: 'Timed out waiting for the Supertext translation.',
    error_emptyTranslation: 'The translation from Supertext was empty or incomplete.',

    // Languages: the mapping (Settings › Supertext, Supertext:Languages:<culture>:Code wins), else an exact Supertext code, else the SDK default.
    notMapped: 'not mapped',
    notMappedHint: (culture) => `${culture} has no Supertext language yet. Your administrator can map it on Settings › Supertext.`,
    error_unsupportedLanguage: (language) =>
      `The language ${language} has no Supertext language yet. Ask your administrator to choose one on Settings › Supertext.`,
    error_unsupportedFeature:
      'Supertext offers no formal or informal tone for this language. Ask your administrator to set it to the default tone on Settings › Supertext.',

    // Supertext dashboard (Settings section)
    dashboardLabel: 'Supertext',
    dashboardHeadline: 'Supertext Translation',
    dashboardLoadFailed: 'Could not load the Supertext settings.',
    version: 'Version',
    endpoint: 'API endpoint',
    apiKey: 'API key',
    apiKeySet: 'Set',
    apiKeyNotSet: 'Not set (Supertext:ApiKey or SUPERTEXT_API_KEY)',
    keyCheck: 'API key check',
    checkedAt: (time) => `checked ${time}`,
    notChecked: 'Not checked yet.',
    checkKey: 'Check API key',
    checkFailed: 'The API key could not be checked.',
    // Results of the automatic API key check (texts of the Supertext Commando Center).
    keycheck_ok: 'API key checked: the connection to Supertext works.',
    keycheck_invalid:
      `Supertext rejected the API key. Check that you copied the whole key, or generate a new one at <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (requires the Admin role). ` +
      `No Supertext account yet? Create one at <a href="${SIGNUP}" target="_blank" rel="noopener">supertext.com</a>.`,
    keycheck_missing:
      `No API key is set yet. No Supertext account yet? Create one at <a href="${SIGNUP}" target="_blank" rel="noopener">supertext.com</a>. ` +
      `Generate your API key at <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (requires the Admin role).`,
    keycheck_unreachable: "The API key couldn't be checked because Supertext can't be reached. The settings are saved; check again later.",
    keycheck_error: (status) => `The API key couldn't be checked: Supertext answered with an error (${status}). The settings are saved; check again later.`,
    reports: 'Send version information to Supertext',
    reportsOn: 'On',
    reportsOff: 'Off',
    reportsHelp:
      'Sends the site URL and the versions of this package, Umbraco and the runtime to Supertext after an update and when the API key changes, so Supertext support can see which version you run. No content, no API key and no personal data are sent.',
    reportsHow: 'To turn it off, set Supertext:SendVersionReports to false in appsettings.json or the environment variable SUPERTEXT_VERSION_REPORTS=0.',
    pingUrl: 'Supertext checks that the site is reachable at',
    languagesHeadline: 'Languages',
    languagesHint:
      'Choose the Supertext language for each Umbraco language; it is also used when the language is the source. Preselected is the matching Supertext language or the usual one (de → German (Germany)); choose another one if needed, e.g. Swiss German. “— choose —”: Supertext has no usual language for this language, choose one before translating. Entries under Supertext:Languages in appsettings.json win over this screen.',
    umbracoLanguage: 'Umbraco language',
    supertextLanguage: 'Supertext language',
    tone: 'Formal/informal',
    fromMapping: 'from the mapping',
    defaultTarget: 'Supertext default',
    exactMatch: 'same code',
    notMappedLong: (culture) => `Choose a Supertext language before translating into ${culture}.`,
    formal: 'formal',
    informal: 'informal',
    defaultTone: 'default',
    noPoliteness: 'Supertext offers no formal/informal choice for this language',
    hiddenInDialog: 'hidden in the dialog',
    choose: '— choose —',
    fromConfig: (culture) => `set in appsettings.json (Supertext:Languages:${culture}:Code)`,
    politenessFromConfig: (culture) => `set in appsettings.json (Supertext:Languages:${culture}:Politeness)`,
    saveLanguages: 'Save',
    languagesSaved: 'Languages saved.',
    saveFailed: 'Could not save the languages.',
    review: 'Translation review',
    reviewHelp:
      'Translation review: translations into a ticked language go through Supertext\'s review service, so reviewers can check and correct them on the website with the Supertext Localization Review extension. Their corrections are used the next time the page is translated.',
    reviewFromConfig: (culture) => `set in appsettings.json (Supertext:Languages:${culture}:Review)`,
    reviewOff: 'Translation review is turned off on this site (Supertext:ReviewUrl is empty).',
    // Export for translation / import translation (XLIFF 1.2 or HTML files)
    exportForTranslation: 'Export for translation',
    exportHint: 'Download the texts of the source language as a file for a translator or a translation tool, or upload it to Supertext\'s web app, for the target languages ticked above. Import the translated file below.',
    fileFormat: 'File format',
    formatXliff: 'XLIFF 1.2 (for translation tools)',
    formatHtml: 'HTML (one language per file)',
    downloadFile: 'Download file',
    exportFailed: 'The file could not be exported.',
    importTranslation: 'Import translation',
    importHint: 'Imported texts are saved as drafts, like translations with Supertext. Review and publish them when they\'re ready.',
    translatedFile: 'Translated file (XLIFF or HTML)',
    includeChanged: 'Also import texts whose source changed since the export',
    importOverwrite: 'Replace languages that already have content',
    importFailed: 'The file could not be imported.',
    importExists: 'This language already has content. Tick “Replace languages that already have content” and import the file again.',
    importSaved: 'Saved as a draft.',
    importNothing: 'Nothing was imported.',
    summaryImported: (count) => `Imported: ${count}`,
    summaryChanged: (count) => `Skipped, source changed since the export: ${count}`,
    summaryUntranslated: (count) => `Not translated in the file: ${count}`,
    summaryInvalid: (count) => `Formatting changed, not imported: ${count}`,
    summaryMissing: (count) => `Not in the file: ${count}`,
    error_nothingToTranslate: 'Nothing to translate.',
    error_htmlOneLanguage: 'An HTML file holds one language: choose exactly one.',
    error_chooseFile: 'Choose a file to import.',
    error_invalidFile: "This file isn't a valid XLIFF or HTML translation file.",
    error_missingMetadata: "This file wasn't exported by the Supertext package, or its header was removed.",
    error_fileTooLarge: 'The file is too large (at most 10 MB).',
    error_otherSystem: (plugin) => `This file belongs to another system (${plugin}).`,
    error_fileDocumentNotFound: 'The document this file was exported from no longer exists.',
    error_noLanguage: (language) => `The site has no language ${language}.`,
    error_notAllowed: 'You may not edit this document in this language.',
  },
};
