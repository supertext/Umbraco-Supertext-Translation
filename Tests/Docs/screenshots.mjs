#!/usr/bin/env node
/**
 * Regenerates docs/images from a freshly started local Umbraco demo whose package talks to
 * stand-in.mjs (SUPERTEXT_API_ENDPOINT=http://127.0.0.1:8765/v1/), so the German texts are
 * real translations from sample-de.json. See docs/DEVELOPER.md -> Docs screenshots.
 *
 *   BASE_URL (default http://127.0.0.1:8095)
 *   DEMO_EDITOR_EMAIL / DEMO_EDITOR_PASSWORD  editor account (translates and publishes)
 *   DEMO_ADMIN_EMAIL / DEMO_ADMIN_PASSWORD    admin account (settings screens)
 *   CHROMIUM_PATH                             optional Chromium binary
 *   ONLY=dashboard                            only the Settings › Supertext dashboard (any demo state)
 *
 * The demo must not have translations of "Features" yet (fresh database). Start it with
 * SUPERTEXT_REPORT_URL=http://127.0.0.1:8765/report so its version reports go to the stand-in.
 */
import { chromium } from 'playwright';

const B = process.env.BASE_URL || 'http://127.0.0.1:8095';
const EDITOR = [process.env.DEMO_EDITOR_EMAIL || 'editor@example.com', process.env.DEMO_EDITOR_PASSWORD || 'Docs12345!Docs'];
const ADMIN = [process.env.DEMO_ADMIN_EMAIL || 'admin@example.com', process.env.DEMO_ADMIN_PASSWORD || 'Docs12345!Docs'];
const OUT = new URL('../../docs/images', import.meta.url).pathname;

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });
const pad = (r, p = 8) => ({ x: Math.max(0, r.x - p), y: Math.max(0, r.y - p), width: r.width + 2 * p, height: r.height + 2 * p });
const union = (...rs) => {
  const x = Math.min(...rs.map((r) => r.x)), y = Math.min(...rs.map((r) => r.y));
  return { x, y, width: Math.max(...rs.map((r) => r.x + r.width)) - x, height: Math.max(...rs.map((r) => r.y + r.height)) - y };
};
const shot = (page, name, clip) => page.screenshot({ path: `${OUT}/${name}.png`, ...(clip ? { clip } : {}) });

async function login([user, password]) {
  const page = await (await browser.newContext({ viewport: { width: 1400, height: 900 }, locale: 'en-US', timezoneId: 'Europe/Zurich' })).newPage();
  await page.goto(`${B}/umbraco`);
  await page.getByLabel(/Email/i).first().fill(user);
  await page.getByLabel(/Password/i).first().fill(password);
  await page.getByRole('button', { name: /Login/i }).first().click();
  await page.waitForURL(/\/umbraco\/section\//, { timeout: 60000 });
  await page.getByText('Home', { exact: true }).first().waitFor({ timeout: 60000 });
  return page;
}

async function openFeatures(page) {
  const home = page.locator('umb-document-tree-item, umb-tree-item').filter({ hasText: 'Home' }).first();
  if (!(await page.getByRole('link', { name: 'Features' }).first().isVisible().catch(() => false))) {
    await home.locator('#caret-button').first().click();
  }
  await page.getByRole('link', { name: 'Features' }).first().click();
  await page.getByRole('button', { name: 'Translate with Supertext' }).waitFor({ timeout: 30000 });
  await page.waitForTimeout(1500);
}

// The sidebar dialog (size "small") occupies the right 500 px of the 1400 px viewport.
const DIALOG = { x: 900, y: 0, width: 500, height: 900 };

// The workspace header's variant selector (the tree's language switcher has the same text, further left).
async function clickInWorkspaceHeader(page, text) {
  for (const el of await page.getByText(text, { exact: true }).all()) {
    const box = await el.boundingBox();
    if (box && box.x > 600 && box.y < 130) return el.click();
  }
  throw new Error(`"${text}" not found in the workspace header`);
}

/** Switches the open document to its German version (shown by its German name). */
async function switchToGerman(page, germanName) {
  await clickInWorkspaceHeader(page, 'English (United States)');
  await page.getByText(germanName, { exact: true }).first().waitFor();
  await page.waitForTimeout(400);
  await page.getByText(germanName, { exact: true }).first().click();
  await page.waitForTimeout(2000);
}

async function translateAndPublishGerman(page, germanName) {
  await page.getByRole('button', { name: 'Translate with Supertext' }).click();
  await page.getByText('Translate from').waitFor();
  for (const name of ['French (Switzerland)', 'Italian (Switzerland)']) {
    await page.locator('uui-checkbox').filter({ hasText: name }).first().click();
  }
  await page.getByRole('button', { name: 'Translate', exact: true }).click();
  await page.getByText('Translated with Supertext').first().waitFor({ timeout: 120000 });
  await page.waitForTimeout(1500);
  await switchToGerman(page, germanName); // the publish dialog preselects the open language
  await publishGerman(page);
}

async function publishGerman(page, screenshot) {
  await page.getByRole('button', { name: /^Save and publish/ }).first().click();
  await page.getByText('Select all').waitFor({ timeout: 15000 });
  await page.waitForTimeout(500);
  if (screenshot) await shot(page, screenshot, { x: 400, y: 283, width: 600, height: 334 });
  await page.getByRole('button', { name: 'Save and publish', exact: true }).last().click();
  await page.waitForTimeout(3000);
}

/** Settings › Supertext: configuration, API key check and version reports; the languages box (combo boxes) on its own. */
async function dashboard() {
  const admin = await login(ADMIN);
  // The key check's time changes with every run: show a fixed one, so the image only changes with the UI.
  await admin.route('**/umbraco/management/api/v1/supertext/settings**', async (route) => {
    const response = await route.fetch();
    const body = (await response.text()).replace(/"checkedAt"\s*:\s*"[^"]*"/gi, '"checkedAt":"2026-10-01T07:30:00Z"');
    await route.fulfill({ response, body });
  });
  await admin.goto(`${B}/umbraco/section/settings/dashboard/supertext`);
  await admin.getByText('Send version information to Supertext').first().waitFor({ timeout: 30000 });
  await admin.getByRole('button', { name: 'Check API key' }).click();
  await admin.getByText('API key checked').first().waitFor({ timeout: 30000 });
  await admin.waitForTimeout(800);
  const boxes = admin.locator('supertext-settings-dashboard uui-box');
  await boxes.first().screenshot({ path: `${OUT}/supertext-dashboard.png` });
  await boxes.nth(1).scrollIntoViewIfNeeded();
  await admin.waitForTimeout(300);
  await boxes.nth(1).screenshot({ path: `${OUT}/supertext-languages.png` });
  await admin.context().close();
}

if (process.env.ONLY === 'dashboard') {
  await dashboard();
  await browser.close();
  console.log(`Screenshots written to ${OUT}/supertext-dashboard.png and supertext-languages.png`);
  process.exit(0);
}

// --- User guide (editor) ----------------------------------------------------
const page = await login(EDITOR);
// German pages need a published German home page above them to get a URL.
await page.getByRole('link', { name: 'Home' }).first().click();
await page.getByRole('button', { name: 'Translate with Supertext' }).waitFor({ timeout: 30000 });
await page.waitForTimeout(1000);
await translateAndPublishGerman(page, 'Startseite');
await openFeatures(page);
{
  const button = await page.getByRole('button', { name: 'Translate with Supertext' }).boundingBox();
  const save = await page.getByRole('button', { name: /^Save and publish/ }).first().boundingBox();
  await shot(page, 'translate-button', pad(union(button, save), 12));
}

await page.getByRole('button', { name: 'Translate with Supertext' }).click();
await page.getByText('Translate from').waitFor();
await page.waitForTimeout(1000);
await shot(page, 'translate-dialog', DIALOG);

// The stand-in has real German only (sample-de.json), so translate into German here.
for (const name of ['French (Switzerland)', 'Italian (Switzerland)']) {
  await page.locator('uui-checkbox').filter({ hasText: name }).first().click();
}
await page.getByRole('button', { name: 'Translate', exact: true }).click();
await page.getByText('Translated with Supertext').first().waitFor({ timeout: 120000 });
await page.waitForTimeout(800);
{
  // The toast lives in shadow DOM; crop around its headline.
  const t = await page.getByText('Translated with Supertext').first().boundingBox();
  await shot(page, 'translated-notification', { x: Math.max(0, t.x - 28), y: Math.max(0, t.y - 24), width: 440, height: 116 });
}

// German version in the backoffice
await clickInWorkspaceHeader(page, 'English (United States)');
await page.getByText('Funktionen', { exact: true }).first().waitFor();
await page.waitForTimeout(600);
{
  const menuTop = await page.getByText('Features', { exact: true }).nth(1).boundingBox();
  const last = await page.getByText('Italian (Switzerland)', { exact: true }).last().boundingBox();
  await shot(page, 'variant-menu', pad({ x: 356, y: 70, width: 835, height: last.y + last.height + 14 - 70 }, 4));
}
await page.getByText('Funktionen', { exact: true }).first().click();
await page.waitForTimeout(2500);
await shot(page, 'translated-german');

// Second run into an existing language: the overwrite warning
await page.getByRole('button', { name: 'Translate with Supertext' }).click();
await page.getByText('Translate from').waitFor();
await page.waitForTimeout(800);
for (const name of ['French (Switzerland)', 'Italian (Switzerland)']) {
  const box = page.locator('uui-checkbox').filter({ hasText: name }).first();
  if (await box.evaluate((el) => el.checked)) await box.click();
}
const german = page.locator('uui-checkbox').filter({ hasText: 'German (Switzerland)' }).first();
if (!(await german.evaluate((el) => el.checked))) await german.click();
await page.getByRole('button', { name: 'Translate', exact: true }).click();
await page.getByRole('alert').filter({ hasText: 'already' }).waitFor();
await page.waitForTimeout(500);
await shot(page, 'overwrite-warning', DIALOG);
await page.getByRole('button', { name: 'Cancel' }).last().click();
await page.waitForTimeout(800);

// Publish the German version
await publishGerman(page, 'publish-german');

// Public website in German
const site = await page.context().newPage();
await site.goto(`${B}/de/`);
const featuresLink = site.locator('a', { hasText: 'Funktionen' }).first();
const href = (await featuresLink.getAttribute('href').catch(() => null)) ?? '/de/funktionen/';
await site.goto(new URL(href, B).toString());
await site.waitForTimeout(1500);
await site.screenshot({ path: `${OUT}/website-german.jpg`, type: 'jpeg', quality: 80, clip: { x: 0, y: 0, width: 1400, height: 820 } });

// --- Installation guide (admin) ---------------------------------------------
const admin = await login(ADMIN);
await admin.goto(`${B}/umbraco/section/settings/workspace/language-root`);
await admin.getByText('German (Switzerland)').first().waitFor({ timeout: 30000 });
await admin.waitForTimeout(1000);
await shot(admin, 'languages', { x: 300, y: 60, width: 1100, height: 420 });

// Document type setting "Vary by culture" (Settings > Document Types > Content > Settings)
await admin.getByRole('link', { name: 'Document Types' }).first().click().catch(() => {});
await admin.goto(`${B}/umbraco/section/settings/workspace/document-type/edit/${process.env.CONTENT_TYPE_KEY || 'b871f83c-2395-4894-be0f-5422c1a71e48' /* Clean's "Content" document type */}/view/settings`);
await admin.getByText(/Vary by culture/i).first().waitFor({ timeout: 30000 });
await admin.waitForTimeout(1200);
{
  const label = await admin.getByText(/Vary by culture/i).first().boundingBox();
  await shot(admin, 'vary-by-culture', { x: 300, y: Math.max(60, label.y - 60), width: 1100, height: 180 });
}

// Culture and Hostnames on the home page
await admin.goto(`${B}/umbraco/section/content`);
await admin.getByText('Home', { exact: true }).first().waitFor();
const homeItem = admin.locator('umb-document-tree-item, umb-tree-item').filter({ hasText: 'Home' }).first();
await homeItem.hover();
await homeItem.locator('#action-modal, [label="Open actions menu"], uui-action-bar uui-button, #actions-menu').first().click().catch(async () => homeItem.click({ button: 'right' }));
await admin.getByText(/Culture and Hostnames/i).first().click();
await admin.getByText('/de').first().waitFor({ timeout: 15000 }).catch(() => {});
await admin.waitForTimeout(1200);
await shot(admin, 'culture-hostnames', { x: 520, y: 0, width: 880, height: 560 }); // medium-size sidebar

await dashboard();

await browser.close();
console.log(`Screenshots written to ${OUT}`);
