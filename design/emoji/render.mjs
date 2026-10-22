// Renders the quick-chat emojis (emoji.html: the domino mascot) to 256 × 256 transparent PNGs for the game
// (Resources/UI/Emoji):  node render.mjs <out dir>
import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
const here = path.dirname(new URL(import.meta.url).pathname);
const out = process.argv[2]; fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: 128, height: 128 }, deviceScaleFactor: 2 });
await page.goto('file://' + path.join(here, 'emoji.html'));
const ids = await page.evaluate(() => [...document.querySelectorAll('svg')].map(d => d.id));
for (const id of ids) {
  await page.evaluate(id => { for (const d of document.querySelectorAll('svg')) d.style.display = d.id === id ? 'block' : 'none'; }, id);
  await page.screenshot({ path: path.join(out, id + '.png'), omitBackground: true });
}
await browser.close(); console.log(ids.join(' '));
