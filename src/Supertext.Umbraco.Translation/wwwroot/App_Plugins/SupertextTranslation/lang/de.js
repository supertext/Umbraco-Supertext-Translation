// German UI strings of the Supertext backoffice extension (keys: see en.js).
const SIGNUP = 'https://www.supertext.com/person/en/account/signin';
const API_KEY = 'https://www.supertext.com/en/integrations/api';

export default {
  supertext: {
    translateWithSupertext: 'Mit Supertext übersetzen',
    translateFrom: 'Übersetzen aus',
    into: 'Übersetzen in',
    sourceLanguage: 'Ausgangssprache',
    translate: 'Übersetzen',
    replaceAndTranslate: 'Ersetzen und übersetzen',
    hasContent: 'hat Inhalt',
    translated: 'übersetzt',
    failed: 'fehlgeschlagen',
    hint: 'Übersetzt wird der gespeicherte Stand der Seite: Speichern Sie Ihre Änderungen zuerst. Die Übersetzungen werden als Entwürfe gespeichert, die Sie prüfen und veröffentlichen.',
    overwriteWarning: (languages, count) => `${languages} ${count === 1 ? 'hat' : 'haben'} bereits Inhalt.`,
    overwriteExplanation: (source) => `Beim Übersetzen wird der aktuelle Entwurf dort durch eine neue Übersetzung der Version ${source} ersetzt.`,
    noApiKeyHtml:
      'Es ist kein Supertext-API-Schlüssel konfiguriert. Bitten Sie Ihre Administratorin oder Ihren Administrator, <code>SUPERTEXT_API_KEY</code> zu setzen. ' +
      `Noch kein Supertext-Konto? <a href="${SIGNUP}" target="_blank" rel="noopener">Erstellen Sie eines auf supertext.com</a>. ` +
      `Generieren Sie Ihren API-Schlüssel auf <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (erfordert die Admin-Rolle).`,
    loadLanguagesFailed: 'Die Sprachen dieses Dokuments konnten nicht geladen werden.',
    startFailed: 'Die Übersetzung konnte nicht gestartet werden.',
    translatedHeadline: 'Mit Supertext übersetzt',
    translatedMessage: (languages) => `${languages} als Entwurf gespeichert. Prüfen Sie die Übersetzung und veröffentlichen Sie sie, wenn sie bereit ist.`,
    failedHeadline: 'Supertext-Übersetzung fehlgeschlagen',

    error_chooseLanguages: 'Wählen Sie eine Ausgangssprache und mindestens eine Zielsprache.',
    error_documentNotFound: 'Dokument nicht gefunden.',
    error_invariantDocumentType: (name) =>
      `Der Dokumenttyp „${name}“ variiert nicht nach Kultur und hat daher keine eigenen Sprachversionen, in die übersetzt werden kann.`,
    error_noSourceVersion: (culture) => `Das Dokument hat keine Version in ${culture}, aus der übersetzt werden kann.`,
    error_saveFailed: 'Umbraco konnte das übersetzte Dokument nicht speichern.',
    error_noApiKey:
      'Es ist kein Supertext-API-Schlüssel konfiguriert (Supertext:ApiKey oder SUPERTEXT_API_KEY). ' +
      `Noch kein Supertext-Konto? Erstellen Sie eines auf ${SIGNUP}. ` +
      `Generieren Sie Ihren API-Schlüssel auf ${API_KEY} (supertext.com → Integrations → API, erfordert die Admin-Rolle).`,
    error_authFailed:
      'Authentifizierung fehlgeschlagen. Bitte prüfen Sie den Supertext-API-Schlüssel. ' +
      `Noch kein Supertext-Konto? Erstellen Sie eines auf ${SIGNUP}. ` +
      `Generieren Sie Ihren API-Schlüssel auf ${API_KEY} (supertext.com → Integrations → API, erfordert die Admin-Rolle).`,
    error_unreachable: 'Supertext ist nicht erreichbar.',
    error_languagePair: 'Supertext unterstützt dieses Sprachpaar nicht. Prüfen Sie die Sprachzuordnung (Supertext:Languages:<culture>:Code, siehe Einstellungen › Supertext).',
    error_notFound: 'Die angeforderte Supertext-Ressource wurde nicht gefunden.',
    error_tooLarge: 'Der Inhalt ist zu groß, um von Supertext in einem Durchgang übersetzt zu werden.',
    error_rateLimited: 'Zu viele Anfragen an Supertext. Bitte versuchen Sie es gleich noch einmal.',
    error_unavailable: 'Der Supertext-Dienst ist zurzeit nicht verfügbar.',
    error_http: (status) => `Supertext hat mit HTTP ${status} geantwortet.`,
    error_noFileId: 'Supertext hat keine Datei-ID zurückgegeben.',
    error_translationFailed: 'Supertext konnte das Dokument nicht übersetzen.',
    error_limitExceeded: 'Ihr Supertext-Übersetzungslimit ist überschritten.',
    error_fileDeleted: 'Die Supertext-Datei wurde gelöscht, bevor sie heruntergeladen werden konnte.',
    error_timeout: 'Zeitüberschreitung beim Warten auf die Supertext-Übersetzung.',
    error_emptyTranslation: 'Die Übersetzung von Supertext war leer oder unvollständig.',

    notMapped: 'nicht zugeordnet',
    notMappedHint: (culture) => `${culture} ist noch keiner Supertext-Sprache zugeordnet. Ihre Administratorin oder Ihr Administrator kann sie unter Einstellungen › Supertext zuordnen.`,
    error_unsupportedLanguage: (language) =>
      `Die Sprache ${language} ist noch keiner Supertext-Sprache zugeordnet. Bitten Sie Ihre Administratorin oder Ihren Administrator, unter Einstellungen › Supertext eine zu wählen.`,
    error_unsupportedFeature:
      'Supertext bietet für diese Sprache keine formelle oder informelle Anrede an. Bitten Sie Ihre Administratorin oder Ihren Administrator, unter Einstellungen › Supertext die Standard-Anrede einzustellen.',

    dashboardLabel: 'Supertext',
    dashboardHeadline: 'Supertext Translation',
    dashboardLoadFailed: 'Die Supertext-Einstellungen konnten nicht geladen werden.',
    version: 'Version',
    endpoint: 'API-Endpunkt',
    apiKey: 'API-Schlüssel',
    apiKeySet: 'Gesetzt',
    apiKeyNotSet: 'Nicht gesetzt (Supertext:ApiKey oder SUPERTEXT_API_KEY)',
    keyCheck: 'Prüfung des API-Schlüssels',
    checkedAt: (time) => `geprüft ${time}`,
    notChecked: 'Noch nicht geprüft.',
    checkKey: 'API-Schlüssel prüfen',
    checkFailed: 'Der API-Schlüssel konnte nicht geprüft werden.',
    keycheck_ok: 'API-Schlüssel geprüft: Die Verbindung zu Supertext funktioniert.',
    keycheck_invalid:
      `Supertext hat den API-Schlüssel abgelehnt. Prüfen Sie, ob Sie den ganzen Schlüssel kopiert haben, oder erstellen Sie einen neuen unter <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (erfordert die Rolle Admin). ` +
      `Noch kein Supertext-Konto? Erstellen Sie eines auf <a href="${SIGNUP}" target="_blank" rel="noopener">supertext.com</a>.`,
    keycheck_missing:
      `Es ist noch kein API-Schlüssel eingetragen. Noch kein Supertext-Konto? Erstellen Sie eines auf <a href="${SIGNUP}" target="_blank" rel="noopener">supertext.com</a>. ` +
      `Ihren API-Schlüssel erstellen Sie unter <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (erfordert die Rolle Admin).`,
    keycheck_unreachable: 'Der API-Schlüssel konnte nicht geprüft werden, weil Supertext nicht erreichbar ist. Die Einstellungen sind gespeichert; prüfen Sie ihn später erneut.',
    keycheck_error: (status) => `Der API-Schlüssel konnte nicht geprüft werden: Supertext hat mit einem Fehler geantwortet (${status}). Die Einstellungen sind gespeichert; prüfen Sie ihn später erneut.`,
    reports: 'Versionsinformationen an Supertext senden',
    reportsOn: 'Ein',
    reportsOff: 'Aus',
    reportsHelp:
      'Sendet die URL der Website sowie die Versionen dieses Pakets, von Umbraco und der Laufzeitumgebung an Supertext, nach einem Update und wenn sich der API-Schlüssel ändert. So sieht der Supertext-Support, welche Version Sie einsetzen. Es werden keine Inhalte, kein API-Schlüssel und keine Personendaten übertragen.',
    reportsHow: 'Zum Ausschalten setzen Sie Supertext:SendVersionReports in appsettings.json auf false oder die Umgebungsvariable SUPERTEXT_VERSION_REPORTS=0.',
    pingUrl: 'Supertext prüft die Erreichbarkeit der Website unter',
    languagesHeadline: 'Sprachen',
    languagesHint:
      'Wählen Sie für jede Umbraco-Sprache die Supertext-Sprache; sie gilt auch, wenn die Sprache die Ausgangssprache ist. Vorausgewählt ist die passende oder die übliche Supertext-Sprache (de → Deutsch (Deutschland)); wählen Sie bei Bedarf eine andere, z. B. Schweizerdeutsch. „— bitte wählen —“: Supertext hat für diese Sprache keine übliche Sprache, wählen Sie vor dem Übersetzen eine aus. Einträge unter Supertext:Languages in appsettings.json haben Vorrang vor dieser Seite.',
    umbracoLanguage: 'Umbraco-Sprache',
    supertextLanguage: 'Supertext-Sprache',
    tone: 'Formell/informell',
    fromMapping: 'aus der Zuordnung',
    defaultTarget: 'Standard von Supertext',
    exactMatch: 'gleicher Code',
    notMappedLong: (culture) => `Wählen Sie eine Supertext-Sprache, bevor Sie nach ${culture} übersetzen.`,
    formal: 'formell',
    informal: 'informell',
    defaultTone: 'Standard',
    noPoliteness: 'Supertext bietet für diese Sprache keine formelle/informelle Anrede an',
    hiddenInDialog: 'im Dialog ausgeblendet',
    choose: '— bitte wählen —',
    fromConfig: (culture) => `in appsettings.json festgelegt (Supertext:Languages:${culture}:Code)`,
    politenessFromConfig: (culture) => `in appsettings.json festgelegt (Supertext:Languages:${culture}:Politeness)`,
    saveLanguages: 'Speichern',
    languagesSaved: 'Sprachen gespeichert.',
    saveFailed: 'Die Sprachen konnten nicht gespeichert werden.',
    review: 'Übersetzungsprüfung',
    reviewHelp:
      'Übersetzungsprüfung: Übersetzungen in eine angehakte Sprache laufen über den Prüfdienst von Supertext, damit Prüferinnen und Prüfer sie mit der Erweiterung Supertext Localization Review direkt auf der Website kontrollieren und korrigieren können. Ihre Korrekturen werden beim nächsten Übersetzen der Seite übernommen.',
    reviewFromConfig: (culture) => `in appsettings.json festgelegt (Supertext:Languages:${culture}:Review)`,
    reviewOff: 'Die Übersetzungsprüfung ist auf dieser Website ausgeschaltet (Supertext:ReviewUrl ist leer).',
    // Für Übersetzung exportieren / Übersetzung importieren (XLIFF 1.2 oder HTML)
    exportForTranslation: 'Für Übersetzung exportieren',
    exportHint: 'Laden Sie die Texte der Ausgangssprache für die oben angekreuzten Zielsprachen als Datei für eine Übersetzerin, einen Übersetzer oder ein Übersetzungstool herunter, oder laden Sie sie in die Web-App von Supertext hoch. Importieren Sie die übersetzte Datei unten.',
    fileFormat: 'Dateiformat',
    formatXliff: 'XLIFF 1.2 (für Übersetzungstools)',
    formatHtml: 'HTML (eine Sprache pro Datei)',
    downloadFile: 'Datei herunterladen',
    exportFailed: 'Die Datei konnte nicht exportiert werden.',
    importTranslation: 'Übersetzung importieren',
    importHint: 'Importierte Texte werden wie Übersetzungen mit Supertext als Entwürfe gespeichert. Prüfen und veröffentlichen Sie sie, wenn sie bereit sind.',
    translatedFile: 'Übersetzte Datei (XLIFF oder HTML)',
    includeChanged: 'Auch Texte importieren, deren Ausgangstext sich seit dem Export geändert hat',
    importOverwrite: 'Sprachen ersetzen, die bereits Inhalt haben',
    importFailed: 'Die Datei konnte nicht importiert werden.',
    importExists: 'Diese Sprache hat bereits Inhalt. Kreuzen Sie «Sprachen ersetzen, die bereits Inhalt haben» an und importieren Sie die Datei erneut.',
    importSaved: 'Als Entwurf gespeichert.',
    importNothing: 'Es wurde nichts importiert.',
    summaryImported: (count) => `Importiert: ${count}`,
    summaryChanged: (count) => `Übersprungen, Ausgangstext seit dem Export geändert: ${count}`,
    summaryUntranslated: (count) => `In der Datei nicht übersetzt: ${count}`,
    summaryInvalid: (count) => `Formatierung verändert, nicht importiert: ${count}`,
    summaryMissing: (count) => `Nicht in der Datei: ${count}`,
    error_nothingToTranslate: 'Nichts zu übersetzen.',
    error_htmlOneLanguage: 'Eine HTML-Datei enthält eine Sprache: Wählen Sie genau eine.',
    error_chooseFile: 'Wählen Sie eine Datei zum Importieren.',
    error_invalidFile: 'Diese Datei ist keine gültige XLIFF- oder HTML-Übersetzungsdatei.',
    error_missingMetadata: 'Diese Datei wurde nicht vom Supertext-Paket exportiert, oder ihr Kopfbereich wurde entfernt.',
    error_fileTooLarge: 'Die Datei ist zu groß (höchstens 10 MB).',
    error_otherSystem: (plugin) => `Diese Datei gehört zu einem anderen System (${plugin}).`,
    error_fileDocumentNotFound: 'Das Dokument, aus dem diese Datei exportiert wurde, existiert nicht mehr.',
    error_noLanguage: (language) => `Die Website hat keine Sprache ${language}.`,
    error_notAllowed: 'Sie dürfen dieses Dokument in dieser Sprache nicht bearbeiten.',
  },
};
