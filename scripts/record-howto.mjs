// Records the how-to-play GIFs from the real game (practice against the computer) with captions,
// one per game and language, into client/public/howto/<game>-<lang>.gif.
//
//   npm run build && node scripts/record-howto.mjs [game ...]
//
// Needs Playwright with Chromium and ffmpeg. Runs the built server and drives the page with real taps and
// drags, so the GIFs always show the current look of the game.
import { chromium } from 'playwright';
import { spawn, execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), '..');
const outDir = path.join(root, 'client/public/howto');
fs.mkdirSync(outDir, { recursive: true });
const PORT = 8133;
const W = 390, H = 844;

const CAPTIONS = {
  domino: {
    en: ['Your tiles are at the bottom: the glowing ones fit', 'Drag a tile onto a matching end (or tap it)', 'Everyone has 10 seconds a turn', 'Empty your hand first to score the others’ pips'],
    ar: ['أحجارك تحت، والمنوّر هو اللي ينزل', 'اسحب الحجر لطرف بنفس الرقم (أو دوس عليه)', 'كل لاعب عنده ١٠ ثواني', 'خلّص أحجارك الأول وخد نقط الباقيين'],
  },
  tawla: {
    en: ['Roll the dice', 'Drag a glowing checker to a lit point', 'Use both dice; doubles play four times', 'Bring all 15 home, then bear them off'],
    ar: ['ارمي الزهر', 'اسحب قطعة منوّرة لخانة منوّرة', 'العب الزهرين، والدوبيش أربع مرات', 'دخّل الـ١٥ بيتك وبعدين قفّل'],
  },
  chess: {
    en: ['Drag a piece to a lit square', 'The computer replies with its move', 'Tap a piece to see its moves', 'Checkmate wins; out of time loses'],
    ar: ['اسحب القطعة لمربع منوّر', 'الكمبيوتر بيرد بنقلته', 'دوس على القطعة تشوف حركاتها', 'الكش مات يكسب، واللي وقته يخلص يخسر'],
  },
  ludo: {
    en: ['Roll the dice: a 6 brings a pawn out', 'Tap a glowing pawn to move it', 'Land on an opponent to send it home', 'Get all four pawns home to win'],
    ar: ['ارمي الزهر: الـ٦ تطلّع قطعة', 'دوس على قطعة منوّرة عشان تتحرك', 'انزل على الخصم ترجّعه البيت', 'دخّل الأربع قطع البيت تكسب'],
  },
};
const GAME_INDEX = { domino: 0, tawla: 1, chess: 2, ludo: 3 };
// which mode to record: chess uses the 10-minute clock so the recording never runs out of time
const MODE_INDEX = { domino: 0, tawla: 0, chess: 3, ludo: 0 };

const games = process.argv.slice(2).length ? process.argv.slice(2) : ['domino', 'tawla', 'chess', 'ludo'];
const server = spawn(process.execPath, ['dist/server.cjs'], {
  cwd: root, stdio: 'ignore',
  env: { ...process.env, PORT: String(PORT), HOST: '127.0.0.1', DB_PATH: path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'howto-')), 'r.db'), STATIC_DIR: 'client/dist' },
});
await new Promise(r => setTimeout(r, 900));
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });

try {
  for (const lang of (process.env.HOWTO_LANGS || 'ar,en').split(',')) for (const game of games) await record(game, lang);
} finally {
  await browser.close(); server.kill();
}

async function record(game, lang) {
  const frames = fs.mkdtempSync(path.join(os.tmpdir(), `howto-${game}-${lang}-`));
  const ctx = await browser.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 1, locale: lang === 'ar' ? 'ar-EG' : 'en-US' });
  const p = await ctx.newPage();
  await p.route('https://fonts.googleapis.com/**', r => r.fulfill({ contentType: 'text/css', body: '' }));
  await p.addInitScript(l => { try { localStorage.setItem('dom.lang', l); } catch { /* private mode */ } }, lang);
  await p.goto(`http://127.0.0.1:${PORT}/?rec`);
  await p.addStyleTag({ content: '.timer-row,.lbl .bar,.clock{display:none!important}' });
  await p.waitForTimeout(900);
  await p.locator('.game').nth(GAME_INDEX[game]).click();
  await p.locator('.mode').nth(MODE_INDEX[game]).click().catch(() => {});
  await p.locator('.sheet-card .btn.alt').last().click();   // practice
  await p.waitForTimeout(1500);

  let n = 0;
  const snap = async (count = 1, gap = 250) => {
    for (let i = 0; i < count; i++) {
      await p.screenshot({ path: path.join(frames, String(n++).padStart(4, '0') + '.png'), timeout: 120_000 });
      if (gap) await p.waitForTimeout(gap);
    }
  };
  const caption = async (i) => {
    await p.evaluate(({ text, step, rtl }) => {
      let el = document.getElementById('howto-cap');
      if (!el) {
        el = document.createElement('div'); el.id = 'howto-cap';
        el.style.cssText = 'position:fixed;left:14px;right:14px;top:78px;z-index:50;display:flex;align-items:center;gap:10px;padding:10px 14px;border-radius:14px;background:rgba(24,14,8,.92);border:2px solid #d9a84e;color:#f6eddb;font:600 17px/1.35 "IBM Plex Sans Arabic",system-ui,sans-serif;box-shadow:0 8px 20px rgba(0,0,0,.5)';
        document.body.append(el);
      }
      el.dir = rtl ? 'rtl' : 'ltr';
      el.innerHTML = `<b style="flex:none;width:28px;height:28px;border-radius:50%;background:#d9a84e;color:#24170c;display:grid;place-items:center;font-size:15px">${step}</b><span></span>`;
      el.querySelector('span').textContent = text;
    }, { text: CAPTIONS[game][lang][i], step: lang === 'ar' ? '١٢٣٤'[i] : String(i + 1), rtl: lang === 'ar' });
  };
  const view = () => p.evaluate(() => window.__ahwa.view());
  const myTurn = async () => { const v = await view(); return v && v.phase === 'play' && v.turn === v.you; };
  const waitMyTurn = async (ms = 70_000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { if (await myTurn()) return true; await p.waitForTimeout(300); } return false; };
  /** Press, move in a few visible steps while capturing, and let go. */
  const drag = async (a, b, shots = 4) => {
    await p.mouse.move(a.x, a.y); await p.mouse.down();
    for (let i = 1; i <= shots; i++) { await p.mouse.move(a.x + (b.x - a.x) * i / shots, a.y + (b.y - a.y) * i / shots, { steps: 3 }); await snap(1, 0); }
    await p.mouse.up();
  };
  const spot = (pick) => p.evaluate(pk => window.__ahwa.spot(pk), pick);

  if (game === 'domino') {
    await caption(0); await waitMyTurn(); await snap(5);
    await caption(1);
    const tile = p.locator('.hand .tile.ok').first();
    const box = await tile.boundingBox();
    if (box) {
      const a = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
      await p.mouse.move(a.x, a.y); await p.mouse.down();
      await p.mouse.move(a.x, a.y - 60, { steps: 4 }); await snap(1, 150);
      const v = await view();
      const end = v.line.f === null ? null : await p.evaluate(() => window.__ahwa.ghost('R') || window.__ahwa.ghost('L'));
      const b = end ?? { x: W / 2, y: 340 };
      for (let i = 1; i <= 4; i++) { await p.mouse.move(a.x + (b.x - a.x) * i / 4, a.y - 60 + (b.y - a.y + 60) * i / 4, { steps: 3 }); await snap(1, 0); }
      await p.mouse.up();
    }
    await snap(4);
    await caption(2); await snap(3, 300);
    // the computer plays, then a second tile goes down with a simple tap
    if (await waitMyTurn()) {
      await snap(2, 200);
      const ok = p.locator('.hand .tile.ok').first();
      if (await ok.count()) { await ok.click(); await snap(1, 200); const alt = p.locator('.bottom .btn.alt.small').first(); if (await alt.count()) await alt.click(); }
      await snap(3, 300);
    }
    await caption(3); await snap(5, 300);
  }

  if (game === 'tawla') {
    await caption(0); await waitMyTurn(); await snap(3);
    await p.locator('#roll-btn').click({ force: true }); await snap(6, 120);
    await caption(1);
    for (let k = 0; k < 2; k++) {
      const v = await view();
      if (!v || v.turn !== v.you || !v.tawla.steps.length) break;
      const [from, to] = v.tawla.steps[0];
      const a = await spot({ kind: 'point', p: from }), b = await spot({ kind: 'point', p: to });
      if (!a || !b) break;
      await drag(a, b, 5); await snap(3, 200);
    }
    await caption(2);
    // the computer's turn, then roll and play again
    if (await waitMyTurn()) {
      await snap(2, 200);
      if (await p.locator('#roll-btn').count()) { await p.locator('#roll-btn').click({ force: true }); await snap(4, 120); }
      await caption(3);
      for (let k = 0; k < 2; k++) {
        const v = await view();
        if (!v || v.turn !== v.you || !v.tawla.steps.length) break;
        const [from, to] = v.tawla.steps[0];
        const a = await spot({ kind: 'point', p: from }), b = await spot({ kind: 'point', p: to });
        if (!a || !b) break;
        await drag(a, b, 4); await snap(2, 200);
      }
    } else await caption(3);
    await snap(4, 300);
  }

  if (game === 'chess') {
    await caption(0); await waitMyTurn(); await snap(4);
    const pref = [[12, 28], [51, 35], [11, 27], [52, 36]];
    const v = await view();
    const legal = v.chess.legal;
    const mv = pref.find(([f, t]) => legal.some(m => m[0] === f && m[1] === t)) ?? legal[0];
    await drag(await spot({ kind: 'square', sq: mv[0] }), await spot({ kind: 'square', sq: mv[1] }), 5);
    await snap(3);
    await caption(1); await waitMyTurn(); await snap(5, 300);
    await caption(2);
    const v2 = await view();
    const knight = [[6, 21], [62, 45], [1, 18], [57, 42]].find(([f, t]) => v2.chess.legal.some(m => m[0] === f && m[1] === t)) ?? v2.chess.legal[0];
    const a = await spot({ kind: 'square', sq: knight[0] });
    await p.mouse.click(a.x, a.y); await snap(5, 300);
    const b = await spot({ kind: 'square', sq: knight[1] });
    await p.mouse.click(b.x, b.y); await snap(3);
    await caption(3); await waitMyTurn(); await snap(6, 300);
  }

  if (game === 'ludo') {
    await caption(0);
    let outShown = false, turns = 0;
    while (turns < 14) {
      if (!(await waitMyTurn(70_000))) break;
      const v = await view();
      if (v.ludo.die === null && await p.locator('#roll-btn').count()) {
        await p.locator('#roll-btn').click({ force: true }); turns++;
        await snap(4, 150);
      }
      const v2 = await view();
      if (v2 && v2.turn === v2.you && v2.ludo.movable.length) {
        if (!outShown) await caption(1);
        const s = await spot({ kind: 'pawn', i: v2.ludo.movable[0] });
        if (s) { await p.mouse.click(s.x, s.y); await snap(4, 200); }
        if (outShown && turns > 5) break;
        outShown = true;
      } else await p.waitForTimeout(500);
    }
    await caption(2); await snap(6, 400);
    await caption(3); await snap(5, 400);
  }

  await ctx.close();
  // frames → GIF: 280 px wide, 4 frames a second, an optimised palette
  const out = path.join(outDir, `${game}-${lang}.gif`);
  const filters = 'fps=4,scale=280:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=96:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle';
  execFileSync('ffmpeg', ['-loglevel', 'error', '-y', '-framerate', '4', '-i', path.join(frames, '%04d.png'), '-vf', filters, '-loop', '0', out]);
  fs.rmSync(frames, { recursive: true, force: true });
  console.log(`${path.relative(root, out)}  ${n} frames  ${(fs.statSync(out).size / 1024).toFixed(0)} KB`);
}
