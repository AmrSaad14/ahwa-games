// Headless browser walk-through of the real UI against a running server.
// Usage: CLIENT_STUBS=1 npm run build && node tests/ui-smoke.mjs [screenshotDir]
// (CLIENT_STUBS replaces three.js with a no-op so this runs on machines without a GPU.)
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const shots = process.argv[2] || fs.mkdtempSync(path.join(os.tmpdir(), 'domino-ui-'));
fs.mkdirSync(shots, { recursive: true });
const PORT = 8099;
const db = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'domino-db-')), 'ui.db');
const server = spawn(process.execPath, ['dist/server.cjs'], {
  env: { ...process.env, PORT: String(PORT), HOST: '127.0.0.1', DB_PATH: db, STATIC_DIR: 'client/dist', BOT_FILL_SECONDS: '1', NEXT_ROUND_SECONDS: '2' },
  stdio: 'inherit',
});
await new Promise(r => setTimeout(r, 800));
const url = `http://127.0.0.1:${PORT}/`;
const browser = await chromium.launch();
const phone = { viewport: { width: 844, height: 390 }, deviceScaleFactor: 2, hasTouch: true };   // landscape phone
const errors = [];
const page = async () => {
  const ctx = await browser.newContext(phone);
  const p = await ctx.newPage();
  p.on('pageerror', e => errors.push(e.message));
  p.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  await p.goto(url);
  pages.push(p);
  return p;
};
const pages = [];
const step = async (name, fn) => {
  process.stdout.write(`• ${name} … `);
  try { await fn(); } catch (e) { for (const [i, p] of pages.entries()) await p.screenshot({ path: path.join(shots, `fail-${i}.png`) }).catch(() => {}); throw e; }
  console.log('ok');
};

try {
  const a = await page();
  await step('home screen connects', async () => {
    await a.waitForSelector('.btn.big:not([disabled])', { timeout: 8000 });
    await a.screenshot({ path: path.join(shots, '1-home.png') });
  });
  await step('switch to Arabic (right-to-left) and back', async () => {
    await a.click('.topbar .chip');
    await a.waitForFunction(() => document.documentElement.dir === 'rtl');
    await a.screenshot({ path: path.join(shots, '1b-home-ar.png') });
    await a.click('.topbar .chip');
    await a.waitForFunction(() => document.documentElement.dir === 'ltr');
  });
  await step('practice game deals 7 tiles and a move can be played', async () => {
    await a.click('text=Practice');
    await a.waitForSelector('.hand .tile');
    await a.waitForFunction(() => document.querySelectorAll('.hand .tile').length === 7);
    // wait for our turn; draw from the boneyard until something fits
    for (let i = 0; i < 30 && !(await a.$('.hand .tile.ok')); i++) {
      const act = await a.$('.btn.act'); if (act) await act.click();
      await a.waitForTimeout(500);
    }
    await a.waitForSelector('.hand .tile.ok', { timeout: 15000 });
    await a.screenshot({ path: path.join(shots, '2-practice.png') });
    const before = await a.$$eval('.hand .tile', e => e.length);
    await a.click('.hand .tile.ok');
    await a.waitForTimeout(300);
    const choose = await a.$('.bottom .row.center .btn');
    if (choose) await choose.click();
    await a.waitForFunction(n => document.querySelectorAll('.hand .tile').length === n - 1, before);
  });
  await step('emote shows a bubble', async () => {
    await a.click('.handbar .iconbtn');
    await a.click('.emotes .chip >> nth=1');
    await a.waitForSelector('.bubble');
    await a.screenshot({ path: path.join(shots, '3-emote.png') });
  });
  await step('leave practice', async () => {
    await a.click('.hud .iconbtn');
    await a.waitForSelector('.home');
  });
  await step('chess practice: clocks, a legal move by tapping is offered', async () => {
    await a.click('.game >> nth=2');
    await a.click('text=Practice');
    await a.waitForSelector('.clock');
    await a.waitForFunction(() => /\d:\d\d/.test(document.querySelector('.clock')?.textContent || ''));
    await a.click('.hud .iconbtn');
    await a.waitForSelector('.home');
  });
  for (const [i, g] of [[1, 'tawla'], [3, 'ludo']]) {
    await step(`${g} practice: roll the dice on your turn`, async () => {
      await a.click(`.game >> nth=${i}`);
      await a.click('text=Practice');
      await a.waitForSelector('#roll-btn', { timeout: 20000 });
      await a.click('#roll-btn', { force: true });
      await a.waitForFunction(() => !document.querySelector('#roll-btn'));
      await a.click('.hud .iconbtn');
      await a.waitForSelector('.home');
    });
  }
  await a.click('.game >> nth=0');
  let code = '';
  await step('create a private room', async () => {
    await a.click('text=Play with friends');
    await a.click('.sheet-card .btn.big');
    await a.waitForSelector('.codebig');
    code = (await a.textContent('.codebig')).trim();
    if (!/^[A-Z0-9]{5}$/.test(code)) throw new Error('bad code ' + code);
    await a.screenshot({ path: path.join(shots, '4-room.png') });
  });
  const b = await page();
  await step('friend joins with the code', async () => {
    await b.waitForSelector('.btn.big:not([disabled])');
    await b.click('.topbar .chip.profile'); // settings
    await b.fill('#set-name', 'Mona');
    await b.press('#set-name', 'Enter');
    await b.click('.topbar .iconbtn');
    await b.click('.home .btn.tq');
    await b.fill('#join-code', code);
    await b.click('#join-code + .btn');
    await b.waitForSelector('.codebig');
    await a.waitForFunction(() => [...document.querySelectorAll('.seat .nm')].some(e => e.textContent.includes('Mona')));
  });
  await step('host starts, both are dealt in', async () => {
    await a.click('.btn.big');
    await a.waitForFunction(() => document.querySelectorAll('.hand .tile').length === 7);
    await b.waitForFunction(() => document.querySelectorAll('.hand .tile').length === 7);
    await a.screenshot({ path: path.join(shots, '5-online-a.png') });
    await b.screenshot({ path: path.join(shots, '5-online-b.png') });
    const ha = await a.$$eval('.hand .tile', els => els.map(e => e.getAttribute('aria-label')));
    const hb = await b.$$eval('.hand .tile', els => els.map(e => e.getAttribute('aria-label')));
    if (ha.some(x => hb.includes(x))) throw new Error('players see the same tile');
  });
  await step('the player whose turn it is can move', async () => {
    const mover = (await a.$('.hand .tile.ok, .btn.act')) ? a : b;
    for (let i = 0; i < 30 && !(await mover.$('.hand .tile.ok')); i++) { const d = await mover.$('.btn.act'); if (d) await d.click(); await mover.waitForTimeout(300); }
    const before = await mover.$$eval('.hand .tile', e => e.length);
    await mover.click('.hand .tile.ok');
    await mover.waitForTimeout(300);
    const choose = await mover.$('.bottom .row.center .btn'); if (choose) await choose.click();
    await mover.waitForFunction(n => document.querySelectorAll('.hand .tile').length === n - 1, before);
  });
  await step('leave asks for confirmation mid-game', async () => {
    await a.click('.hud .iconbtn');
    await a.waitForSelector('.overlay .btn.danger');
    await a.screenshot({ path: path.join(shots, '6-leave.png') });
    await a.click('.overlay .btn.danger');
    await a.waitForSelector('.home');
  });
  const c = await page();
  await step('quick match fills with computer players', async () => {
    await c.waitForSelector('.btn.big:not([disabled])');
    await c.click('.mode >> nth=2');
    await c.click('.btn.big');
    await c.waitForSelector('.spinner');
    await c.screenshot({ path: path.join(shots, '7-search.png') });
    await c.waitForFunction(() => document.querySelectorAll('.hand .tile').length === 7, null, { timeout: 10000 });
    await c.waitForFunction(() => document.querySelectorAll('.sc').length === 4);
  });
  await step('leaderboard opens', async () => {
    await a.click('.home-links .link >> nth=0');
    await a.waitForSelector('.sheet-card h2');
    await a.screenshot({ path: path.join(shots, '8-leaderboard.png') });
  });
  const relevant = errors.filter(e => !/fonts\.g|ERR_NAME|net::|Failed to load resource/.test(e));
  if (relevant.length) throw new Error('page errors:\n' + relevant.join('\n'));
  console.log('UI smoke test passed. Screenshots:', shots);
} finally {
  await browser.close();
  server.kill();
}
