const { chromium } = require('playwright'); const path = require('path');
(async () => { const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1080, height: 1080 } });
  await p.goto('file://' + path.join(__dirname, 'profile.html')); await p.waitForTimeout(300);
  await p.screenshot({ path: path.join(__dirname, 'out/profile-1080.png') }); await b.close(); })();
