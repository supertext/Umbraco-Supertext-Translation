#!/usr/bin/env node
/**
 * Records the README's demo movie (docs/images/demo.gif, plus a narrated MP4 that isn't committed) with
 * the shared Supertext movie kit (movie/movie-kit.mjs), as the demo's editor: the Features page in English,
 * Translate with Supertext into German, the waiting state, the German draft. Run it against a fresh local
 * demo whose package talks to stand-in.mjs (started with STAND_IN_DELAY=4). See docs/DEVELOPER.md → Demo movie.
 *
 *   BASE_URL=http://127.0.0.1:8095 DEMO_EDITOR_EMAIL=… DEMO_EDITOR_PASSWORD=… npm run movie
 *
 * MOVIE_OUT: where the MP4 goes (default Tests/Docs/movie/out/Umbraco-Supertext-Translation.mp4, git-ignored).
 * CHROMIUM_PATH: optional Chromium binary. MOVIE_VOICE=0: no voice (captions only).
 */
import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import { mkdirSync, mkdtempSync, rmSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { MovieKit } from './movie/movie-kit.mjs';

const B = process.env.BASE_URL || 'http://127.0.0.1:8095';
const need = (name) => process.env[name] ?? (() => { throw new Error(`Set ${name}`); })();
const here = (p) => fileURLToPath(new URL(p, import.meta.url));
const mp4 = process.env.MOVIE_OUT ?? here('movie/out/Umbraco-Supertext-Translation.mp4');
const gif = here('../../docs/images/demo.gif');
mkdirSync(dirname(mp4), { recursive: true });
const workDir = mkdtempSync(join(tmpdir(), 'umbraco-movie-'));

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1, locale: 'en-US' });
page.setDefaultTimeout(90_000);

// Sign in as the editor and open Features (not recorded).
await page.goto(`${B}/umbraco`);
await page.getByLabel(/Email/i).first().fill(need('DEMO_EDITOR_EMAIL'));
await page.getByLabel(/Password/i).first().fill(need('DEMO_EDITOR_PASSWORD'));
await page.getByRole('button', { name: /Login/i }).first().click();
await page.waitForURL(/\/umbraco\/section\//, { timeout: 60_000 });
await page.getByText('Home', { exact: true }).first().waitFor({ timeout: 60_000 });
const home = page.locator('umb-document-tree-item, umb-tree-item').filter({ hasText: 'Home' }).first();
await home.locator('#caret-button').first().click();
await page.getByRole('link', { name: 'Features' }).first().click();
const translateButton = page.getByRole('button', { name: 'Translate with Supertext' });
await translateButton.waitFor({ timeout: 30_000 });
await page.waitForTimeout(1500);

/** The workspace header's variant selector (the tree's language switcher has the same text, further left). */
const inHeader = async (text) => {
	const all = page.getByText(text, { exact: true });
	for (let i = 0; i < (await all.count()); i++) {
		const box = await all.nth(i).boundingBox();
		if (box && box.x > 600 && box.y < 130) return all.nth(i);
	}
	throw new Error(`"${text}" not found in the workspace header`);
};
/** Scrolls the document's content panel smoothly to its rows (the block list, down) or back to the top (up). */
const scroll = async (down) => {
	await page.locator('umb-property-layout').nth(down ? 3 : 0).evaluate((el) => el.scrollIntoView({ block: 'start', behavior: 'smooth' }));
	await page.waitForTimeout(900);
};

const kit = await MovieKit.start(page, { workDir, assets: here('movie/assets'), voiceCache: here('movie/voice') });

// 1. The page in English.
await kit.say("Here's a page in Umbraco, written in English. It's called Features.", {
	caption: 'A page in English',
	during: async () => {
		await kit.moveTo(page.getByRole('link', { name: 'Features' }).first());
		await scroll(true);
		await kit.hold(700);
		await scroll(false);
	},
});

// 2. Open Translate with Supertext and choose German.
const tick = (name) => page.locator('uui-checkbox').filter({ hasText: name }).first();
await kit.say('Click Translate with Supertext, next to Save and publish.', {
	caption: 'Click <b>Translate with Supertext</b>',
	during: async () => {
		await kit.click(translateButton);
		await page.getByText('Translate from').waitFor();
		await page.waitForTimeout(700);
	},
});
await kit.say('Choose the languages. This time, untick French and Italian, and keep just German.', {
	caption: 'Choose the languages: <b>German</b>',
	during: async () => {
		await kit.click(tick('French (Switzerland)'));
		await kit.click(tick('Italian (Switzerland)'));
		await kit.moveTo(tick('German (Switzerland)'));
	},
});
await kit.say('Then click Translate.', {
	caption: 'Click <b>Translate</b>',
	during: () => kit.click(page.getByRole('button', { name: 'Translate', exact: true })),
});

// 3. Waiting.
await kit.say('Supertext translates the content.', {
	caption: 'Supertext translates the content…',
	during: () => kit.moveTo(tick('German (Switzerland)')),
});
kit.pause();
await page.getByText('Translated with Supertext').first().waitFor({ timeout: 180_000 });
await page.waitForTimeout(300);
kit.resume();
await kit.say('A few seconds later, the German version is saved as a draft.', {
	caption: 'The German version is saved as a draft',
	during: () => kit.hold(800),
});

// 4. The German version.
await kit.say('To check it, open the language menu at the top, and switch the page to German.', {
	caption: 'Switch the page to <b>German</b>',
	during: async () => {
		await kit.click(await inHeader('English (United States)'));
		await page.getByText('Funktionen', { exact: true }).first().waitFor();
		await page.waitForTimeout(300);
		await kit.click(page.getByText('Funktionen', { exact: true }).first());
		await page.waitForTimeout(1000);
	},
});
await kit.say('Here it is, in German. Review it, then publish it with Save and publish.', {
	caption: 'Review the German version, then <b>Save and publish</b>',
	during: async () => {
		await scroll(true);
		await kit.hold(1200);
		await scroll(false);
		await kit.moveTo(page.getByRole('button', { name: /^Save and publish/ }).first());
	},
});
await kit.caption('');

await kit.finish(mp4, { system: 'Umbraco', repo: 'Umbraco-Supertext-Translation', gif });
await browser.close();
rmSync(workDir, { recursive: true, force: true });
console.log(`Saved ${mp4} and ${gif} (${(statSync(gif).size / 1e6).toFixed(2)} MB)`);
