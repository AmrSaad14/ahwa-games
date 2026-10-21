// Renders the studio logo (studio.html) to a transparent PNG for the launch splash (unity/Assets/Splash/StudioLogo.png).
import { chromium } from 'playwright';
import path from 'node:path';
const here = path.dirname(new URL(import.meta.url).pathname);
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: 900, height: 520 }, deviceScaleFactor: 1 });
await page.goto('file://' + path.join(here, 'studio.html'));
await page.waitForTimeout(1500);
await page.screenshot({ path: process.argv[2], omitBackground: true });
await browser.close();
