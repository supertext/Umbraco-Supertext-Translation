// The "Supertext" dashboard in the Settings section: the configuration as the package sees it, the latest
// API key check (with a "Check API key" button), the version reports setting and the language mapping:
// a Supertext language, tone and Translation review per Umbraco language, saved with PUT settings/languages.
// Supertext:Languages in appsettings.json overrides the mapping; other settings live in appsettings.json /
// the environment (docs/INSTALLATION.md).
import { html, css, nothing, unsafeHTML } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';

const API = '/umbraco/management/api/v1/supertext/settings';
const AUTH = [{ scheme: 'bearer', type: 'http' }];

export class SupertextSettingsDashboardElement extends UmbLitElement {
  static properties = {
    _settings: { state: true },
    _check: { state: true },
    _checking: { state: true },
    _error: { state: true },
    _rows: { state: true },
    _saving: { state: true },
    _saveMessage: { state: true },
  };

  constructor() {
    super();
    this._checking = false;
    this._saving = false;
    this._rows = {};
  }

  async connectedCallback() {
    super.connectedCallback();
    const { data, error } = await umbHttpClient.get({ url: API, security: AUTH });
    if (error || !data) {
      this._error = this.localize.term('supertext_dashboardLoadFailed');
      return;
    }
    this._settings = data;
    this._check = data.keyCheck;
    this._initRows(data.languages);
  }

  // Form state per Umbraco language: the chosen code (saved, else the suggestion, else none) and tone.
  _initRows(languages) {
    const rows = {};
    for (const l of languages) {
      rows[l.isoCode] = {
        code: l.configuredCode ? l.savedCode ?? '' : l.savedCode ?? l.suggestedCode ?? '',
        politeness: l.politeness,
        review: l.review,
      };
    }
    this._rows = rows;
  }

  _setRow(isoCode, change) {
    this._rows = { ...this._rows, [isoCode]: { ...this._rows[isoCode], ...change } };
    this._saveMessage = undefined;
  }

  _target(code) {
    return this._settings.targets.find((t) => t.code === code);
  }

  // The language's name in the backoffice language where the browser knows it ("Deutsch (Schweiz)"),
  // else Supertext's English name; Swiss German dialects always use Supertext's name (Zurich, Bern).
  _name(target) {
    if (!target.code.includes('-u-')) {
      try {
        const lang = typeof this.localize.lang === 'function' ? this.localize.lang() : document.documentElement.lang;
        const name = new Intl.DisplayNames([lang || 'en'], { type: 'language' }).of(target.code);
        if (name && name !== target.code) return name;
      } catch {
        // fall back to Supertext's name
      }
    }
    return target.name;
  }

  async _saveLanguages() {
    this._saving = true;
    this._saveMessage = undefined;
    const languages = this._settings.languages.map((l) => ({
      isoCode: l.isoCode,
      code: this._rows[l.isoCode].code || null,
      politeness: this._rows[l.isoCode].politeness,
      review: this._rows[l.isoCode].review,
    }));
    const { data, error } = await umbHttpClient.put({
      url: `${API}/languages`,
      security: AUTH,
      body: { languages },
      headers: { 'Content-Type': 'application/json' },
    });
    this._saving = false;
    if (error || !data) {
      this._saveMessage = { ok: false, text: this.localize.term('supertext_saveFailed') };
      return;
    }
    this._settings = { ...this._settings, languages: data };
    this._initRows(data);
    this._saveMessage = { ok: true, text: this.localize.term('supertext_languagesSaved') };
  }

  async _checkKey() {
    this._checking = true;
    const { data, error } = await umbHttpClient.post({ url: `${API}/check-key`, security: AUTH });
    this._checking = false;
    if (error || !data) {
      this._error = this.localize.term('supertext_checkFailed');
      return;
    }
    this._error = undefined;
    this._check = data;
  }

  _when(iso) {
    const date = new Date(iso);
    return Number.isNaN(date.getTime()) ? iso : this.localize.date(date, { dateStyle: 'medium', timeStyle: 'short' });
  }

  _tone(politeness) {
    if (politeness === 'more') return this.localize.term('supertext_formal');
    if (politeness === 'less') return this.localize.term('supertext_informal');
    return this.localize.term('supertext_defaultTone');
  }

  render() {
    if (this._error && !this._settings) return html`<uui-box><div class="fail">${this._error}</div></uui-box>`;
    if (!this._settings) return html`<uui-loader></uui-loader>`;
    const s = this._settings;
    return html`
      <uui-box headline=${this.localize.term('supertext_dashboardHeadline')}>
        <dl>
          <dt>${this.localize.term('supertext_version')}</dt>
          <dd>
            ${s.releaseUrl ? html`<a href=${s.releaseUrl} target="_blank" rel="noopener">${s.version}</a>` : s.version}
            <span class="hint">· Umbraco ${s.umbracoVersion ?? '?'} · Supertext SDK ${s.sdkVersion}</span>
          </dd>
          <dt>${this.localize.term('supertext_endpoint')}</dt>
          <dd><code>${s.endpoint}</code></dd>
          <dt>${this.localize.term('supertext_apiKey')}</dt>
          <dd>${this.localize.term(s.hasApiKey ? 'supertext_apiKeySet' : 'supertext_apiKeyNotSet')}</dd>
          <dt>${this.localize.term('supertext_keyCheck')}</dt>
          <dd>
            ${this._check
              ? html`<div class=${this._check.status === 'ok' ? 'ok' : 'fail'}>
                    ${unsafeHTML(this.localize.term(`supertext_keycheck_${this._check.status}`, this._check.httpStatus ?? ''))}
                  </div>
                  <div class="hint">${this.localize.term('supertext_checkedAt', this._when(this._check.checkedAt))}</div>`
              : html`<div>${this.localize.term('supertext_notChecked')}</div>`}
            ${this._error ? html`<div class="fail">${this._error}</div>` : nothing}
            <uui-button
              look="secondary"
              label=${this.localize.term('supertext_checkKey')}
              .state=${this._checking ? 'waiting' : undefined}
              ?disabled=${this._checking}
              @click=${this._checkKey}></uui-button>
          </dd>
          <dt>${this.localize.term('supertext_reports')}</dt>
          <dd>
            <div>
              ${this.localize.term(s.reports.enabled ? 'supertext_reportsOn' : 'supertext_reportsOff')}
              ${s.reports.fromEnvironment ? html`<span class="hint">(SUPERTEXT_VERSION_REPORTS)</span>` : nothing}
            </div>
            <div class="hint">${this.localize.term('supertext_reportsHelp')}</div>
            <div class="hint">${this.localize.term('supertext_reportsHow')}</div>
            ${s.reports.pingUrl
              ? html`<div class="hint">${this.localize.term('supertext_pingUrl')} <code>${s.reports.pingUrl}</code></div>`
              : nothing}
          </dd>
        </dl>
      </uui-box>
      <uui-box headline=${this.localize.term('supertext_languagesHeadline')}>
        <p class="hint">${this.localize.term('supertext_languagesHint')}</p>
        <uui-table>
          <uui-table-head>
            <uui-table-head-cell>${this.localize.term('supertext_umbracoLanguage')}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term('supertext_supertextLanguage')}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term('supertext_tone')}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term('supertext_review')}</uui-table-head-cell>
          </uui-table-head>
          ${s.languages.map((l) => this._languageRow(l))}
        </uui-table>
        <p class="hint review-help">
          ${this.localize.term('supertext_reviewHelp')}
          ${s.reviewUrl ? nothing : html`<span class="fail">${this.localize.term('supertext_reviewOff')}</span>`}
        </p>
        <div class="actions">
          <uui-button
            look="primary"
            color="positive"
            label=${this.localize.term('supertext_saveLanguages')}
            .state=${this._saving ? 'waiting' : undefined}
            ?disabled=${this._saving}
            @click=${this._saveLanguages}></uui-button>
          ${this._saveMessage ? html`<span class=${this._saveMessage.ok ? 'ok' : 'fail'}>${this._saveMessage.text}</span>` : nothing}
        </div>
      </uui-box>
    `;
  }

  _languageRow(l) {
    const row = this._rows[l.isoCode] ?? { code: '', politeness: 'default' };
    const code = l.configuredCode ? l.supertextCode : row.code || null;
    const effective = code ?? (l.configuredCode ? null : l.suggestedCode);
    const target = effective ? this._target(effective) : undefined;
    const options = [
      { name: this.localize.term('supertext_choose'), value: '', selected: !row.code },
      ...this._settings.targets.map((t) => ({ name: `${this._name(t)} (${t.code})`, value: t.code, selected: t.code === row.code })),
    ];
    const tones = ['default', 'more', 'less'].map((p) => ({ name: this._tone(p), value: p, selected: p === row.politeness }));
    return html`
      <uui-table-row>
        <uui-table-cell>
          ${l.name} <code>${l.isoCode}</code>
          ${l.enabled ? nothing : html`<span class="hint">· ${this.localize.term('supertext_hiddenInDialog')}</span>`}
        </uui-table-cell>
        <uui-table-cell>
          ${l.configuredCode
            ? html`<div><code>${l.supertextCode ?? l.configuredCode}</code></div>
                <div class="hint">${this.localize.term('supertext_fromConfig', l.isoCode)}</div>`
            : html`<uui-select
                  label=${this.localize.term('supertext_supertextLanguage')}
                  .options=${options}
                  @change=${(e) => this._setRow(l.isoCode, { code: e.target.value })}></uui-select>
                ${!effective
                  ? html`<div class="fail">${this.localize.term('supertext_notMappedLong', l.isoCode)}</div>`
                  : !row.code || (!l.savedCode && row.code === l.suggestedCode)
                    ? html`<div class="hint">${this.localize.term(l.fromDefault ? 'supertext_defaultTarget' : 'supertext_exactMatch')}</div>`
                    : nothing}`}
        </uui-table-cell>
        <uui-table-cell>
          ${l.configuredPoliteness
            ? html`<div>${this._tone(l.politeness)}</div>
                <div class="hint">${this.localize.term('supertext_politenessFromConfig', l.isoCode)}</div>
                ${l.politeness !== 'default' && l.politenessSupported === false
                  ? html`<div class="fail">${this.localize.term('supertext_noPoliteness')}</div>`
                  : nothing}`
            : target?.politeness
              ? html`<uui-select
                  label=${this.localize.term('supertext_tone')}
                  .options=${tones}
                  @change=${(e) => this._setRow(l.isoCode, { politeness: e.target.value })}></uui-select>`
              : html`<span class="hint">${target ? this.localize.term('supertext_noPoliteness') : '—'}</span>`}
        </uui-table-cell>
        <uui-table-cell>
          <uui-checkbox
            label=${this.localize.term('supertext_review')}
            ?checked=${l.configuredReview ?? row.review}
            ?disabled=${l.configuredReview !== null && l.configuredReview !== undefined}
            @change=${(e) => this._setRow(l.isoCode, { review: e.target.checked })}
            ><span class="sr-only">${this.localize.term('supertext_review')}: ${l.name}</span></uui-checkbox
          >
          ${l.configuredReview !== null && l.configuredReview !== undefined
            ? html`<div class="hint">${this.localize.term('supertext_reviewFromConfig', l.isoCode)}</div>`
            : nothing}
        </uui-table-cell>
      </uui-table-row>
    `;
  }

  static styles = css`
    :host { display: flex; flex-direction: column; gap: var(--uui-size-layout-1); padding: var(--uui-size-layout-1); }
    dl { display: grid; grid-template-columns: max-content 1fr; gap: var(--uui-size-space-4) var(--uui-size-space-6); margin: 0; }
    dt { font-weight: bold; }
    dd { margin: 0; display: flex; flex-direction: column; align-items: flex-start; gap: var(--uui-size-space-2); }
    .hint { color: var(--uui-color-text-alt); }
    .ok { color: var(--uui-color-positive-standalone); }
    .fail { color: var(--uui-color-danger-standalone); }
    .fail a, .ok a { color: inherit; text-decoration: underline; }
    p { margin-top: 0; }
    uui-select { min-width: 18rem; }
    code { white-space: nowrap; }
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
    .review-help { margin: var(--uui-size-space-4) 0 0; }
    .actions { display: flex; align-items: center; gap: var(--uui-size-space-4); margin-top: var(--uui-size-space-5); }
  `;
}

customElements.define('supertext-settings-dashboard', SupertextSettingsDashboardElement);
export default SupertextSettingsDashboardElement;
