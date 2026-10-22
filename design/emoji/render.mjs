// Renders the quick-chat emojis (emoji.html) to 128 × 128 PNGs for the game (Resources/UI/Emoji).
import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
const here = path.dirname(new URL(import.meta.url).pathname);
const out = process.argv[2]; fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: 128, height: 128 } });
await page.goto('file://' + path.join(here, 'emoji.html'));
await page.evaluate(() => document.fonts.ready); await page.waitForTimeout(1500);
const ids = await page.evaluate(() => [...document.querySelectorAll('div')].map(d => d.id));
for (const id of ids) {
  await page.evaluate(id => { for (const d of document.querySelectorAll('div')) d.style.display = d.id === id ? 'block' : 'none'; }, id);
  await page.screenshot({ path: path.join(out, id + '.png'), omitBackground: true });
}
await browser.close(); console.log(ids.join(' '));
