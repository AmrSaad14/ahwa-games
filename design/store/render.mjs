// Store art: the feature graphic (feature.html, 1024 × 500).
import { chromium } from 'playwright';
import path from 'node:path';
const here = path.dirname(new URL(import.meta.url).pathname);
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: 1024, height: 500 } });
await page.goto('file://' + path.join(here, 'feature.html'));
await page.evaluate(() => document.fonts.ready); await page.waitForTimeout(1200);
await page.screenshot({ path: path.join(here, 'feature-1024x500.png') });
await browser.close();
