#!/usr/bin/env node
/**
 * Records the install and configuration movie (a narrated MP4 that isn't committed) with the shared Supertext
 * movie kit (movie/movie-kit.mjs): the install commands from docs/INSTALLATION.md and the API key in a terminal,
 * then, as the demo's administrator, Settings › Languages, Settings › Supertext (key status, Check API key,
 * the language mapping) and where editors find Translate with Supertext. Run it against a local demo whose
 * package talks to stand-in.mjs (it needn't be fresh). See docs/DEVELOPER.md → Demo movie.
 *
 *   BASE_URL=http://127.0.0.1:8095 DEMO_ADMIN_EMAIL=… DEMO_ADMIN_PASSWORD=… npm run movie:install
 *
 * MOVIE_OUT: where the MP4 goes (default Tests/Docs/movie/out/Umbraco-Supertext-Translation-install.mp4, git-ignored).
 * CHROMIUM_PATH: optional Chromium binary. MOVIE_VOICE=0: no voice (captions only).
 */
import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { MovieKit } from './movie/movie-kit.mjs';

const B = process.env.BASE_URL || 'http://127.0.0.1:8095';
const need = (name) => process.env[name] ?? (() => { throw new Error(`Set ${name}`); })();
const here = (p) => fileURLToPath(new URL(p, import.meta.url));
const mp4 = process.env.MOVIE_OUT ?? here('movie/out/Umbraco-Supertext-Translation-install.mp4');
mkdirSync(dirname(mp4), { recursive: true });
const workDir = mkdtempSync(join(tmpdir(), 'umbraco-install-movie-'));

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });
const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1, locale: 'en-US' });
page.setDefaultTimeout(90_000);
// The movie runs against the stand-in on this machine: show the addresses users see instead of local ones,
// also inside the backoffice's shadow DOM.
await page.addInitScript(() => {
	const fix = (root) => {
		const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
		for (let n = walker.currentNode; n; n = walker.nextNode()) {
			if (n.shadowRoot) fix(n.shadowRoot);
			if (n.nodeType === 3 && /127\.0\.0\.1|localhost/.test(n.textContent)) {
				n.textContent = n.textContent.replace(/https?:\/\/(127\.0\.0\.1|localhost):\d+\/v1\//g, 'https://api.supertext.com/v1/')
					.replace(/https?:\/\/(127\.0\.0\.1|localhost)(:\d+)?/g, 'https://www.example.com');
			}
		}
	};
	setInterval(() => document.body && fix(document.body), 30);
});

// Sign in as the administrator (not recorded).
await page.goto(`${B}/umbraco`);
await page.getByLabel(/Email/i).first().fill(need('DEMO_ADMIN_EMAIL'));
await page.getByLabel(/Password/i).first().fill(need('DEMO_ADMIN_PASSWORD'));
await page.getByRole('button', { name: /Login/i }).first().click();
await page.waitForURL(/\/umbraco\/section\//, { timeout: 60_000 });
await page.getByText('Home', { exact: true }).first().waitFor({ timeout: 60_000 });

const kit = await MovieKit.start(page, { workDir, assets: here('movie/assets'), voiceCache: here('movie/voice') });
const pkg = 'Supertext.Umbraco.Translation';

/** Opens a page without recording the loading. */
const open = async (path, ready) => {
	kit.pause();
	await page.goto(B + path);
	if (ready) await ready();
	await page.waitForTimeout(1200);
	kit.resume();
};
/** Moves the cursor to a point and clicks there (for controls without a usable label). */
const clickAt = async (x, y) => {
	kit.cursor = { x, y };
	await kit.sync();
	await page.waitForTimeout(550);
	await page.evaluate(([x, y]) => window.__stk?.ripple(x, y), [x, y]);
	await page.mouse.click(x, y);
	await page.waitForTimeout(300);
};

// 1. Build the package and add it (docs/INSTALLATION.md → 1. Add the package).
await kit.say("The package isn't on NuGet yet, so start by building it yourself. Clone the repository from GitHub, then run dotnet pack. It creates the package file in a local folder.", {
	caption: 'Clone the package and <b>dotnet pack</b> it',
	during: () => kit.terminal({
		title: 'my-site',
		lines: [
			{ cmd: 'git clone https://github.com/Supertext/Umbraco-Supertext-Translation' },
			{ out: "Cloning into 'Umbraco-Supertext-Translation'..." },
			{ cmd: `dotnet pack Umbraco-Supertext-Translation/src/${pkg} -c Release -o ./packages` },
			{ out: `  ${pkg} -> bin/Release/net10.0/${pkg}.dll`, wait: 500 },
			{ out: `  Successfully created package 'packages/${pkg}.0.1.0.nupkg'.` },
		],
	}),
});
await kit.say('Then, in your Umbraco project, add that folder as a local package source, and add the package with dotnet add package.', {
	caption: 'Add the folder as a source, then <b>dotnet add package</b>',
	during: () => kit.terminal({
		title: 'my-site',
		lines: [
			{ cmd: 'dotnet nuget add source ../packages --name supertext-local' },
			{ out: 'Package source with Name: supertext-local added successfully.' },
			{ cmd: `dotnet add package ${pkg} --version 0.1.0` },
			{ out: `info : PackageReference for package '${pkg}' version '0.1.0' added to file 'my-site.csproj'.` },
		],
	}),
});
await kit.say('Set your Supertext API key as the environment variable SUPERTEXT_API_KEY, or in appsettings.json. Then restart the site.', {
	caption: 'Set <b>SUPERTEXT_API_KEY</b> and restart the site',
	during: () => kit.terminal({
		title: 'my-site',
		lines: [
			{ cmd: 'export SUPERTEXT_API_KEY=st-demo-0000-0000-0000' },
			{ cmd: 'dotnet run' },
			{ out: 'info: Umbraco.Cms.Core.Services.IRuntime[0]', wait: 400 },
			{ out: '      Booted. Runtime level: Run' },
			{ out: 'info: Microsoft.Hosting.Lifetime[0]' },
			{ out: '      Application started.' },
		],
	}),
});
await kit.say('Generate your API key at supertext.com, Integrations, API. No account yet? Create one at supertext.com.', {
	caption: 'Generate your API key at <b>supertext.com → Integrations → API</b>. No account yet? Create one at supertext.com.',
});

// 2. Languages: Umbraco's own setup.
await open('/umbraco/section/settings/workspace/language-root', () => page.getByText('German (Switzerland)').first().waitFor());
await kit.say("Supertext uses Umbraco's own languages. Add them under Settings, Languages, and let your document types vary by culture.", {
	caption: '<b>Settings › Languages</b>, and document types that <b>vary by culture</b>',
	during: () => kit.moveTo(page.getByText('German (Switzerland)').first()),
});

// 3. Settings › Supertext: the key.
const tab = page.getByRole('tab', { name: 'Supertext' }).or(page.locator('a, uui-tab').filter({ hasText: /^Supertext$/ })).first();
await open('/umbraco/section/settings/dashboard', () => tab.waitFor());
await kit.say('Then open the Supertext dashboard: Settings, Supertext. The API key is set.', {
	caption: 'Open <b>Settings › Supertext</b>: the API key is set',
	during: async () => {
		await kit.click(tab);
		await page.getByText('Send version information to Supertext').first().waitFor();
		await page.waitForTimeout(500);
		await kit.moveTo(page.locator('supertext-settings-dashboard uui-box').first().getByText('Set', { exact: true }));
	},
});
await kit.say('Click Check API key. The connection to Supertext works.', {
	caption: '<b>Check API key</b>: the connection works',
	during: async () => {
		await kit.click(page.getByRole('button', { name: 'Check API key' }));
		await page.waitForTimeout(800);
		await kit.moveTo(page.getByText('API key checked: the connection to Supertext works.').first());
	},
});

// 4. Language mapping.
const languages = page.locator('supertext-settings-dashboard uui-box').nth(1);
const german = languages.locator('select').nth(1);
await kit.say('Under Languages, each Umbraco language gets a Supertext language. German is set to Swiss High German. Pick another one if you need it.', {
	caption: '<b>Languages</b>: a Supertext language for each language',
	during: async () => {
		await languages.evaluate((el) => el.scrollIntoView({ block: 'start', behavior: 'smooth' }));
		await page.waitForTimeout(900);
		await kit.moveTo(german);
		await page.evaluate(([x, y]) => window.__stk?.ripple(x, y), [kit.cursor.x, kit.cursor.y]);
		await german.selectOption({ label: 'Swiss High German (de-CH)' });
	},
});
await kit.say('Translation review can be ticked for each language. Leave it off for now.', {
	caption: '<b>Translation review</b>: optional, per language',
	during: () => kit.moveTo(languages.locator('uui-checkbox').nth(1)),
});
await kit.say('Click Save. The mapping applies to the next translation, no restart needed.', {
	caption: 'Click <b>Save</b>',
	during: async () => {
		const save = languages.getByRole('button', { name: 'Save' });
		await save.evaluate((el) => el.scrollIntoView({ block: 'center', behavior: 'smooth' }));
		await page.waitForTimeout(700);
		await kit.click(save);
		await page.waitForTimeout(700);
	},
});

// 5. Where editors find it.
await open('/umbraco/section/content', async () => {
	await page.locator('umb-document-tree-item, umb-tree-item').filter({ hasText: 'Home' }).first().locator('#caret-button').first().click();
	await page.getByRole('link', { name: 'Features' }).first().click();
	await page.getByRole('button', { name: 'Translate with Supertext' }).waitFor();
});
await kit.say('Done. Editors now find Translate with Supertext on every page, next to Save and publish,', {
	caption: 'Editors find <b>Translate with Supertext</b> on every page',
	during: () => kit.moveTo(page.getByRole('button', { name: 'Translate with Supertext' })),
});
await kit.say("and in the actions menu of each page, behind the three dots.", {
	caption: '… and in the page’s actions menu',
	during: async () => {
		await clickAt(1098, 93);
		await page.waitForTimeout(500);
		const entries = page.getByText('Translate with Supertext', { exact: true });
		for (let i = 0; i < (await entries.count()); i++) {
			const box = await entries.nth(i).boundingBox();
			if (box && box.y < 600) { await kit.moveTo(entries.nth(i)); break; }
		}
	},
});
await kit.caption('');

await kit.finish(mp4, { system: 'Umbraco', repo: 'Umbraco-Supertext-Translation', kind: 'install' });
await browser.close();
rmSync(workDir, { recursive: true, force: true });
console.log(`Saved ${mp4}`);
