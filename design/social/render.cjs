// Social art: the cover (cover.html) at 1640 × 624 and its phone crop for checking.
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const b = await chromium.launch();
  const p = await b.newPage({ viewport: { width: 1640, height: 624 }, deviceScaleFactor: 1 });
  await p.goto('file://' + path.join(__dirname, 'cover.html'));
  await p.evaluate(() => document.fonts.ready); await p.waitForTimeout(500);
  await p.screenshot({ path: path.join(__dirname, 'out/cover-1640x624.png') });
  await b.close();
})();
