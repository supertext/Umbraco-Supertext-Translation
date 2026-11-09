#!/usr/bin/env node
/**
 * End-to-end test against the REAL Supertext API: a fresh demo (nothing translated yet)
 * with a real SUPERTEXT_API_KEY. The editor opens every culture-variant document of the
 * demo in the backoffice and translates it with "Translate with Supertext" into every
 * other language, then the documents are checked through Umbraco's Management API (as the
 * same editor): every document has a version in each target language, nothing is
 * published, the texts changed and are really in the target language (de-CH without "ß"),
 * formatting, links and Umbraco's local links and blocks survived, no placeholder of the
 * Supertext protocol is left, and translating again ("Replace and translate") updates the
 * same documents instead of creating new ones.
 * See "End-to-end tests" in docs/DEVELOPER.md.
 *
 *   BASE_URL=http://127.0.0.1:8081 EDITOR_EMAIL=… EDITOR_PASSWORD=… node Tests/E2E/live.mjs
 *
 *   E2E_RETRANSLATE  how many documents the second run translates again (default 2)
 *   CHROMIUM_PATH    optional Chromium binary
 */
import { chromium } from 'playwright';
import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const B = (process.env.BASE_URL ?? 'http://127.0.0.1:8081').replace(/\/$/, '');
const need = (name) => process.env[name] || (() => { throw new Error(`Set ${name}`); })();
const artifacts = fileURLToPath(new URL('./artifacts/', import.meta.url));
mkdirSync(artifacts, { recursive: true });
const API = `${B}/umbraco/management/api/v1`;
const EXCLUDED = ['code']; // Supertext:ExcludedProperties in the demo's appsettings.json
const TEXT_EDITORS = ['Umbraco.TextBox', 'Umbraco.TextArea', 'Umbraco.RichText', 'Umbraco.TinyMCE', 'Umbraco.BlockList', 'Umbraco.BlockGrid', 'Umbraco.SingleBlock'];

const failures = [];
function check(name, ok, detail = '') {
	console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}${detail ? ` — ${detail}` : ''}`);
	if (!ok) failures.push(`${name}${detail ? ` — ${detail}` : ''}`);
	return ok;
}
const note = (text) => console.log(`note ${text}`);

// Common words, to tell the target languages apart. Lower case, whole words.
const STOPWORDS = {
	en: ['the', 'and', 'is', 'are', 'with', 'for', 'you', 'your', 'our', 'of', 'to', 'we', 'who', 'what'],
	de: ['der', 'die', 'das', 'und', 'ist', 'sind', 'mit', 'für', 'sie', 'ihre', 'ihr', 'unser', 'unsere', 'wir', 'eine', 'ein', 'den', 'zu', 'von', 'was'],
	fr: ['le', 'les', 'et', 'est', 'sont', 'avec', 'pour', 'vous', 'votre', 'vos', 'notre', 'nos', 'nous', 'des', 'une', 'un', 'du', 'de', 'qui', 'que'],
	it: ['il', 'lo', 'gli', 'e', 'è', 'sono', 'con', 'per', 'lei', 'sua', 'suo', 'nostro', 'nostra', 'noi', 'una', 'un', 'del', 'della', 'di', 'che', 'chi'],
};
const words = (text) => text.toLowerCase().match(/[\p{L}]+/gu) ?? [];
const score = (text, lang) => words(text).filter((w) => STOPWORDS[lang].includes(w)).length;
const plain = (s) => String(s ?? '').replace(/<[^>]+>/g, ' ').replace(/&[a-z#0-9]+;/gi, ' ');
const count = (haystack, needle) => String(haystack ?? '').split(needle).length - 1;
const LEFTOVERS = ['data-st-id', 'data-st-ph', 'st-ph-'];
// What the rich text must keep: formatting, links, Umbraco's local links and blocks in the text.
const KEEP = [['links', '<a '], ['local links', 'localLink'], ['bold', '<strong'], ['italics', '<em'], ['list items', '<li'], ['blocks in the text', '<umb-rte-block']];
// The translator skips bare URLs, e-mail addresses, paths and numbers: they stay the same.
const notText = (s) => /^\s*(https?:\/\/\S+|mailto:\S+|[\w.+-]+@[\w-]+\.[\w.]+|\/\S*|[\d\s.,:;%+\-–()]+|[0-9a-f-]{36}|umb:\/\/\S+)\s*$/i.test(s);

/**
 * Translatable text of a value, as [path, text] pairs: plain strings, rich text markup
 * (and the blocks in it), and block list/grid content (settings and layout aren't translated).
 */
function texts(value, path = '') {
	if (value == null) return [];
	if (typeof value === 'string') {
		const s = value.trim();
		if (/^[[{]/.test(s)) {
			try { return texts(JSON.parse(s), path); } catch { /* plain text */ }
		}
		return s && plain(s).trim() && !notText(plain(s)) ? [[path, value]] : [];
	}
	if (Array.isArray(value)) return []; // pickers, tags: not translated
	if (typeof value === 'object') {
		const out = [];
		if ('markup' in value) out.push(...texts(String(value.markup ?? ''), `${path}.markup`));
		const data = value.contentData ?? value.blocks?.contentData;
		if (Array.isArray(data)) {
			data.forEach((item, i) => {
				for (const entry of item.values ?? []) {
					if (EXCLUDED.includes(entry.alias)) continue;
					out.push(...texts(entry.value, `${path}.contentData[${i}].${entry.alias}`));
				}
			});
		}
		return out;
	}
	return [];
}

// --- Browser and Management API as the editor ----------------------------------------------
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });
const context = await browser.newContext({ viewport: { width: 1400, height: 900 }, locale: 'en-US' });
const page = await context.newPage();
page.setDefaultTimeout(30_000);
page.on('dialog', (d) => d.accept().catch(() => {}));
// The backoffice calls the Management API with a bearer token (or a secure cookie): reuse it.
let bearer = '';
page.on('request', (r) => {
	const auth = r.headers().authorization;
	if (auth?.startsWith('Bearer ') && r.url().includes('/umbraco/management/api/')) bearer = auth;
});
async function api(path) {
	const res = await context.request.get(`${API}${path}`, { headers: bearer ? { Authorization: bearer } : {} });
	if (!res.ok()) throw new Error(`GET ${path}: HTTP ${res.status()} ${(await res.text()).slice(0, 300)}`);
	return res.json();
}

await page.goto(`${B}/umbraco`);
await page.getByLabel(/Email/i).first().fill(need('EDITOR_EMAIL'));
await page.getByLabel(/Password/i).first().fill(need('EDITOR_PASSWORD'));
await page.getByRole('button', { name: /Login/i }).first().click();
const loggedIn = await page.waitForURL(/\/umbraco\/section\//, { timeout: 60_000 }).then(() => true, () => false);
if (!check('editor can log in (DEMO_EDITOR_*)', loggedIn)) {
	await page.screenshot({ path: `${artifacts}login.png` });
	await browser.close();
	process.exit(1);
}
await page.getByText('Home', { exact: true }).first().waitFor({ timeout: 60_000 });
await page.waitForTimeout(1500);

// --- Setup: languages and documents ----------------------------------------------------------
const languages = (await api('/language?skip=0&take=100')).items;
const sourceLang = languages.find((l) => l.isDefault);
const targets = languages.filter((l) => !l.isDefault);
console.log(`Languages: ${sourceLang.isoCode} (default) → ${targets.map((t) => t.isoCode).join(', ')}`);
check('demo has target languages', targets.length > 0);

/** Every document in the tree that varies by culture, in tree order. */
async function documents() {
	const out = [];
	async function walk(query) {
		const { items } = await api(`/tree/document/${query}`);
		for (const item of items) {
			if (item.isTrashed) continue;
			if (item.variants?.some((v) => v.culture)) out.push(item.id);
			if (item.hasChildren) await walk(`children?parentId=${item.id}&skip=0&take=200`);
		}
	}
	await walk('root?skip=0&take=200');
	return out;
}
const docIds = await documents();
const totalBefore = docIds.length;
const load = async (id) => api(`/document/${id}`);
const sourceDocs = new Map();
for (const id of docIds) sourceDocs.set(id, await load(id));
const nameOf = (doc, culture) => doc.variants.find((v) => v.culture === culture)?.name;
const exists = (doc, culture) => {
	const v = doc.variants.find((x) => x.culture === culture);
	return !!v && v.state !== 'NotCreated';
};
console.log(`Documents: ${docIds.length} (${[...sourceDocs.values()].map((d) => nameOf(d, sourceLang.isoCode)).join(', ')})`);
check('demo has documents in the source language', [...sourceDocs.values()].filter((d) => exists(d, sourceLang.isoCode)).length > 0);
check('fresh demo (nothing translated yet)', [...sourceDocs.values()].every((d) => targets.every((t) => !exists(d, t.isoCode))));

// --- Translating through the backoffice -------------------------------------------------------
/** Opens the document, translates it with the dialog into every target language, returns the results. */
async function translate(id, label, overwrite) {
	const name = nameOf(sourceDocs.get(id), sourceLang.isoCode);
	const tag = `${label}: "${name}"`;
	await page.goto(`${B}/umbraco/section/content/workspace/document/edit/${id}`);
	// Let the workspace finish loading (list views, e.g. "Blog", render late), else it re-renders under the dialog.
	await page.getByRole('button', { name: 'Translate with Supertext' }).first().waitFor({ timeout: 60_000 });
	await page.waitForLoadState('networkidle').catch(() => {});
	await page.waitForTimeout(1000);
	await page.getByRole('button', { name: 'Translate with Supertext' }).first().click();
	await page.getByText('Translate from').waitFor();
	const dialog = page.locator('supertext-translate-modal');
	await dialog.locator('uui-checkbox').first().waitFor();
	await page.waitForTimeout(1000); // the sidebar slides in
	for (const t of targets) {
		const box = dialog.locator('uui-checkbox').filter({ hasText: t.name }).first();
		if (!(await box.evaluate((el) => el.checked))) {
			if (!overwrite) check(`${tag}: ${t.isoCode} preselected (mapped to a Supertext language, no version yet)`, false);
			await box.click();
		}
	}
	const warnings = await dialog.locator('.warning:not([role=alert])').allTextContents();
	check(`${tag}: dialog without errors`, warnings.length === 0, warnings.join(' | '));
	const responded = page.waitForResponse((r) => r.url().includes('/supertext/translate') && r.request().method() === 'POST', { timeout: 900_000 });
	responded.catch(() => {});
	await dialog.getByRole('button', { name: 'Translate', exact: true }).click();
	if (overwrite) {
		// Targets that have a version already: the dialog asks before replacing them.
		const warned = await dialog.getByRole('alert').filter({ hasText: 'already' }).waitFor().then(() => true, () => false);
		check(`${tag}: overwrite warning shown`, warned);
		await dialog.getByRole('button', { name: 'Replace and translate', exact: true }).click();
	}
	const started = Date.now();
	const response = await responded;
	const results = await response.json().catch(() => null);
	await page.waitForTimeout(1500);
	const failedToast = await page.getByText('Supertext translation failed').count();
	const after = await dialog.locator('.warning:not([role=alert])').allTextContents().catch(() => []);
	if (failedToast || after.length || !Array.isArray(results)) await page.screenshot({ path: `${artifacts}${label}-${id}.png`, fullPage: true });
	console.log(`${tag}: ${Math.round((Date.now() - started) / 1000)} s`);
	if (!check(`${tag}: translate request succeeded`, response.ok() && Array.isArray(results), `HTTP ${response.status()} ${Array.isArray(results) ? '' : JSON.stringify(results)?.slice(0, 300)}`)) return;
	const ok = results.filter((r) => r.status === 'translated');
	check(`${tag}: every language translated`, ok.length === targets.length,
		results.map((r) => `${r.culture} ${r.status}${r.errorCode ? ` ${r.errorCode}: ${r.errorDetail ?? r.error}` : ''}`).join(', '));
	check(`${tag}: no error shown in the backoffice`, failedToast === 0 && after.length === 0, after.join(' | '));
}

/** A step that throws (e.g. a dialog that never opens) fails its check and the test goes on. */
async function attempt(id, label, overwrite) {
	try {
		await translate(id, label, overwrite);
	} catch (e) {
		check(`${label}: "${nameOf(sourceDocs.get(id), sourceLang.isoCode)}" translated through the dialog`, false, e.message.split('\n')[0]);
		await page.screenshot({ path: `${artifacts}${label}-${id}-error.png`, fullPage: true }).catch(() => {});
		await page.keyboard.press('Escape').catch(() => {});
	}
}

for (const id of docIds) await attempt(id, 'first-run', false);

// --- Checks through the Management API --------------------------------------------------------
const first = new Map();
for (const id of docIds) first.set(id, await load(id));

for (const t of targets) {
	const lang = t.isoCode.split(/[-_]/)[0].toLowerCase();
	const tag = t.isoCode;
	let fields = 0;
	let changed = 0;
	let text = '';
	const missing = [];
	const published = [];
	for (const id of docIds) {
		const s = sourceDocs.get(id);
		const d = first.get(id);
		const sourceName = nameOf(s, sourceLang.isoCode);
		const variant = d.variants.find((v) => v.culture === t.isoCode);
		if (!variant || variant.state === 'NotCreated') { missing.push(sourceName); continue; }
		if (variant.state !== 'Draft') published.push(`${sourceName} (${variant.state})`);

		// The document name: a single word can be the same ("Blog", "Contact" in French).
		check(`${tag}: "${sourceName}" has a name`, String(variant.name ?? '').trim() !== '');
		fields++;
		if (variant.name !== sourceName) changed++;
		else note(`${tag}: name "${sourceName}" unchanged`);
		text += ` ${variant.name}`;

		for (const sv of s.values.filter((v) => v.culture === sourceLang.isoCode && TEXT_EDITORS.includes(v.editorAlias) && !EXCLUDED.includes(v.alias))) {
			const tv = d.values.find((v) => v.culture === t.isoCode && v.alias === sv.alias && v.segment === sv.segment);
			const where = `"${sourceName}".${sv.alias}`;
			const raw = JSON.stringify(tv?.value ?? '');
			const left = LEFTOVERS.filter((l) => raw.includes(l));
			check(`${tag}: ${where} has no placeholders left`, left.length === 0, left.join(', '));
			const sourceTexts = texts(sv.value);
			const targetTexts = new Map(texts(tv?.value));
			for (const [path, st] of sourceTexts) {
				fields++;
				const tt = targetTexts.get(path);
				if (!check(`${tag}: ${where}${path} filled`, !!tt && plain(tt).trim() !== '')) continue;
				if (tt !== st) changed++;
				else note(`${tag}: ${where}${path} unchanged: ${plain(st).trim().slice(0, 60)}`);
				text += ` ${plain(tt)}`;
				for (const [what, needle] of KEEP) {
					if (count(st, needle)) check(`${tag}: ${where}${path} keeps its ${what}`, count(tt, needle) === count(st, needle), `${count(tt, needle)}/${count(st, needle)}`);
				}
			}
		}
	}
	check(`${tag}: every document translated`, missing.length === 0, `${docIds.length - missing.length}/${docIds.length}${missing.length ? `, missing: ${missing.join(', ')}` : ''}`);
	check(`${tag}: translations are drafts (not published)`, published.length === 0, published.join(', '));
	check(`${tag}: texts were changed by the translation`, fields > 0 && changed / fields >= 0.8, `${changed}/${fields} fields`);
	if (STOPWORDS[lang]) {
		const scores = Object.fromEntries(Object.keys(STOPWORDS).map((l) => [l, score(text, l)]));
		const best = Object.entries(scores).sort((a, b) => b[1] - a[1])[0][0];
		check(`${tag}: text is in the target language`, best === lang, JSON.stringify(scores));
	}
	if (/^de-ch$/i.test(t.isoCode)) check(`${tag}: Swiss German spelling (no "ß")`, !text.includes('ß'));
}

// --- Translating again updates in place --------------------------------------------------------
const again = docIds.slice(0, Number(process.env.E2E_RETRANSLATE ?? 2));
for (const id of again) await attempt(id, 'second-run', true);
const docIdsAfter = await documents();
check('translating again creates no new documents', docIdsAfter.length === totalBefore, `${totalBefore} → ${docIdsAfter.length}`);
for (const id of again) {
	const before = first.get(id);
	const d = await load(id);
	const name = nameOf(before, sourceLang.isoCode);
	for (const t of targets) {
		const v0 = before.variants.find((v) => v.culture === t.isoCode);
		const v1 = d.variants.find((v) => v.culture === t.isoCode);
		check(`second-run: "${name}" ${t.isoCode} updated in place`, !!v1 && v1.state === 'Draft' && v1.updateDate !== v0?.updateDate, `${v0?.updateDate} → ${v1?.updateDate} (${v1?.state})`);
	}
	const left = LEFTOVERS.filter((l) => JSON.stringify(d.values).includes(l));
	check(`second-run: "${name}" has no placeholders left`, left.length === 0, left.join(', '));
}

await browser.close();

if (failures.length) {
	console.error(`\n${failures.length} check(s) failed:\n- ${failures.join('\n- ')}`);
	process.exit(1);
}
console.log('\nAll end-to-end checks passed.');
