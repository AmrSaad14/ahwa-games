// The engagement-visit advert, put together: the living-room film and the four game cut-ins (from the ad-salon
// release) under salon.html (grade, captions, game names, end card), with the mixed sound (tools/ad/salon/mix.py).
//   node compose-salon.mjs <film.mp4> <inserts dir> <timeline.json> <mix.wav> <out.mp4> <w> <h> <ar|en> [--preview t1,t2,…]
import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
import { execFileSync } from 'node:child_process';

const here = path.dirname(new URL(import.meta.url).pathname);
const [filmP, insDir, timelineP, mixP, out, w, h, lang] = process.argv.slice(2);
const preview = process.argv.includes('--preview') ? process.argv[process.argv.indexOf('--preview') + 1].split(',').map(Number) : null;
const FPS = 30;
const work = path.join(path.dirname(path.resolve(out)), path.basename(out, '.mp4') + '-work');
const frames = path.join(work, 'frames');
fs.rmSync(work, { recursive: true, force: true }); fs.mkdirSync(frames, { recursive: true });

function extract(src, dir, scale) {
  fs.mkdirSync(dir, { recursive: true });
  execFileSync('ffmpeg', ['-loglevel', 'error', '-i', src, '-vf', scale, '-q:v', '2', path.join(dir, 'f%05d.jpg')]);
  return fs.readdirSync(dir).filter(f => f.endsWith('.jpg')).sort().map(f => 'file://' + path.join(dir, f));
}
const files = extract(filmP, path.join(work, 'film'), `scale=${w}:${h}:force_original_aspect_ratio=increase,crop=${w}:${h}`);
const ins = {};
for (const g of ['tawla', 'domino', 'ludo', 'chess']) {
  const p = path.join(insDir, `ins-${g}.mp4`);
  if (fs.existsSync(p)) ins[g] = extract(p, path.join(work, 'ins-' + g), +w > +h ? `scale=${w}:${h}` : 'scale=1280:720');
}
const lines = JSON.parse(fs.readFileSync(timelineP, 'utf8')).lines;
const D = files.length / FPS;

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: +w, height: +h } });
await page.goto(`file://${path.join(here, 'salon.html')}?w=${w}&h=${h}&lang=${lang}`);
await page.evaluate(([f, l, d, i]) => { window.FRAMES = f; window.LINES = l; window.DURATION = d; window.INS = i; }, [files, lines, D, ins]);
await page.waitForFunction(() => window.READY === true);
await page.waitForTimeout(500);
const times = preview ?? Array.from({ length: files.length }, (_, i) => i / FPS);
let n = 0;
for (const t of times) {
  await page.evaluate(t => window.render(t), t);
  await page.evaluate(() => Promise.all([...document.images].filter(i => i.style.display !== 'none' && !i.complete).map(i => new Promise(r => { i.onload = i.onerror = r; }))));
  await page.screenshot({ path: path.join(frames, `f${String(n++).padStart(5, '0')}.jpg`), type: 'jpeg', quality: 93 });
}
await browser.close();
if (preview) { console.log('preview frames in', frames); process.exit(0); }
execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-framerate', String(FPS), '-i', path.join(frames, 'f%05d.jpg'), '-i', mixP,
  '-c:v', 'libx264', '-preset', 'slow', '-crf', '17', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '256k', '-shortest', '-movflags', '+faststart', out], { stdio: 'inherit' });
fs.rmSync(work, { recursive: true, force: true });
console.log('wrote', out, D.toFixed(1), 's');
