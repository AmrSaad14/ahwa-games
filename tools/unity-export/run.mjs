// Exports the café and the six characters (standing and seated) as .glb files into unity/Assets/Resources/Art.
//   THREE_PATH=…/three.module.js EXPORTER=…/GLTFExporter.js node tools/unity-export/run.mjs
import { build } from 'esbuild';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), '../..');
const out = path.join(root, 'unity/Assets/Resources/Art'); fs.mkdirSync(out, { recursive: true });
const stub = path.join(root, 'tests/stubs/capacitor.ts');
await build({
  entryPoints: [path.join(root, 'tools/unity-export/entry.ts')], bundle: true, format: 'esm', outfile: path.join(root, 'client/dist/assets/export.js'), logLevel: 'warning',
  alias: { three: process.env.THREE_PATH, GLTFExporter: process.env.EXPORTER, '@capacitor/core': stub, '@capacitor/app': stub, '@capacitor/share': stub, '@capacitor/haptics': stub, '@capacitor/browser': stub, '@capacitor-community/admob': stub },
  define: { __SERVER_URL__: '""', __DEMO__: 'false', __ADS__: '{"android":{},"ios":{}}' },
});
const html = fs.readFileSync(path.join(root, 'client/dist/index.html'), 'utf8').replace(/<script type="module" src="[^"]+"><\/script>/, '<script type="module" src="/assets/export.js"></script>');
fs.writeFileSync(path.join(root, 'client/dist/export.html'), html);
const PORT = 8231;
const server = spawn(process.execPath, ['dist/server.cjs'], { cwd: root, env: { ...process.env, PORT: String(PORT), HOST: '127.0.0.1', DB_PATH: '/tmp/unity-export.db', STATIC_DIR: 'client/dist' }, stdio: 'ignore' });
await new Promise(r => setTimeout(r, 800));
const b = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const p = await (await b.newContext({ viewport: { width: 900, height: 500 } })).newPage();
p.on('pageerror', e => console.log('page error:', e.message));
await p.route('https://fonts.googleapis.com/**', r => r.fulfill({ contentType: 'text/css', body: '' }));
await p.exposeFunction('saveGlb', (name, b64) => { fs.writeFileSync(path.join(out, name + '.glb'), Buffer.from(b64, 'base64')); console.log('saved', name, (b64.length * 0.75 / 1024 | 0) + ' KB'); });
await p.goto(`http://127.0.0.1:${PORT}/export.html?promo`);
await p.waitForSelector('.table-tag', { state: 'attached', timeout: 170000 });
await p.waitForTimeout(2000);
await p.evaluate(async () => {
  const { THREE, GLTFExporter, makePerson, PEOPLE } = window.__export;
  const ex = new GLTFExporter();
  const save = async (name, obj) => {
    const buf = await ex.parseAsync(obj, { binary: true, onlyVisible: true, maxTextureSize: 1024 });
    let s = ''; const u = new Uint8Array(buf); for (let i = 0; i < u.length; i += 0x8000) s += String.fromCharCode.apply(null, u.subarray(i, i + 0x8000));
    await window.saveGlb(name, btoa(s));
  };
  const ts = window.__ahwa.scene(), scene = ts.scene;
  // the café: the room and the four game tables (not the people, the tap boxes or the ring)
  const cafe = new THREE.Group(); cafe.name = 'Cafe';
  const room = scene.children.find(o => o.userData.room);
  const hide = []; ts.lobbyHits.forEach(h => { hide.push(h); h.visible = false; }); ts.lobbyRing.visible = false;
  const roomCopy = room.clone(true), lobbyCopy = ts.lobbyG.clone(true);
  hide.forEach(h => { h.visible = true; });
  // no lights in the file (Unity lights the café itself)
  for (const c of [roomCopy, lobbyCopy]) { const lights = []; c.traverse(o => { if (o.isLight) lights.push(o); }); lights.forEach(l => l.parent.remove(l)); }
  roomCopy.name = 'Room'; lobbyCopy.name = 'Tables'; cafe.add(roomCopy, lobbyCopy);
  const SPOTS = { domino: [-8, -5], tawla: [8, -5], chess: [-8, 6.5], ludo: [8, 6.5] };
  for (const c of lobbyCopy.children) {
    for (const [g, [x, z]] of Object.entries(SPOTS)) {
      if (Math.abs(c.position.x - x) > 0.01 || Math.abs(c.position.z - z) > 0.01) continue;
      const chairs = c.children.filter(k => Math.abs(k.position.x) > 4 && Math.abs(k.position.z) < 0.01);
      if (chairs.length === 2 && !c.name) { c.name = 'Table_' + g; chairs.forEach((k, n) => { k.name = 'Seat_' + g + '_' + n; }); }
      else if (!c.name) c.name = 'Board_' + g;
    }
  }
  // drop invisible tap boxes left in the copy
  const gone = []; cafe.traverse(o => { if (o.isMesh && o.material && o.material.visible === false) gone.push(o); }); gone.forEach(o => o.parent.remove(o));
  await save('cafe', cafe);
  // the six regulars, each standing (walking rig) and seated
  const names = ['AmFathi', 'CaptainSami', 'Ziko', 'Mona', 'Nada', 'HajjMahmoud'];
  for (let i = 0; i < PEOPLE.length; i++) for (const standing of [true, false]) {
    const per = makePerson(PEOPLE[i], 'u' + i + standing, standing);
    per.group.name = names[i]; per.piv.name = 'Torso'; per.head.name = 'Head';
    per.arms.forEach((a, k) => { a.name = 'Shoulder' + (k ? 'R' : 'L'); }); per.elbows.forEach((a, k) => { a.name = 'Elbow' + (k ? 'R' : 'L'); });
    per.hands.forEach((a, k) => { a.name = 'Hand' + (k ? 'R' : 'L'); });
    (per.hips || []).forEach((a, k) => { a.name = 'Hip' + (k ? 'R' : 'L'); }); (per.knees || []).forEach((a, k) => { a.name = 'Knee' + (k ? 'R' : 'L'); });
    (per.eyes || []).forEach((a, k) => { a.name = 'Eye' + (k ? 'R' : 'L'); }); (per.lids || []).forEach((a, k) => { a.name = 'Lid' + (k ? 'R' : 'L'); });
    per.group.children.forEach(c => { if (!c.name && c.type === 'Group') c.name = 'Legs'; });
    await save(names[i] + (standing ? '_Stand' : '_Sit'), per.group);
  }
});
await b.close(); server.kill();
console.log('done');
