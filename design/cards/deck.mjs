// Makes the cards from deck.html (the classic deck): c0 … c51, back.png, joker.png. `node deck.mjs <out dir> [sheet.png]`
import { chromium } from 'playwright';
import path from 'node:path';
const here = path.dirname(new URL(import.meta.url).pathname);
const out = process.argv[2];
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 150, height: 210 }, deviceScaleFactor: 1.4 });
await p.goto('file://' + path.join(here, 'deck.html'));
await p.evaluate(() => document.fonts.ready);
for (let id = -1; id < 53; id++) {
  await p.evaluate(i => window.card(i), id);
  await p.screenshot({ path: path.join(out, id < 0 ? 'back.png' : id === 52 ? 'joker.png' : `c${id}.png`), omitBackground: true, clip: { x: 0, y: 0, width: 150, height: 210 } });
}
await b.close();
console.log('deck done');
