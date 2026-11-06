// Premium store screenshots (1920 × 1080): the game in a phone over the brand's red and gold, with a headline.
//   node premium.mjs <folder of game captures> <out dir>
import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
const here = path.dirname(new URL(import.meta.url).pathname);
const [caps, out] = process.argv.slice(2);
const shots = JSON.parse(fs.readFileSync(path.join(here, 'premium.json'), 'utf8'));
fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
await page.goto('file://' + path.join(here, 'premium.html'));
await page.evaluate(() => document.fonts.ready);
for (const [lang, list] of Object.entries(shots)) list.forEach((s, i) => s.name = `${lang}-${i + 1}`);
for (const [lang, list] of Object.entries(shots)) for (const s of list) {
  await page.evaluate(s => window.show(s), { ...s, img: 'file://' + path.join(caps, s.img), ar: lang === 'ar' });
  await page.waitForFunction(() => document.getElementById('img').complete && document.getElementById('img').naturalWidth > 0);
  await page.waitForTimeout(150);
  await page.screenshot({ path: path.join(out, s.name + '.png') });
  console.log('shot', s.name);
}
await browser.close();
