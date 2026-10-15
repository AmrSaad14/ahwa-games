// Bundles the game client into client/dist (served by the game server and packaged by Capacitor).
//   SERVER_URL=https://domino.example.com node scripts/build-client.mjs   → store app build that talks to your server
//   node scripts/build-client.mjs --watch                                 → rebuild on change during development
import { build, context } from 'esbuild';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), '..');
const src = path.join(root, 'client');
const out = path.join(src, 'dist');
const watch = process.argv.includes('--watch');

// Test-only: build without the real three.js / Capacitor packages (used by the offline UI test).
const stubs = process.env.CLIENT_STUBS === '1' ? {
  three: process.env.THREE_PATH || path.join(root, 'tests/stubs/three.ts'),   // THREE_PATH: a local three.module.js for real rendering
  '@capacitor/core': path.join(root, 'tests/stubs/capacitor.ts'),
  '@capacitor/app': path.join(root, 'tests/stubs/capacitor.ts'),
  '@capacitor/share': path.join(root, 'tests/stubs/capacitor.ts'),
  '@capacitor/haptics': path.join(root, 'tests/stubs/capacitor.ts'),
} : undefined;

const options = {
  entryPoints: { app: path.join(src, 'src/main.ts') },
  bundle: true,
  minify: !watch,
  sourcemap: watch ? 'inline' : false,
  format: 'esm',
  target: ['es2020', 'safari15', 'chrome90'],
  outdir: path.join(out, 'assets'),
  entryNames: '[name]-[hash]',
  metafile: true,
  alias: stubs,
  define: { __SERVER_URL__: JSON.stringify(process.env.SERVER_URL || ''), __DEMO__: JSON.stringify(process.env.DEMO === '1') },
  logLevel: 'info',
  plugins: [{
    name: 'write-html',
    setup(b) {
      b.onEnd(result => {
        if (result.errors.length || !result.metafile) return;
        const files = Object.keys(result.metafile.outputs).map(f => '/' + path.relative(out, path.resolve(root, f)).split(path.sep).join('/'));
        const js = files.find(f => f.endsWith('.js')), css = files.find(f => f.endsWith('.css'));
        // copy static files
        fs.cpSync(path.join(src, 'public'), out, { recursive: true });
        let html = fs.readFileSync(path.join(src, 'index.html'), 'utf8');
        html = html.replace('<!--CSS-->', css ? `<link rel="stylesheet" href="${css}">` : '')
                   .replace('<!--JS-->', `<script type="module" src="${js}"></script>`);
        fs.writeFileSync(path.join(out, 'index.html'), html);
        // offline cache for the web version (practice mode works without a connection)
        const version = crypto.createHash('sha1').update(files.join()).digest('hex').slice(0, 10);
        const precache = ['/', '/index.html', '/manifest.webmanifest', '/icon.svg', '/icon-192.png', ...files];
        fs.writeFileSync(path.join(out, 'sw.js'), `const V='domino-${version}',P=${JSON.stringify(precache)};
self.addEventListener('install',e=>{e.waitUntil(caches.open(V).then(c=>c.addAll(P)).then(()=>self.skipWaiting()))});
self.addEventListener('activate',e=>{e.waitUntil(caches.keys().then(k=>Promise.all(k.filter(x=>x!==V).map(x=>caches.delete(x)))).then(()=>self.clients.claim()))});
self.addEventListener('fetch',e=>{const u=new URL(e.request.url);if(e.request.method!=='GET'||u.origin!==location.origin||u.pathname.startsWith('/api')||u.pathname==='/ws')return;
e.respondWith(caches.match(e.request).then(r=>r||fetch(e.request).catch(()=>caches.match('/index.html'))))});
`);
        // remove stale bundles from earlier builds
        const keep = new Set(files.map(f => path.basename(f)));
        for (const f of fs.readdirSync(path.join(out, 'assets'))) if (!keep.has(f)) fs.rmSync(path.join(out, 'assets', f));
        console.log(`client built → ${path.relative(root, out)} (${files.join(', ')})`);
      });
    },
  }],
};

fs.mkdirSync(path.join(out, 'assets'), { recursive: true });
if (watch) { const ctx = await context(options); await ctx.watch(); }
else await build(options);
