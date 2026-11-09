#!/usr/bin/env node
/**
 * Stand-in for the Supertext AI file translation API, for tests and docs screenshots.
 *
 * Texts listed in sample-<lang>.json (e.g. sample-fr.json, keyed by the English
 * source) come back translated. Anything else comes back unchanged, or prefixed
 * with "[<target_lang>] " when STAND_IN_PREFIX=1, which makes untranslated text
 * easy to spot in tests. STAND_IN_DUMP=<dir> writes each submitted document's
 * untranslated segments to <dir>/<target_lang>.json (to extend the samples).
 * STAND_IN_DELAY=<seconds>: files stay `in_progress` that long (the demo movie shows the waiting state).
 * Listens on :8765 (PORT), accepts any API key. Also answers POST /report with 202 (a stand-in
 * Commando Center: point SUPERTEXT_REPORT_URL at http://127.0.0.1:8765/report so local demos and
 * screenshots never report to the live center).
 */
import http from 'node:http';
import { randomBytes } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const norm = (s) => s.replace(/&apos;|&#39;/g, "'").replace(/&nbsp;/g, ' ').replace(/<br\s*\/?>/g, '<br>').replace(/\s+/g, ' ').trim();
const samples = new Map();
const sampleFor = (lang) => {
  const primary = lang.toLowerCase().split(/[-_]/)[0];
  if (!samples.has(primary)) {
    const file = new URL(`sample-${primary}.json`, import.meta.url);
    const entries = existsSync(file) ? Object.entries(JSON.parse(readFileSync(file))) : [];
    samples.set(primary, new Map(entries.map(([en, tr]) => [norm(en), tr])));
  }
  return samples.get(primary);
};
const files = new Map();
// The target codes Supertext accepts (GET features, October 2026; the SDK's Languages.Targets).
const TARGETS = new Set(['bg', 'cs', 'da', 'de-AT', 'de-CH', 'de-DE', 'el', 'en-GB', 'en-US', 'es', 'fi', 'fr-CH', 'fr-FR',
  'gsw-u-sd-chbe', 'gsw-u-sd-chzh', 'hr', 'hu', 'it-CH', 'it-IT', 'ja', 'ko', 'nb', 'nl', 'pl', 'pt-BR', 'pt-PT', 'rm', 'ru',
  'sk', 'sl', 'sq', 'sr-Cyrl', 'sr-Latn', 'sv', 'tr', 'zh-Hans', 'zh-Hant']);
const prefix = process.env.STAND_IN_PREFIX === '1';
const delayMs = Number(process.env.STAND_IN_DELAY || 0) * 1000;

http
  .createServer(async (req, res) => {
    const path = new URL(req.url, 'http://x').pathname.replace(/^\/v1/, '');
    const send = (status, body, type = 'application/json') => {
      res.writeHead(status, { 'Content-Type': type });
      res.end(type === 'application/json' ? JSON.stringify(body) : body);
    };
    if (req.method === 'POST' && path === '/report') {
      const chunks = [];
      for await (const chunk of req) chunks.push(chunk);
      const report = JSON.parse(Buffer.concat(chunks).toString() || '{}');
      console.log(`POST /report event=${report.event} plugin=${report.plugin?.name}@${report.plugin?.version} key_check=${report.api_key_check?.status ?? '-'}`);
      return send(202, { ok: true });
    }
    if (!/^Supertext-Auth-Key \S+$/.test(req.headers.authorization || '')) return send(401, { error: 'missing key' });
    if (path === '/features') return send(200, {});
    if (req.method === 'POST' && path === '/translate/ai/file') {
      const chunks = [];
      for await (const chunk of req) chunks.push(chunk);
      let form;
      try {
        form = await new Request('http://x', { method: 'POST', headers: req.headers, body: Buffer.concat(chunks) }).formData();
      } catch (e) {
        console.log('POST with unreadable multipart body:', e.message);
        return send(400, { error: 'INVALID_MULTIPART' });
      }
      const target = String(form.get('target_lang') || '');
      // Like the live API: only the exact codes of its language list ("de", "es-ES", "zh-CN" are rejected).
      if (!TARGETS.has(target)) {
        console.log(`POST target_lang=${target} rejected: INVALID_LANGUAGE_PAIR`);
        return send(400, { error_code: 'INVALID_LANGUAGE_PAIR', source_lang: form.get('source_lang'), target_lang: target });
      }
      const id = randomBytes(6).toString('hex');
      files.set(id, { html: await form.get('file').text(), lang: target, ready: Date.now() + delayMs });
      console.log(`POST target_lang=${form.get('target_lang')} source_lang=${form.get('source_lang')} politeness=${form.get('politeness') ?? '-'} -> ${id}`);
      return send(200, { file_id: id });
    }
    const match = path.match(/file\/([a-f0-9]+)(\/status|\/translation)?$/);
    if (!match || !files.has(match[1])) return send(404, {});
    if (req.method === 'DELETE') { files.delete(match[1]); return send(200, {}); }
    if (match[2] === '/status') return send(200, { status: Date.now() < files.get(match[1]).ready ? 'in_progress' : 'done' });
    const { html, lang } = files.get(match[1]);
    const sample = sampleFor(lang);
    if (process.env.STAND_IN_DUMP) {
      const missing = {};
      for (const [, inner] of html.matchAll(/<div data-st-id="\d+">([\s\S]*?)<\/div>\n/g)) if (!sample.has(norm(inner))) missing[inner] = '';
      writeFileSync(`${process.env.STAND_IN_DUMP}/${lang}.json`, JSON.stringify(missing, null, 1));
    }
    const out = html.replace(/(<div data-st-id="\d+">)([\s\S]*?)(<\/div>\n)/g, (all, open, inner, close) => {
      const translated = sample.get(norm(inner));
      if (translated !== undefined) return open + translated + close;
      return prefix ? open + inner.replace(/^((?:\s*<[^>]+>)*)/, `$1[${lang}] `) + close : all;
    });
    send(200, out, 'text/html');
  })
  .listen(Number(process.env.PORT || 8765), () => console.log(`Stand-in API on http://127.0.0.1:${process.env.PORT || 8765}/v1/`));
