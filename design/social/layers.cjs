// The logo's layers at 4× (2400 × 2800): the full logo and the emblem alone, transparent.
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 600, height: 700 }, deviceScaleFactor: 4 });
  await p.goto('file://' + path.join(__dirname, '../logo/logo.html'));
  await p.evaluate(() => document.fonts.ready); await p.waitForTimeout(400);
  const only = ids => p.evaluate(ids => { for (const s of document.querySelectorAll('svg.layer')) s.style.display = ids.includes(s.id) ? 'block' : 'none'; }, ids);
  await only(['shield', 'domino', 'die', 'crown', 'ribbon']);
  await p.screenshot({ path: path.join(__dirname, 'out/logo-4x.png'), omitBackground: true });
  await only(['shield', 'domino', 'die', 'crown']);
  await p.screenshot({ path: path.join(__dirname, 'out/emblem-4x.png'), omitBackground: true });
  await b.close();
})();
