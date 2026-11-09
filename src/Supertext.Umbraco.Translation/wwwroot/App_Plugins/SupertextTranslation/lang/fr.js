// French UI strings of the Supertext backoffice extension (keys: see en.js).
//   = non-breaking space before ? ! : ; and inside « ».
const SIGNUP = 'https://www.supertext.com/person/en/account/signin';
const API_KEY = 'https://www.supertext.com/en/integrations/api';

export default {
  supertext: {
    translateWithSupertext: 'Traduire avec Supertext',
    translateFrom: 'Traduire depuis',
    into: 'Traduire vers',
    sourceLanguage: 'Langue source',
    translate: 'Traduire',
    replaceAndTranslate: 'Remplacer et traduire',
    hasContent: 'a du contenu',
    translated: 'traduit',
    failed: 'échec',
    hint: 'La page est traduite telle qu’elle est sauvegardée : sauvegardez d’abord vos modifications. Les traductions sont sauvegardées comme brouillons, que vous pouvez relire puis publier.',
    overwriteWarning: (languages, count) => `${languages} ${count === 1 ? 'a' : 'ont'} déjà du contenu.`,
    overwriteExplanation: (source) => `La traduction y remplace le brouillon actuel par une nouvelle traduction de la version ${source}.`,
    noApiKeyHtml:
      'Aucune clé API Supertext n’est configurée. Demandez à votre administrateur de définir <code>SUPERTEXT_API_KEY</code>. ' +
      `Pas encore de compte Supertext ? <a href="${SIGNUP}" target="_blank" rel="noopener">Créez-en un sur supertext.com</a>. ` +
      `Générez votre clé API sur <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (rôle Admin requis).`,
    loadLanguagesFailed: 'Impossible de charger les langues de ce document.',
    startFailed: 'La traduction n’a pas pu être lancée.',
    translatedHeadline: 'Traduit avec Supertext',
    translatedMessage: (languages) => `${languages} : sauvegardé comme brouillon. Relisez puis publiez lorsque c’est prêt.`,
    failedHeadline: 'Échec de la traduction Supertext',

    error_chooseLanguages: 'Choisissez une langue source et au moins une langue cible.',
    error_documentNotFound: 'Document introuvable.',
    error_invariantDocumentType: (name) =>
      `Le type de document « ${name} » ne varie pas selon la culture : il n’a donc pas de versions linguistiques distinctes vers lesquelles traduire.`,
    error_noSourceVersion: (culture) => `Le document n’a pas de version ${culture} à partir de laquelle traduire.`,
    error_saveFailed: 'Umbraco n’a pas pu sauvegarder le document traduit.',
    error_noApiKey:
      'Aucune clé API Supertext n’est configurée (Supertext:ApiKey ou SUPERTEXT_API_KEY). ' +
      `Pas encore de compte Supertext ? Créez-en un sur ${SIGNUP}. ` +
      `Générez votre clé API sur ${API_KEY} (supertext.com → Integrations → API, rôle Admin requis).`,
    error_authFailed:
      'Échec de l’authentification. Veuillez vérifier la clé API Supertext. ' +
      `Pas encore de compte Supertext ? Créez-en un sur ${SIGNUP}. ` +
      `Générez votre clé API sur ${API_KEY} (supertext.com → Integrations → API, rôle Admin requis).`,
    error_unreachable: 'Impossible de joindre Supertext.',
    error_languagePair: 'Supertext ne prend pas en charge cette paire de langues. Vérifiez l’association des langues (Supertext:Languages:<culture>:Code, voir Paramètres › Supertext).',
    error_notFound: 'La ressource Supertext demandée est introuvable.',
    error_tooLarge: 'Le contenu est trop volumineux pour être traduit par Supertext en une seule fois.',
    error_rateLimited: 'Trop de requêtes envoyées à Supertext. Veuillez réessayer dans un instant.',
    error_unavailable: 'Le service Supertext est actuellement indisponible.',
    error_http: (status) => `Supertext a répondu avec le code HTTP ${status}.`,
    error_noFileId: 'Supertext n’a pas renvoyé d’identifiant de fichier.',
    error_translationFailed: 'Supertext n’a pas pu traduire le document.',
    error_limitExceeded: 'Votre limite de traduction Supertext est dépassée.',
    error_fileDeleted: 'Le fichier Supertext a été supprimé avant de pouvoir être téléchargé.',
    error_timeout: 'Délai dépassé en attendant la traduction Supertext.',
    error_emptyTranslation: 'La traduction de Supertext était vide ou incomplète.',

    notMapped: 'non associée',
    notMappedHint: (culture) => `${culture} n’est encore associée à aucune langue Supertext. Votre administrateur peut l’associer sous Paramètres › Supertext.`,
    error_unsupportedLanguage: (language) =>
      `La langue ${language} n’est encore associée à aucune langue Supertext. Demandez à votre administrateur d’en choisir une sous Paramètres › Supertext.`,
    error_unsupportedFeature:
      'Supertext ne propose pas de ton formel ou informel pour cette langue. Demandez à votre administrateur de choisir le ton par défaut sous Paramètres › Supertext.',

    dashboardLabel: 'Supertext',
    dashboardHeadline: 'Supertext Translation',
    dashboardLoadFailed: 'Impossible de charger les paramètres Supertext.',
    version: 'Version',
    endpoint: 'Point d’accès API',
    apiKey: 'Clé API',
    apiKeySet: 'Définie',
    apiKeyNotSet: 'Non définie (Supertext:ApiKey ou SUPERTEXT_API_KEY)',
    keyCheck: 'Vérification de la clé API',
    checkedAt: (time) => `vérifiée ${time}`,
    notChecked: 'Pas encore vérifiée.',
    checkKey: 'Vérifier la clé API',
    checkFailed: 'La clé API n’a pas pu être vérifiée.',
    keycheck_ok: 'Clé API vérifiée : la connexion à Supertext fonctionne.',
    keycheck_invalid:
      `Supertext a refusé la clé API. Vérifiez que vous avez copié la clé entière ou générez-en une nouvelle sur <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (rôle Admin requis). ` +
      `Pas encore de compte Supertext ? Créez-en un sur <a href="${SIGNUP}" target="_blank" rel="noopener">supertext.com</a>.`,
    keycheck_missing:
      `Aucune clé API n’est encore saisie. Pas encore de compte Supertext ? Créez-en un sur <a href="${SIGNUP}" target="_blank" rel="noopener">supertext.com</a>. ` +
      `Générez votre clé API sur <a href="${API_KEY}" target="_blank" rel="noopener">supertext.com → Integrations → API</a> (rôle Admin requis).`,
    keycheck_unreachable: 'La clé API n’a pas pu être vérifiée, car Supertext n’est pas joignable. Les paramètres sont enregistrés ; réessayez plus tard.',
    keycheck_error: (status) => `La clé API n’a pas pu être vérifiée : Supertext a répondu par une erreur (${status}). Les paramètres sont enregistrés ; réessayez plus tard.`,
    reports: 'Envoyer les informations de version à Supertext',
    reportsOn: 'Activé',
    reportsOff: 'Désactivé',
    reportsHelp:
      'Envoie à Supertext l’URL du site ainsi que les versions de ce package, d’Umbraco et de l’environnement d’exécution après une mise à jour et lorsque la clé API change. Le support Supertext voit ainsi quelle version vous utilisez. Aucun contenu, aucune clé API et aucune donnée personnelle ne sont transmis.',
    reportsHow: 'Pour le désactiver, définissez Supertext:SendVersionReports sur false dans appsettings.json ou la variable d’environnement SUPERTEXT_VERSION_REPORTS=0.',
    pingUrl: 'Supertext vérifie que le site est joignable à l’adresse',
    languagesHeadline: 'Langues',
    languagesHint:
      'Choisissez la langue Supertext de chaque langue Umbraco ; elle sert aussi lorsque la langue est la langue source. Est présélectionnée la langue Supertext correspondante ou la langue usuelle (fr → français (France)) ; choisissez-en une autre si nécessaire, p. ex. français (Suisse). « — choisir — » : Supertext n’a pas de langue usuelle pour cette langue, choisissez-en une avant de traduire. Les entrées sous Supertext:Languages dans appsettings.json l’emportent sur cette page.',
    umbracoLanguage: 'Langue Umbraco',
    supertextLanguage: 'Langue Supertext',
    tone: 'Formel/informel',
    fromMapping: 'selon l’association',
    defaultTarget: 'par défaut de Supertext',
    exactMatch: 'même code',
    notMappedLong: (culture) => `Choisissez une langue Supertext avant de traduire vers ${culture}.`,
    formal: 'formel',
    informal: 'informel',
    defaultTone: 'par défaut',
    noPoliteness: 'Supertext ne propose pas de choix formel/informel pour cette langue',
    hiddenInDialog: 'masquée dans la boîte de dialogue',
    choose: '— choisir —',
    fromConfig: (culture) => `défini dans appsettings.json (Supertext:Languages:${culture}:Code)`,
    politenessFromConfig: (culture) => `défini dans appsettings.json (Supertext:Languages:${culture}:Politeness)`,
    saveLanguages: 'Enregistrer',
    languagesSaved: 'Langues enregistrées.',
    saveFailed: 'Les langues n’ont pas pu être enregistrées.',
    review: 'Révision des traductions',
    reviewHelp:
      'Révision des traductions : les traductions vers une langue cochée passent par le service de révision de Supertext, afin que les réviseurs puissent les vérifier et les corriger directement sur le site avec l’extension Supertext Localization Review. Leurs corrections sont reprises lors de la prochaine traduction de la page.',
    reviewFromConfig: (culture) => `défini dans appsettings.json (Supertext:Languages:${culture}:Review)`,
    reviewOff: 'La révision des traductions est désactivée sur ce site (Supertext:ReviewUrl est vide).',
    // Exporter pour traduction / importer une traduction (fichiers XLIFF 1.2 ou HTML)
    exportForTranslation: 'Exporter pour traduction',
    exportHint: 'Téléchargez les textes de la langue source pour les langues cibles cochées ci-dessus dans un fichier pour un traducteur ou un outil de traduction, ou chargez-le dans l’application web de Supertext. Importez ensuite le fichier traduit ci-dessous.',
    fileFormat: 'Format de fichier',
    formatXliff: 'XLIFF 1.2 (pour les outils de traduction)',
    formatHtml: 'HTML (une langue par fichier)',
    downloadFile: 'Télécharger le fichier',
    exportFailed: 'Le fichier n’a pas pu être exporté.',
    importTranslation: 'Importer une traduction',
    importHint: 'Les textes importés sont enregistrés comme brouillons, comme les traductions avec Supertext. Vérifiez-les et publiez-les lorsqu’ils sont prêts.',
    translatedFile: 'Fichier traduit (XLIFF ou HTML)',
    includeChanged: 'Importer aussi les textes dont la source a changé depuis l’export',
    importOverwrite: 'Remplacer les langues qui ont déjà du contenu',
    importFailed: 'Le fichier n’a pas pu être importé.',
    importExists: 'Cette langue a déjà du contenu. Cochez « Remplacer les langues qui ont déjà du contenu » et importez à nouveau le fichier.',
    importSaved: 'Enregistré comme brouillon.',
    importNothing: 'Rien n’a été importé.',
    summaryImported: (count) => `Importés : ${count}`,
    summaryChanged: (count) => `Ignorés, source modifiée depuis l’export : ${count}`,
    summaryUntranslated: (count) => `Non traduits dans le fichier : ${count}`,
    summaryInvalid: (count) => `Mise en forme modifiée, non importés : ${count}`,
    summaryMissing: (count) => `Absents du fichier : ${count}`,
    error_nothingToTranslate: 'Rien à traduire.',
    error_htmlOneLanguage: 'Un fichier HTML contient une seule langue : choisissez-en exactement une.',
    error_chooseFile: 'Choisissez un fichier à importer.',
    error_invalidFile: 'Ce fichier n’est pas un fichier de traduction XLIFF ou HTML valide.',
    error_missingMetadata: 'Ce fichier n’a pas été exporté par le paquet Supertext, ou son en-tête a été supprimé.',
    error_fileTooLarge: 'Le fichier est trop volumineux (10 Mo au maximum).',
    error_otherSystem: (plugin) => `Ce fichier appartient à un autre système (${plugin}).`,
    error_fileDocumentNotFound: 'Le document à partir duquel ce fichier a été exporté n’existe plus.',
    error_noLanguage: (language) => `Le site n’a pas de langue ${language}.`,
    error_notAllowed: 'Vous n’êtes pas autorisé à modifier ce document dans cette langue.',
  },
};
