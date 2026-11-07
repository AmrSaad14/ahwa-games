// The playing cards for basra, tarneeb and konkan: 52 faces (c0 … c51: suit * 13 + rank - 1, suits ♠ ♥ ♦ ♣),
// the back and the joker. `node cards.mjs <out> joker` makes just the joker.
import { chromium } from 'playwright';
import path from 'node:path';
const here = path.dirname(new URL(import.meta.url).pathname);
const out = process.argv[2];
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 150, height: 210 }, deviceScaleFactor: 1.4 });
await p.goto('file://' + path.join(here, 'cards.html'));
await p.evaluate(() => document.fonts.ready);
for (let id = process.argv[3] === 'joker' ? 52 : -1; id < 53; id++) {
  await p.evaluate(i => window.card(i), id);
  await p.screenshot({ path: path.join(out, id < 0 ? 'back.png' : id === 52 ? 'joker.png' : `c${id}.png`), omitBackground: true, clip: { x: 0, y: 0, width: 150, height: 210 } });
}
await b.close();
console.log('cards done');
