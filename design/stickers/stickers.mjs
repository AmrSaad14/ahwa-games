// The quick-chat stickers: Egyptian phrases as drawn speech bubbles, 256 × 256 PNG with transparency.
import { chromium } from 'playwright';
import path from 'node:path';
const here = path.dirname(new URL(import.meta.url).pathname);
const out = process.argv[2];
const S = [
  ['s_yasater', 'يا ساتر!', '😱', ['#e8505b', '#b3242f'], -6, 44],
  ['s_allem', 'علّم عليه', '😎', ['#2f9d6b', '#1d6b48'], 5, 44],
  ['s_khalasna', 'خلّصنا بقى', '😴', ['#4a7bd8', '#2c4f9a'], -4, 40],
  ['s_halawa', 'يا حلاوة!', '😍', ['#f0a23a', '#c46d12'], 6, 44],
  ['s_afla', 'آفلة!', '🔒', ['#8e44ad', '#5b2470'], -5, 54],
  ['s_hazz', 'إيه الحظ ده', '🍀', ['#16a39a', '#0d6b65'], 4, 38],
  ['s_rakkez', 'ركّز يا معلم', '🧐', ['#d35400', '#8a3600'], -5, 38],
  ['s_teesh', 'تعيش وتاخد غيرها', '🤝', ['#7f8c8d', '#4d5657'], 3, 27],
];
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 256, height: 256 } });
await p.goto('file://' + path.join(here, 'stickers.html'));
await p.evaluate(() => document.fonts.ready);
for (const [k, t, e, c, r, fs] of S) {
  await p.evaluate(a => window.show(a), { t, e, c, r, fs });
  await p.waitForTimeout(80);
  await p.screenshot({ path: path.join(out, k + '.png'), omitBackground: true });
  console.log(k);
}
await b.close();
