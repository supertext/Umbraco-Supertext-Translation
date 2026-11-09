// Italian UI strings of the Supertext backoffice extension (keys: see en.js).
const SIGNUP = 'https://www.supertext.com/person/en/account/signin';
const API_KEY = 'https://www.supertext.com/en/integrations/api';

export default {
  supertext: {
    translateWithSupertext: 'Traduci con Supertext',
    translateFrom: 'Traduci da',
    into: 'Traduci in',
    sourceLanguage: 'Lingua di origine',
    translate: 'Traduci',
    replaceAndTranslate: 'Sostituisci e traduci',
    hasContent: 'ha contenuto',
    translated: 'tradotto',
    failed: 'non riuscito',
    hint: 'La pagina viene tradotta così come è salvata: salvi prima le Sue modifiche. Le traduzioni vengono salvate come bozze, che potrà rivedere e pubblicare.',
    overwriteWarning: (languages, count) => `${languages} ${count === 1 ? 'ha' : 'hanno'} già contenuto.`,
    overwriteExplanation: (source) => `La traduzione sostituisce la bozza attuale con una nuova traduzione della versione ${source}.`,
    noApiKeyHtml:
      'Non è configurata alcuna chiave API Supertext. Chieda al Suo amministratore di impostare <code>SUPERTEXT_API_KEY</code>. ' +
      `Non ha ancora un account Supertext? <a href="${SIGNUP}" target="_blank" rel="noopener">Ne crei uno su supertext.com</a>. ` +
      `Generi la Sua chiave API su <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (richiede il ruolo Admin).`,
    loadLanguagesFailed: 'Impossibile caricare le lingue di questo documento.',
    startFailed: 'Non è stato possibile avviare la traduzione.',
    translatedHeadline: 'Tradotto con Supertext',
    translatedMessage: (languages) => `${languages}: salvato come bozza. Lo riveda e lo pubblichi quando è pronto.`,
    failedHeadline: 'Traduzione Supertext non riuscita',

    error_chooseLanguages: 'Scelga una lingua di origine e almeno una lingua di destinazione.',
    error_documentNotFound: 'Documento non trovato.',
    error_invariantDocumentType: (name) =>
      `Il tipo di documento «${name}» non varia in base alla lingua, quindi non ha versioni linguistiche separate in cui tradurre.`,
    error_noSourceVersion: (culture) => `Il documento non ha una versione ${culture} da cui tradurre.`,
    error_saveFailed: 'Umbraco non è riuscito a salvare il documento tradotto.',
    error_noApiKey:
      'Non è configurata alcuna chiave API Supertext (Supertext:ApiKey o SUPERTEXT_API_KEY). ' +
      `Non ha ancora un account Supertext? Ne crei uno su ${SIGNUP}. ` +
      `Generi la Sua chiave API su ${API_KEY} (supertext.com → Integrations → API, richiede il ruolo Admin).`,
    error_authFailed:
      'Autenticazione non riuscita. Verifichi la chiave API Supertext. ' +
      `Non ha ancora un account Supertext? Ne crei uno su ${SIGNUP}. ` +
      `Generi la Sua chiave API su ${API_KEY} (supertext.com → Integrations → API, richiede il ruolo Admin).`,
    error_unreachable: 'Impossibile raggiungere Supertext.',
    error_languagePair: 'Supertext non supporta questa coppia di lingue. Verifichi l’associazione delle lingue (Supertext:Languages:<culture>:Code, vedere Impostazioni › Supertext).',
    error_notFound: 'La risorsa Supertext richiesta non è stata trovata.',
    error_tooLarge: 'Il contenuto è troppo grande per essere tradotto da Supertext in una sola volta.',
    error_rateLimited: 'Troppe richieste a Supertext. Riprovi tra poco.',
    error_unavailable: 'Il servizio Supertext non è al momento disponibile.',
    error_http: (status) => `Supertext ha risposto con HTTP ${status}.`,
    error_noFileId: 'Supertext non ha restituito un ID file.',
    error_translationFailed: 'Supertext non è riuscito a tradurre il documento.',
    error_limitExceeded: 'Il Suo limite di traduzione Supertext è stato superato.',
    error_fileDeleted: 'Il file Supertext è stato eliminato prima di poter essere scaricato.',
    error_timeout: 'Tempo scaduto in attesa della traduzione Supertext.',
    error_emptyTranslation: 'La traduzione di Supertext era vuota o incompleta.',

    notMapped: 'non associata',
    notMappedHint: (culture) => `${culture} non è ancora associata a una lingua Supertext. Il Suo amministratore può associarla in Impostazioni › Supertext.`,
    error_unsupportedLanguage: (language) =>
      `La lingua ${language} non è ancora associata a una lingua Supertext. Chieda al Suo amministratore di sceglierne una in Impostazioni › Supertext.`,
    error_unsupportedFeature:
      'Supertext non offre un tono formale o informale per questa lingua. Chieda al Suo amministratore di impostare il tono predefinito in Impostazioni › Supertext.',

    dashboardLabel: 'Supertext',
    dashboardHeadline: 'Supertext Translation',
    dashboardLoadFailed: 'Impossibile caricare le impostazioni Supertext.',
    version: 'Versione',
    endpoint: 'Endpoint API',
    apiKey: 'Chiave API',
    apiKeySet: 'Impostata',
    apiKeyNotSet: 'Non impostata (Supertext:ApiKey o SUPERTEXT_API_KEY)',
    keyCheck: 'Verifica della chiave API',
    checkedAt: (time) => `verificata ${time}`,
    notChecked: 'Non ancora verificata.',
    checkKey: 'Verifica chiave API',
    checkFailed: 'Non è stato possibile verificare la chiave API.',
    keycheck_ok: 'Chiave API verificata: la connessione a Supertext funziona.',
    keycheck_invalid:
      `Supertext ha rifiutato la chiave API. Verifichi di aver copiato l'intera chiave oppure ne generi una nuova su <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (richiede il ruolo Admin). ` +
      `Non ha ancora un account Supertext? Ne crei uno su <a href="${SIGNUP}" target="_blank" rel="noopener">supertext.com</a>.`,
    keycheck_missing:
      `Non è ancora stata inserita alcuna chiave API. Non ha ancora un account Supertext? Ne crei uno su <a href="${SIGNUP}" target="_blank" rel="noopener">supertext.com</a>. ` +
      `Generi la Sua chiave API su <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (richiede il ruolo Admin).`,
    keycheck_unreachable: 'Non è stato possibile verificare la chiave API perché Supertext non è raggiungibile. Le impostazioni sono salvate; riprovi più tardi.',
    keycheck_error: (status) => `Non è stato possibile verificare la chiave API: Supertext ha risposto con un errore (${status}). Le impostazioni sono salvate; riprovi più tardi.`,
    reports: 'Inviare le informazioni sulla versione a Supertext',
    reportsOn: 'Attivo',
    reportsOff: 'Disattivato',
    reportsHelp:
      "Invia a Supertext l'URL del sito e le versioni di questo pacchetto, di Umbraco e dell'ambiente di esecuzione dopo un aggiornamento e quando la chiave API cambia. In questo modo l'assistenza Supertext vede quale versione utilizza. Non vengono trasmessi contenuti, chiavi API né dati personali.",
    reportsHow: 'Per disattivarlo, imposti Supertext:SendVersionReports su false in appsettings.json oppure la variabile d’ambiente SUPERTEXT_VERSION_REPORTS=0.',
    pingUrl: 'Supertext verifica che il sito sia raggiungibile all’indirizzo',
    languagesHeadline: 'Lingue',
    languagesHint:
      'Scelga la lingua Supertext di ogni lingua Umbraco; vale anche quando la lingua è la lingua di partenza. È preselezionata la lingua Supertext corrispondente o quella usuale (it → italiano (Italia)); ne scelga un’altra se necessario, ad es. italiano (Svizzera). «— scegliere —»: Supertext non ha una lingua usuale per questa lingua, ne scelga una prima di tradurre. Le voci sotto Supertext:Languages in appsettings.json hanno la precedenza su questa pagina.',
    umbracoLanguage: 'Lingua Umbraco',
    supertextLanguage: 'Lingua Supertext',
    tone: 'Formale/informale',
    fromMapping: 'dall’associazione',
    defaultTarget: 'predefinito di Supertext',
    exactMatch: 'stesso codice',
    notMappedLong: (culture) => `Scelga una lingua Supertext prima di tradurre in ${culture}.`,
    formal: 'formale',
    informal: 'informale',
    defaultTone: 'predefinito',
    noPoliteness: 'Supertext non offre una scelta formale/informale per questa lingua',
    hiddenInDialog: 'nascosta nella finestra di dialogo',
    choose: '— scegliere —',
    fromConfig: (culture) => `impostato in appsettings.json (Supertext:Languages:${culture}:Code)`,
    politenessFromConfig: (culture) => `impostato in appsettings.json (Supertext:Languages:${culture}:Politeness)`,
    saveLanguages: 'Salva',
    languagesSaved: 'Lingue salvate.',
    saveFailed: 'Non è stato possibile salvare le lingue.',
    review: 'Revisione delle traduzioni',
    reviewHelp:
      'Revisione delle traduzioni: le traduzioni in una lingua selezionata passano dal servizio di revisione di Supertext, così i revisori possono controllarle e correggerle direttamente sul sito con l’estensione Supertext Localization Review. Le loro correzioni vengono riprese alla prossima traduzione della pagina.',
    reviewFromConfig: (culture) => `impostato in appsettings.json (Supertext:Languages:${culture}:Review)`,
    reviewOff: 'La revisione delle traduzioni è disattivata su questo sito (Supertext:ReviewUrl è vuoto).',
    // Esporta per la traduzione / importa traduzione (file XLIFF 1.2 o HTML)
    exportForTranslation: 'Esporta per la traduzione',
    exportHint: 'Scarichi i testi della lingua di partenza per le lingue di destinazione selezionate sopra come file per un traduttore o uno strumento di traduzione, oppure lo carichi nell’app web di Supertext. Importi poi il file tradotto qui sotto.',
    fileFormat: 'Formato del file',
    formatXliff: 'XLIFF 1.2 (per strumenti di traduzione)',
    formatHtml: 'HTML (una lingua per file)',
    downloadFile: 'Scarica il file',
    exportFailed: 'Non è stato possibile esportare il file.',
    importTranslation: 'Importa traduzione',
    importHint: 'I testi importati vengono salvati come bozze, come le traduzioni con Supertext. Li verifichi e li pubblichi quando sono pronti.',
    translatedFile: 'File tradotto (XLIFF o HTML)',
    includeChanged: 'Importa anche i testi il cui testo di partenza è cambiato dopo l’esportazione',
    importOverwrite: 'Sostituisci le lingue che hanno già contenuto',
    importFailed: 'Non è stato possibile importare il file.',
    importExists: 'Questa lingua ha già contenuto. Selezioni «Sostituisci le lingue che hanno già contenuto» e importi di nuovo il file.',
    importSaved: 'Salvato come bozza.',
    importNothing: 'Non è stato importato nulla.',
    summaryImported: (count) => `Importati: ${count}`,
    summaryChanged: (count) => `Saltati, testo di partenza modificato dopo l’esportazione: ${count}`,
    summaryUntranslated: (count) => `Non tradotti nel file: ${count}`,
    summaryInvalid: (count) => `Formattazione modificata, non importati: ${count}`,
    summaryMissing: (count) => `Non presenti nel file: ${count}`,
    error_nothingToTranslate: 'Niente da tradurre.',
    error_htmlOneLanguage: 'Un file HTML contiene una sola lingua: ne scelga esattamente una.',
    error_chooseFile: 'Scelga un file da importare.',
    error_invalidFile: 'Questo file non è un file di traduzione XLIFF o HTML valido.',
    error_missingMetadata: 'Questo file non è stato esportato dal pacchetto Supertext oppure la sua intestazione è stata rimossa.',
    error_fileTooLarge: 'Il file è troppo grande (al massimo 10 MB).',
    error_otherSystem: (plugin) => `Questo file appartiene a un altro sistema (${plugin}).`,
    error_fileDocumentNotFound: 'Il documento da cui è stato esportato questo file non esiste più.',
    error_noLanguage: (language) => `Il sito non ha la lingua ${language}.`,
    error_notAllowed: 'Non è autorizzato a modificare questo documento in questa lingua.',
  },
};
