// The "Translate with Supertext" dialog (sidebar modal).
import { html, css, nothing, unsafeHTML } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';

const API = '/umbraco/management/api/v1/supertext';
const AUTH = [{ scheme: 'bearer', type: 'http' }];
const MAX_UPLOAD_BYTES = 10 * 1024 * 1024; // the server checks again

export class SupertextTranslateModalElement extends UmbModalBaseElement {
  static properties = {
    _languages: { state: true },
    _source: { state: true },
    _targets: { state: true },
    _status: { state: true },
    _busy: { state: true },
    _confirmOverwrite: { state: true },
    _results: { state: true },
    _error: { state: true },
    _format: { state: true },
    _exporting: { state: true },
    _file: { state: true },
    _includeChanged: { state: true },
    _importOverwrite: { state: true },
    _importing: { state: true },
    _importResults: { state: true },
    _importError: { state: true },
  };

  constructor() {
    super();
    this._languages = [];
    this._targets = new Set();
    this._busy = false;
    this._confirmOverwrite = false;
    this._results = undefined;
    this._error = undefined;
    this._format = 'xliff';
    this._exporting = false;
    this._file = undefined;
    this._includeChanged = false;
    this._importOverwrite = false;
    this._importing = false;
    this._importResults = undefined;
    this._importError = undefined;
  }

  async connectedCallback() {
    super.connectedCallback();
    const unique = this.data?.unique;
    const [status, languages] = await Promise.all([
      umbHttpClient.get({ url: `${API}/status`, security: AUTH }),
      umbHttpClient.get({ url: `${API}/languages`, query: { documentId: unique }, security: AUTH }),
    ]);
    this._status = status.data;
    if (languages.error || !Array.isArray(languages.data)) {
      this._error = this._errorText(languages.error, 'supertext_loadLanguagesFailed');
      return;
    }
    this._languages = languages.data;
    const source = this._languages.find((l) => l.isDefault && l.exists) ?? this._languages.find((l) => l.exists);
    this._source = source?.isoCode;
    // Preselect every language that has no version yet and has a Supertext language.
    this._targets = new Set(
      this._languages.filter((l) => !l.exists && l.supertextCode && l.isoCode !== this._source).map((l) => l.isoCode),
    );
  }

  _toggle(isoCode, checked) {
    const targets = new Set(this._targets);
    checked ? targets.add(isoCode) : targets.delete(isoCode);
    this._targets = targets;
    this._confirmOverwrite = false;
  }

  get _existingTargets() {
    return this._languages.filter((l) => l.exists && this._targets.has(l.isoCode));
  }

  async _translate() {
    if (this._existingTargets.length > 0 && !this._confirmOverwrite) {
      this._confirmOverwrite = true; // ask first: translating replaces their content
      return;
    }
    this._busy = true;
    this._error = undefined;
    const { data, error } = await umbHttpClient.post({
      url: `${API}/translate`,
      body: {
        documentId: this.data?.unique,
        sourceCulture: this._source,
        targetCultures: [...this._targets],
        overwrite: this._confirmOverwrite,
      },
      headers: { 'Content-Type': 'application/json' },
      security: AUTH,
    });
    this._busy = false;
    if (error || !Array.isArray(data)) {
      this._error = this._errorText(error, 'supertext_startFailed');
      return;
    }
    this._results = data;
    const translated = data.filter((r) => r.status === 'translated');
    const failed = data.filter((r) => r.status === 'failed');
    const notifications = await this.getContext(UMB_NOTIFICATION_CONTEXT);
    if (translated.length) {
      notifications.peek('positive', {
        data: {
          headline: this.localize.term('supertext_translatedHeadline'),
          message: this.localize.term('supertext_translatedMessage', translated.map((r) => this._name(r.culture)).join(', ')),
        },
      });
    }
    if (failed.length) {
      notifications.peek('danger', {
        data: {
          headline: this.localize.term('supertext_failedHeadline'),
          message: failed.map((r) => `${this._name(r.culture)}: ${this._resultError(r)}`).join(' '),
        },
      });
    }
    this.value = { translated: translated.length };
    if (!failed.length) this._submitModal();
  }

  /** Export for translation: downloads the file the server builds for the chosen source and target languages. */
  async _export() {
    this._error = undefined;
    if (this._format === 'html' && this._targets.size !== 1) {
      this._error = this.localize.term('supertext_error_htmlOneLanguage');
      return;
    }
    this._exporting = true;
    const { data, error, response } = await umbHttpClient.post({
      url: `${API}/export`,
      body: { documentId: this.data?.unique, sourceCulture: this._source, targetCultures: [...this._targets], format: this._format },
      headers: { 'Content-Type': 'application/json' },
      security: AUTH,
      parseAs: 'blob',
    });
    this._exporting = false;
    if (error || !(data instanceof Blob)) {
      this._error = this._errorText(await this._problem(error), 'supertext_exportFailed');
      return;
    }
    const disposition = response?.headers.get('Content-Disposition') ?? '';
    const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1] ?? `umbraco-${this.data?.unique}.${this._format === 'html' ? 'html' : 'xlf'}`;
    const url = URL.createObjectURL(data);
    const link = document.createElement('a');
    link.href = url;
    link.download = decodeURIComponent(name);
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  /** An error response read as a blob holds the problem details as JSON. */
  async _problem(error) {
    if (error instanceof Blob) {
      try {
        return JSON.parse(await error.text());
      } catch {
        return undefined;
      }
    }
    return error;
  }

  /** Import translation: sends the chosen file's text (size checked first) and shows the summary per document and language. */
  async _import() {
    this._importError = undefined;
    this._importResults = undefined;
    if (!this._file) {
      this._importError = this.localize.term('supertext_error_chooseFile');
      return;
    }
    if (this._file.size > MAX_UPLOAD_BYTES) {
      this._importError = this.localize.term('supertext_error_fileTooLarge');
      return;
    }
    this._importing = true;
    const content = await this._file.text();
    const { data, error } = await umbHttpClient.post({
      url: `${API}/import`,
      body: { content, includeChanged: this._includeChanged, overwrite: this._importOverwrite },
      headers: { 'Content-Type': 'application/json' },
      security: AUTH,
    });
    this._importing = false;
    if (error || !Array.isArray(data)) {
      this._importError = this._errorText(error, 'supertext_importFailed');
      return;
    }
    this._importResults = data;
    if (data.some((r) => r.status === 'translated')) this.value = { translated: data.filter((r) => r.status === 'translated').length };
  }

  /**
   * Text for a server error in the editor's language: the server sends a code (key
   * `supertext_error_<code>`), its arguments and the untranslated detail from Supertext,
   * plus the English text as `title`, used when the code is unknown.
   */
  _localizeError(code, args, detail, english, fallbackKey) {
    const text = code
      ? this.localize.termOrDefault(`supertext_error_${code}`, null, ...(args ?? []))
      : null;
    if (text === null) return english || this.localize.term(fallbackKey);
    return detail ? `${text} (${detail})` : text;
  }

  _errorText(error, fallbackKey) {
    return this._localizeError(error?.supertextCode, error?.supertextArgs, error?.supertextDetail, error?.title, fallbackKey);
  }

  _resultError(r) {
    return this._localizeError(r.errorCode, r.errorArgs, r.errorDetail, r.error, 'supertext_startFailed');
  }

  _name(isoCode) {
    return this._languages.find((l) => l.isoCode === isoCode)?.name ?? isoCode;
  }

  render() {
    return html`
      <umb-body-layout headline=${this.localize.term('supertext_translateWithSupertext')}>
        <div id="main">
          ${this._status && !this._status.hasApiKey
            ? html`<div class="warning">${unsafeHTML(this.localize.term('supertext_noApiKeyHtml'))}</div>`
            : nothing}
          ${this._error ? html`<div class="warning">${this._error}</div>` : nothing}
          ${this._languages.length ? this._renderForm() : html`<uui-loader></uui-loader>`}
        </div>
        <div slot="actions">
          <uui-button label=${this.localize.term('general_cancel')} @click=${this._rejectModal}></uui-button>
          <uui-button
            look="primary"
            color=${this._confirmOverwrite ? 'danger' : 'positive'}
            label=${this.localize.term(this._confirmOverwrite ? 'supertext_replaceAndTranslate' : 'supertext_translate')}
            ?disabled=${!this._source || this._targets.size === 0 || this._busy || this._status?.hasApiKey === false}
            .state=${this._busy ? 'waiting' : undefined}
            @click=${this._translate}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  _renderForm() {
    const sources = this._languages.filter((l) => l.exists);
    return html`
      <uui-box headline=${this.localize.term('supertext_translateFrom')}>
        <select
          id="source"
          aria-label=${this.localize.term('supertext_sourceLanguage')}
          .value=${this._source ?? ''}
          @change=${(e) => {
            this._source = e.target.value;
            this._toggle(this._source, false);
          }}>
          ${sources.map((l) => html`<option value=${l.isoCode} ?selected=${l.isoCode === this._source}>${l.name}</option>`)}
        </select>
      </uui-box>
      <uui-box headline=${this.localize.term('supertext_into')}>
        ${this._languages
          .filter((l) => l.isoCode !== this._source)
          .map(
            (l) => html`
              <div class="language">
                <uui-checkbox
                  label=${l.name}
                  ?checked=${this._targets.has(l.isoCode)}
                  @change=${(e) => this._toggle(l.isoCode, e.target.checked)}></uui-checkbox>
                ${l.exists ? html`<uui-tag look="secondary">${this.localize.term('supertext_hasContent')}</uui-tag>` : nothing}
                ${l.supertextCode === null
                  ? html`<uui-tag color="warning" title=${this.localize.term('supertext_notMappedHint', l.isoCode)}>${this.localize.term('supertext_notMapped')}</uui-tag>`
                  : nothing}
                ${this._renderResult(l.isoCode)}
              </div>
            `,
          )}
      </uui-box>
      ${this._confirmOverwrite
        ? html`<div class="warning" role="alert">
            <strong>${this.localize.term(
              'supertext_overwriteWarning',
              this._existingTargets.map((l) => l.name).join(', '),
              this._existingTargets.length,
            )}</strong>
            ${this.localize.term('supertext_overwriteExplanation', this._name(this._source))}
          </div>`
        : nothing}
      <p class="hint">${this.localize.term('supertext_hint')}</p>
      ${this._renderExport()} ${this._renderImport()}
    `;
  }

  _renderExport() {
    return html`
      <uui-box headline=${this.localize.term('supertext_exportForTranslation')} class="export">
        <p class="hint">${this.localize.term('supertext_exportHint')}</p>
        <fieldset>
          <legend>${this.localize.term('supertext_fileFormat')}</legend>
          <uui-radio-group .value=${this._format} @change=${(e) => (this._format = e.target.value)}>
            <uui-radio value="xliff" label=${this.localize.term('supertext_formatXliff')}></uui-radio>
            <uui-radio value="html" label=${this.localize.term('supertext_formatHtml')}></uui-radio>
          </uui-radio-group>
        </fieldset>
        <uui-button
          look="secondary"
          label=${this.localize.term('supertext_downloadFile')}
          ?disabled=${!this._source || this._targets.size === 0 || this._exporting}
          .state=${this._exporting ? 'waiting' : undefined}
          @click=${this._export}></uui-button>
      </uui-box>
    `;
  }

  _renderImport() {
    return html`
      <uui-box headline=${this.localize.term('supertext_importTranslation')} class="import">
        <p class="hint">${this.localize.term('supertext_importHint')}</p>
        <label class="file">
          ${this.localize.term('supertext_translatedFile')}
          <input
            type="file"
            id="file"
            accept=".xlf,.xliff,.html,.htm"
            @change=${(e) => {
              this._file = e.target.files?.[0];
              this._importResults = undefined;
              this._importError = undefined;
            }} />
        </label>
        <uui-checkbox
          label=${this.localize.term('supertext_includeChanged')}
          ?checked=${this._includeChanged}
          @change=${(e) => (this._includeChanged = e.target.checked)}></uui-checkbox>
        <uui-checkbox
          label=${this.localize.term('supertext_importOverwrite')}
          ?checked=${this._importOverwrite}
          @change=${(e) => (this._importOverwrite = e.target.checked)}></uui-checkbox>
        <div>
          <uui-button
            look="secondary"
            label=${this.localize.term('supertext_importTranslation')}
            ?disabled=${!this._file || this._importing}
            .state=${this._importing ? 'waiting' : undefined}
            @click=${this._import}></uui-button>
        </div>
        ${this._importError ? html`<div class="warning">${this._importError}</div>` : nothing}
        ${this._importResults ? html`<div class="summary">${this._importResults.map((r) => this._renderImportResult(r))}</div>` : nothing}
      </uui-box>
    `;
  }

  _renderImportResult(r) {
    const counts = [
      ['supertext_summaryImported', r.imported, true],
      ['supertext_summaryChanged', r.sourceChanged],
      ['supertext_summaryUntranslated', r.untranslated],
      ['supertext_summaryInvalid', r.invalidMarkup],
      ['supertext_summaryMissing', r.missing],
    ].filter(([, n, always]) => always || n > 0);
    return html`
      <div class="job">
        <strong>${r.documentName ?? r.documentId ?? ''}</strong> · ${this._name(r.culture)}
        ${r.status === 'failed'
          ? html`<div class="error">${this._localizeError(r.errorCode, r.errorArgs, null, r.error, 'supertext_importFailed')}</div>`
          : r.status === 'exists'
            ? html`<div class="error">${this.localize.term('supertext_importExists')}</div>`
            : html`<ul>${counts.map(([key, n]) => html`<li>${this.localize.term(key, n)}</li>`)}</ul>
                <div>${this.localize.term(r.status === 'translated' ? 'supertext_importSaved' : 'supertext_importNothing')}</div>`}
      </div>
    `;
  }

  _renderResult(isoCode) {
    const r = this._results?.find((x) => x.culture === isoCode);
    if (!r) return nothing;
    if (r.status === 'translated') return html`<uui-tag color="positive">${this.localize.term('supertext_translated')}</uui-tag>`;
    if (r.status === 'failed') return html`<uui-tag color="danger" title=${this._resultError(r)}>${this.localize.term('supertext_failed')}</uui-tag>`;
    return nothing;
  }

  static styles = css`
    #main { display: flex; flex-direction: column; gap: var(--uui-size-space-5); }
    .language { display: flex; align-items: center; gap: var(--uui-size-space-3); padding: var(--uui-size-space-2) 0; }
    select { width: 100%; padding: var(--uui-size-space-3); font: inherit; border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); background: var(--uui-color-surface); color: var(--uui-color-text); }
    .warning { padding: var(--uui-size-space-4); border-radius: var(--uui-border-radius); background: var(--uui-color-warning); color: var(--uui-color-warning-contrast); }
    .warning a { color: inherit; text-decoration: underline; }
    .hint { color: var(--uui-color-text-alt); margin: 0; }
    .export, .import { display: block; }
    .export .hint, .import .hint { margin-bottom: var(--uui-size-space-4); }
    fieldset { border: 0; padding: 0; margin: 0 0 var(--uui-size-space-4); }
    legend, label.file { display: block; font-weight: 600; margin-bottom: var(--uui-size-space-2); }
    label.file input { display: block; font-weight: normal; margin-top: var(--uui-size-space-2); }
    .import uui-checkbox { display: block; margin: var(--uui-size-space-2) 0; }
    .import .warning, .summary { margin-top: var(--uui-size-space-4); }
    .job { padding: var(--uui-size-space-3) 0; border-top: 1px solid var(--uui-color-border); }
    .job ul { margin: var(--uui-size-space-2) 0; padding-left: var(--uui-size-space-5); }
    .job .error { color: var(--uui-color-danger); }
  `;
}

customElements.define('supertext-translate-modal', SupertextTranslateModalElement);
export default SupertextTranslateModalElement;
