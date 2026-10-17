// Kings Battle icon: a gold chess king over crossed swords, on royal crimson.
import fs from 'node:fs';
import { chromium } from '/opt/npm-tools/node_modules/playwright/index.mjs';
const out = '/tmp/claude-0/icon';

function art({ rounded = false, scale = 1, layer = 'all' } = {}) {
  // scale < 1 shrinks the emblem toward the centre (maskable / adaptive safe zone)
  const t = `translate(512 540) scale(${scale}) translate(-512 -540)`;
  const sword = (deg) => `
    <g transform="rotate(${deg} 512 430) translate(0 -40)">
      <path d="M512 70 L540 150 L540 700 L484 700 L484 150 Z" fill="url(#steel)" stroke="#2a1a14" stroke-width="10" stroke-linejoin="round"/>
      <path d="M512 92 L512 690" stroke="#ffffff" stroke-opacity=".55" stroke-width="6"/>
      <rect x="392" y="694" width="240" height="40" rx="20" fill="url(#gold)" stroke="#2a1a14" stroke-width="10"/>
      <rect x="490" y="732" width="44" height="130" rx="14" fill="#4a1d14" stroke="#2a1a14" stroke-width="10"/>
      <path d="M492 760 h40 M492 792 h40 M492 824 h40" stroke="#7a3a24" stroke-width="8"/>
      <circle cx="512" cy="884" r="32" fill="url(#gold)" stroke="#2a1a14" stroke-width="10"/>
    </g>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024" width="1024" height="1024">
  <defs>
    <radialGradient id="bg" cx="50%" cy="42%" r="75%">
      <stop offset="0" stop-color="#9a1b2a"/><stop offset=".55" stop-color="#5a0e1a"/><stop offset="1" stop-color="#1c0609"/>
    </radialGradient>
    <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#fff4c2"/><stop offset=".35" stop-color="#f3cd62"/><stop offset=".7" stop-color="#c38a22"/><stop offset="1" stop-color="#7e4f10"/>
    </linearGradient>
    <linearGradient id="goldSide" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#8a5a14"/><stop offset=".3" stop-color="#f6d77a"/><stop offset=".55" stop-color="#ffe9a8"/><stop offset=".8" stop-color="#d49a2e"/><stop offset="1" stop-color="#7e4f10"/>
    </linearGradient>
    <linearGradient id="steel" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#6f7c88"/><stop offset=".45" stop-color="#f4f7fa"/><stop offset=".55" stop-color="#c9d2da"/><stop offset="1" stop-color="#55606b"/>
    </linearGradient>
    <radialGradient id="glow" cx="50%" cy="45%" r="50%">
      <stop offset="0" stop-color="#ffcf6e" stop-opacity=".55"/><stop offset="1" stop-color="#ffcf6e" stop-opacity="0"/>
    </radialGradient>
    <filter id="shadow" x="-20%" y="-20%" width="140%" height="140%">
      <feDropShadow dx="0" dy="18" stdDeviation="18" flood-color="#000" flood-opacity=".55"/>
    </filter>
    <clipPath id="clip"><rect width="1024" height="1024" rx="${rounded ? 224 : 0}"/></clipPath>
  </defs>
  <g clip-path="url(#clip)">
    ${layer === 'fg' ? '' : '<rect width="1024" height="1024" fill="url(#bg)"/>'}
    <g opacity="${layer === 'fg' ? 0 : .14}" fill="#ffd27a">
      ${Array.from({ length: 16 }, (_, i) => `<path d="M512 470 L${512 + 900 * Math.cos((i * Math.PI) / 8 - 0.09)} ${470 + 900 * Math.sin((i * Math.PI) / 8 - 0.09)} L${512 + 900 * Math.cos((i * Math.PI) / 8 + 0.09)} ${470 + 900 * Math.sin((i * Math.PI) / 8 + 0.09)} Z"/>`).join('')}
    </g>
    ${layer === 'fg' ? '' : '<circle cx="512" cy="470" r="380" fill="url(#glow)"/>'}
    <g transform="${t}" opacity="${layer === 'bg' ? 0 : 1}">
      <g filter="url(#shadow)">${sword(-44)}${sword(44)}</g>
      <g filter="url(#shadow)" stroke="#2a1a14" stroke-width="12" stroke-linejoin="round">
        <!-- cross -->
        <rect x="488" y="150" width="48" height="170" rx="10" fill="url(#goldSide)"/>
        <rect x="442" y="196" width="140" height="46" rx="10" fill="url(#goldSide)"/>
        <!-- crown band and head -->
        <path d="M420 470 C 392 400, 420 340, 512 330 C 604 340, 632 400, 604 470 Z" fill="url(#goldSide)"/>
        <rect x="432" y="306" width="160" height="40" rx="14" fill="url(#gold)"/>
        <!-- collar -->
        <rect x="398" y="462" width="228" height="46" rx="20" fill="url(#gold)"/>
        <!-- body -->
        <path d="M446 506 L578 506 C 590 600, 622 680, 664 748 L360 748 C 402 680, 434 600, 446 506 Z" fill="url(#goldSide)"/>
        <!-- base -->
        <rect x="340" y="740" width="344" height="52" rx="18" fill="url(#gold)"/>
        <rect x="300" y="784" width="424" height="70" rx="26" fill="url(#goldSide)"/>
      </g>
      <!-- highlights -->
      <path d="M470 360 C 452 390, 450 420, 458 452" stroke="#fffbe6" stroke-opacity=".7" stroke-width="12" fill="none" stroke-linecap="round"/>
      <path d="M470 530 C 462 600, 440 660, 410 720" stroke="#fffbe6" stroke-opacity=".5" stroke-width="12" fill="none" stroke-linecap="round"/>
      <circle cx="512" cy="219" r="10" fill="#c8102e" stroke="#2a1a14" stroke-width="5"/>
      <circle cx="512" cy="485" r="12" fill="#c8102e" stroke="#2a1a14" stroke-width="5"/>
    </g>
    <rect width="1024" height="1024" rx="${rounded ? 224 : 0}" fill="none" stroke="#f0c75a" stroke-opacity=".55" stroke-width="${rounded ? 18 : 0}"/>
  </g>
</svg>`;
}

const splash = (iconSvg) => `<!doctype html><html><head><style>
@font-face { font-family: Lalezar; src: url(data:font/ttf;base64,${fs.readFileSync('/tmp/claude-0/promo/gf/ofl/lalezar/Lalezar-Regular.ttf').toString('base64')}); }
html,body{margin:0;width:2732px;height:2732px;background:radial-gradient(circle at 50% 42%,#5a0e1a 0,#2a0710 45%,#120306 100%);}
.c{position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:40px}
img{width:760px;height:760px;border-radius:168px;box-shadow:0 40px 120px rgba(0,0,0,.6)}
h1{margin:30px 0 0;font:150px Lalezar;letter-spacing:12px;color:#f3cd62;text-shadow:0 8px 0 #6b3f0c}
h2{margin:0;font:120px Lalezar;color:#f6e3b0}
</style></head><body><div class="c"><img src="data:image/svg+xml;base64,${Buffer.from(iconSvg).toString('base64')}"><h1>KINGS BATTLE</h1><h2>معركة الملوك</h2></div></body></html>`;

const full = art(), round = art({ rounded: true }), safe = art({ scale: 0.78 });
fs.writeFileSync(`${out}/icon.svg`, round);
fs.writeFileSync(`${out}/icon-full.svg`, full);
fs.writeFileSync(`${out}/icon-safe.svg`, safe);
const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1024, height: 1024 } });
const shot = async (svg, file, size) => {
  await p.setViewportSize({ width: 1024, height: 1024 });
  await p.setContent(`<html><body style="margin:0;background:transparent">${svg}</body></html>`);
  await p.screenshot({ path: `${out}/${file}`, omitBackground: true, clip: { x: 0, y: 0, width: 1024, height: 1024 } });
};
await shot(art({ layer: 'fg', scale: 0.64 }), 'icon-foreground.png'); await shot(art({ layer: 'bg' }), 'icon-background.png');
await shot(full, 'icon-1024.png'); await shot(round, 'icon-round-1024.png'); await shot(safe, 'icon-safe-1024.png');
await p.setViewportSize({ width: 2732, height: 2732 });
await p.setContent(splash(round)); await p.evaluate(() => document.fonts.ready); await p.waitForTimeout(500);
await p.screenshot({ path: `${out}/splash.png` });
await b.close();
console.log('done');
