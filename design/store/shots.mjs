// Store screenshots: a caption over each game screenshot (1920 × 1080), in English and Arabic.
import { chromium } from 'playwright';
import path from 'node:path';
const here = path.dirname(new URL(import.meta.url).pathname);
const [logs, out] = process.argv.slice(2);
const list = [
  ['en-1', 'm5-howto.png', 'Dominoes, Backgammon, Chess & Ludo', false],
  ['en-2', 'q-chat.png', 'Play at the café table with friends', false],
  ['en-3', 'g-tawla-2.png', 'Classic Egyptian tawla', false],
  ['en-4', 'g-chess-1.png', 'Chess with real clocks', false],
  ['en-5', 'g-ludo-1.png', 'Ludo for 2, 3 or 4', false],
  ['en-6', '3-cafe.png', 'Daily rewards that grow', false],
  ['en-7', 'm2-shop-coins.png', 'Collect tiles and dice', false],
  ['en-8', 'v-board-view.png', 'Zoom in on the board', false],
  ['ar-1', 'm5-howto.png', 'دومينو وطاولة وشطرنج ولودو', true],
  ['ar-2', 'q-chat.png', 'العب مع صحابك على ترابيزة القهوة', true],
  ['ar-3', '6-choose.png', 'دومينو زي القهوة بالظبط', true],
  ['ar-4', 'g-tawla-2.png', 'طاولة عادة ومحبوسة و٣١', true],
  ['ar-5', 'g-chess-1.png', 'شطرنج بساعة حقيقية', true],
  ['ar-6', 'g-ludo-1.png', 'لودو لـ ٢ و٣ و٤ لاعبين', true],
  ['ar-7', '3-cafe.png', 'مكافأة كل يوم وبتكبر', true],
  ['ar-8', 'v-board-view.png', 'قرّب على اللوح', true],
];
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
await page.goto('file://' + path.join(here, 'shots.html'));
await page.evaluate(() => document.fonts.ready); await page.waitForTimeout(1200);
for (const [name, file, cap, ar] of list) {
  await page.evaluate(([src, cap, ar]) => { const c = document.getElementById('cap'); c.textContent = cap; c.className = 'cap' + (ar ? ' ar' : ''); document.getElementById('img').src = src; }, ['file://' + path.join(logs, file), cap, ar]);
  await page.waitForFunction(() => document.getElementById('img').complete);
  await page.screenshot({ path: path.join(out, name + '.png') });
}
await browser.close();
