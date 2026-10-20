// Renders the menu icons (icons.html) to transparent 192 × 192 PNGs for the game (Resources/UI/Icons).
import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
const here = path.dirname(new URL(import.meta.url).pathname);
const out = process.argv[2];
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: 96, height: 96 }, deviceScaleFactor: 2 });
await page.goto('file://' + path.join(here, 'icons.html'));
const ids = await page.evaluate(() => [...document.querySelectorAll('svg')].map(s => s.id));
for (const id of ids) {
  await page.evaluate(id => { for (const s of document.querySelectorAll('svg')) s.style.display = s.id === id ? 'block' : 'none'; }, id);
  await page.screenshot({ path: path.join(out, id + '.png'), omitBackground: true });
}
await browser.close();
console.log(ids.join(' '));
