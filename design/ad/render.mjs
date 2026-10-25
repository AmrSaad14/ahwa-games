// Renders the advert (ad.html) frame by frame and puts it together with its sound:
//   node render.mjs <out.mp4> <w> <h> <ar|en> <clips.json> [fps] [--preview t1,t2,…]
// clips.json: { clips: [{ "from": 19.6, "to": 22, "fps": 12, "files": ["file:///…/f0001.png", …] }, …], cards: ["file:///…png", ×3] } (the game's own footage)
import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
import { execFileSync } from 'node:child_process';

const here = path.dirname(new URL(import.meta.url).pathname);
const [out, w, h, lang, clipsFile, fpsArg] = process.argv.slice(2);
const preview = process.argv.includes('--preview') ? process.argv[process.argv.indexOf('--preview') + 1].split(',').map(Number) : null;
const fps = +fpsArg || 30;
const media = clipsFile && fs.existsSync(clipsFile) ? JSON.parse(fs.readFileSync(clipsFile, 'utf8')) : [];
const clips = Array.isArray(media) ? media : media.clips ?? [], cards = Array.isArray(media) ? [] : media.cards ?? [];
const frames = path.join(path.dirname(out), path.basename(out, '.mp4') + '-frames');
fs.rmSync(frames, { recursive: true, force: true }); fs.mkdirSync(frames, { recursive: true });

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM || undefined });
const page = await browser.newPage({ viewport: { width: +w, height: +h } });
await page.goto(`file://${path.join(here, 'ad.html')}?w=${w}&h=${h}&lang=${lang}`);
await page.evaluate(([c, k]) => { window.CLIPS = c; window.CARDS = k; }, [clips, cards]);
await page.waitForFunction(() => window.READY === true);
await page.waitForTimeout(800);
const D = await page.evaluate(() => window.DURATION);
const times = preview ?? Array.from({ length: Math.round(D * fps) }, (_, i) => i / fps);
let n = 0;
for (const t of times) {
  await page.evaluate(t => window.render(t), t);
  await page.evaluate(() => Promise.all([...document.images].filter(i => i.style.display !== 'none' && !i.complete).map(i => new Promise(r => { i.onload = i.onerror = r; }))));
  await page.screenshot({ path: path.join(frames, `f${String(n++).padStart(5, '0')}.jpg`), type: 'jpeg', quality: 92 });
}
await browser.close();
if (preview) { console.log('preview frames in', frames); process.exit(0); }
const audio = path.join(here, 'build', 'ad-audio.wav');
const args = ['-y', '-loglevel', 'error', '-framerate', String(fps), '-i', path.join(frames, 'f%05d.jpg')];
if (fs.existsSync(audio)) args.push('-i', audio, '-c:a', 'aac', '-b:a', '192k', '-shortest');
args.push('-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', out);
execFileSync('ffmpeg', args, { stdio: 'inherit' });
fs.rmSync(frames, { recursive: true, force: true });
console.log('wrote', out);
