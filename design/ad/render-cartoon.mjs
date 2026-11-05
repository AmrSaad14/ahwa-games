// Renders the cartoon advert (cartoon.html): one encoded file per scene, joined, then the sound.
//   node render-cartoon.mjs <voice dir> <out.mp4> [--preview t1,t2,…]
// <voice dir>: the "ad-voice-cartoon" release (timeline.json, mouth.json, voice.wav); the sound comes from build/cartoon-audio.wav.
import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
import { execFileSync } from 'node:child_process';

const here = path.dirname(new URL(import.meta.url).pathname);
const [vdir, outArg] = process.argv.slice(2), out = path.resolve(outArg);
const preview = process.argv.includes('--preview') ? process.argv[process.argv.indexOf('--preview') + 1].split(',').map(Number) : null;
const TL = JSON.parse(fs.readFileSync(path.join(vdir, 'timeline.json'), 'utf8'));
const MOUTH = JSON.parse(fs.readFileSync(path.join(vdir, 'mouth.json'), 'utf8'));
const fps = 30, work = path.join(path.dirname(out), 'cartoon-work');
fs.rmSync(work, { recursive: true, force: true }); fs.mkdirSync(work, { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1080, height: 1920 } });
await page.goto('file://' + path.join(here, 'cartoon.html'));
await page.evaluate(([a, b]) => { window.TL = a; window.MOUTH = b; }, [TL, MOUTH]);
await page.waitForFunction(() => window.READY === true);
await page.evaluate(() => document.fonts.ready);
async function shot(t, file) {
  await page.evaluate(t => window.render(t), t);
  await page.evaluate(() => Promise.all([...document.querySelectorAll('image')].map(i => new Promise(r => { const im = new Image(); im.onload = im.onerror = r; im.src = i.getAttribute('href'); }))));
  await page.screenshot({ path: file, type: 'jpeg', quality: 92 });
}
if (preview) {
  for (const t of preview) await shot(t, path.join(path.dirname(out), `preview-${t.toFixed(2)}.jpg`));
  await browser.close(); console.log('previews in', path.dirname(out)); process.exit(0);
}
const scenes = await page.evaluate(() => window.SCENES());
const D = await page.evaluate(() => window.DURATION());
const parts = [];
let frame = 0;
for (let i = 0; i < scenes.length; i++) {
  const to = i === scenes.length - 1 ? D : scenes[i].to;
  const dir = path.join(work, 's' + i); fs.mkdirSync(dir);
  let n = 0;
  while (frame / fps < to - 1e-6) { await shot(frame / fps, path.join(dir, `f${String(n++).padStart(5, '0')}.jpg`)); frame++; }
  const tmp = path.join(work, `scene${i}.tmp.mp4`), fin = path.join(work, `scene${i}.mp4`);
  execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-framerate', String(fps), '-i', path.join(dir, 'f%05d.jpg'), '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p', '-r', String(fps), tmp]);
  fs.renameSync(tmp, fin); fs.rmSync(dir, { recursive: true, force: true }); parts.push(fin);
  console.log(`scene ${i}: ${n} frames`);
}
await browser.close();
fs.writeFileSync(path.join(work, 'list.txt'), parts.map(p => `file '${p}'`).join('\n'));
const silent = path.join(work, 'video.mp4');
execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-f', 'concat', '-safe', '0', '-i', path.join(work, 'list.txt'), '-c', 'copy', silent]);
const audio = path.join(here, 'build', 'cartoon-audio.wav');
execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-i', silent, '-i', audio, '-map', '0:v', '-map', '1:a', '-c:v', 'copy', '-af', 'loudnorm=I=-16:TP=-1.5:LRA=11', '-ar', '44100', '-c:a', 'aac', '-b:a', '192k', '-shortest', '-movflags', '+faststart', out]);
console.log('wrote', out, D.toFixed(2) + 's');
