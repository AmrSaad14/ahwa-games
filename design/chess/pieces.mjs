// The chess pieces as little pictures for the screens (the pieces taken, the promotion choice):
// UI/Chess/w_k.png … b_p.png, 96×96, drawn with DejaVu Sans's chess glyphs: a filled glyph in the side's
// colour under the outline glyph, with a soft shadow. `node pieces.mjs <out dir>`
import { chromium } from 'playwright';
import fs from 'node:fs';
const out = process.argv[2]; fs.mkdirSync(out, { recursive: true });
const solid = { k: '♚', q: '♛', r: '♜', b: '♝', n: '♞', p: '♟' };
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 96, height: 96 } });
for (const side of ['w', 'b']) for (const t of Object.keys(solid)) {
  const fill = side === 'w' ? '#f6ecd6' : '#2a1d16', edge = side === 'w' ? '#3a2a20' : '#d9b97a';
  await page.setContent(`<html><body style="margin:0;background:transparent"><div style="position:relative;width:96px;height:96px;font:84px 'DejaVu Sans';line-height:96px;text-align:center">
    <span style="position:absolute;inset:0;color:${fill};-webkit-text-stroke:5px ${edge};filter:drop-shadow(0 3px 2px rgba(0,0,0,.45))">${solid[t]}</span>
    <span style="position:absolute;inset:0;color:${fill}">${solid[t]}</span></div></body></html>`);
  await page.screenshot({ path: `${out}/${side}_${t}.png`, omitBackground: true });
}
await browser.close();
console.log('pieces done');
