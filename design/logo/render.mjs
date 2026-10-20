// Renders the logo layers (design/logo/logo.html) to transparent PNGs for the game, plus the full logo and the app icon.
import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
const here = path.dirname(new URL(import.meta.url).pathname);
const out = process.argv[2] || path.join(here, 'out');
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: 600, height: 700 }, deviceScaleFactor: 2 });
await page.goto('file://' + path.join(here, 'logo.html'));
await page.evaluate(() => document.fonts.ready);
const layers = ['shield', 'domino', 'die', 'crown', 'spark', 'ribbon'];
async function only(ids) {
  await page.evaluate(ids => { for (const s of document.querySelectorAll('svg.layer')) s.style.display = ids.includes(s.id) ? 'block' : 'none'; }, ids);
}
for (const l of layers) {
  await only([l]);
  await page.screenshot({ path: path.join(out, l + '.png'), omitBackground: true, clip: { x: 0, y: 0, width: 600, height: 700 } });
}
// the whole logo, and the emblem alone (no ribbon, no spark)
await only(['shield', 'domino', 'die', 'crown', 'ribbon']);
await page.screenshot({ path: path.join(out, 'logo.png'), omitBackground: true, clip: { x: 0, y: 0, width: 600, height: 700 } });
await only(['shield', 'domino', 'die', 'crown']);
await page.screenshot({ path: path.join(out, 'emblem.png'), omitBackground: true, clip: { x: 0, y: 0, width: 600, height: 700 } });
// the app icon: the emblem on a deep red square
const icon = await browser.newPage({ viewport: { width: 512, height: 512 }, deviceScaleFactor: 2 });
const emblem = fs.readFileSync(path.join(out, 'emblem.png')).toString('base64');
await icon.setContent(`<html><body style="margin:0"><div style="width:512px;height:512px;background:radial-gradient(circle at 50% 40%,#8c1a2d,#4a0b16 60%,#1c0609);display:flex;align-items:center;justify-content:center;overflow:hidden">
  <img src="data:image/png;base64,${emblem}" style="height:530px;margin-top:34px"></div></body></html>`);
await icon.screenshot({ path: path.join(out, 'icon.png') });
await browser.close();
console.log('rendered to', out);
