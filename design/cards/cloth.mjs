// The café tablecloth under the card games: unity/Assets/Resources/UI/Cards/cloth.png. `node cloth.mjs <out.png>`
import { chromium } from 'playwright';
import path from 'node:path';
const here = path.dirname(new URL(import.meta.url).pathname);
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1200, height: 560 } });
await p.goto('file://' + path.join(here, 'cloth.html'));
await p.waitForTimeout(300);
await p.screenshot({ path: process.argv[2], omitBackground: true });
await b.close();
console.log('cloth done');
