import * as THREE from 'three';

/* The coffeehouse (قهوة بلدي) around the game table: textures, furniture, shisha, walls and lights.
   Everything is procedural (canvas textures + simple geometry), so there are no image or model files to ship. */

export const FLOOR_Y = -3.6;

type Draw = (g: CanvasRenderingContext2D, w: number, h: number) => void;
/**
 * Texture detail for the game boards and tiles: drawn at 2x on capable devices (sharper print, pips and
 * inlay up close), 1x on low-memory phones or once the device has stepped its graphics down.
 */
export const TEX_HI: number = (() => {
  try { const s = localStorage.getItem('ahwa.gfx'); if (s === '0' || s === '1') return 1; } catch { /* private mode */ }
  const mem = (navigator as { deviceMemory?: number }).deviceMemory;
  return mem !== undefined && mem <= 4 ? 1 : 2;
})();
/** A canvas texture drawn in w×h units; `scale` draws it at that many pixels per unit (capped at 4096 px). */
export function canvasTex(w: number, h: number, draw: Draw, repeat?: [number, number], scale = 1) {
  const k = Math.max(1, Math.min(scale, 4096 / Math.max(w, h)));
  const c = document.createElement('canvas'); c.width = Math.round(w * k); c.height = Math.round(h * k);
  const g = c.getContext('2d')!;
  if (k !== 1) g.scale(c.width / w, c.height / h);
  draw(g, w, h);
  const tx = new THREE.CanvasTexture(c);
  tx.colorSpace = THREE.SRGBColorSpace;
  tx.anisotropy = 16;   // three.js clamps this to what the GPU supports
  if (repeat) { tx.wrapS = tx.wrapT = THREE.RepeatWrapping; tx.repeat.set(repeat[0], repeat[1]); }
  return tx;
}

const rnd = (a: number, b: number) => a + Math.random() * (b - a);
function grime(g: CanvasRenderingContext2D, w: number, h: number, n: number, color: string, maxR: number) {
  for (let i = 0; i < n; i++) {
    const x = Math.random() * w, y = Math.random() * h, r = rnd(maxR * 0.2, maxR);
    const gr = g.createRadialGradient(x, y, 0, x, y, r);
    gr.addColorStop(0, color); gr.addColorStop(1, 'rgba(0,0,0,0)');
    g.fillStyle = gr; g.fillRect(x - r, y - r, r * 2, r * 2);
  }
}

/** Patterned cement floor tile (بلاط), the classic ahwa floor. One texture = one tile. */
export function floorTexture(repeat: number) {
  return canvasTex(256, 256, (g, w) => {
    g.fillStyle = '#d6c6a6'; g.fillRect(0, 0, w, w);
    const c = w / 2;
    // corner quarter-circles form rings across four tiles
    g.fillStyle = '#7c2f24';
    for (const [x, y] of [[0, 0], [w, 0], [0, w], [w, w]]) { g.beginPath(); g.arc(x, y, w * 0.34, 0, Math.PI * 2); g.fill(); }
    g.fillStyle = '#d6c6a6';
    for (const [x, y] of [[0, 0], [w, 0], [0, w], [w, w]]) { g.beginPath(); g.arc(x, y, w * 0.25, 0, Math.PI * 2); g.fill(); }
    g.fillStyle = '#2a2420';
    for (const [x, y] of [[0, 0], [w, 0], [0, w], [w, w]]) { g.beginPath(); g.arc(x, y, w * 0.13, 0, Math.PI * 2); g.fill(); }
    // centre flower
    g.fillStyle = '#2f5d52';
    for (let i = 0; i < 4; i++) {
      g.save(); g.translate(c, c); g.rotate((i * Math.PI) / 2);
      g.beginPath(); g.ellipse(0, -w * 0.13, w * 0.07, w * 0.13, 0, 0, Math.PI * 2); g.fill(); g.restore();
    }
    g.fillStyle = '#c99a3f'; g.beginPath(); g.arc(c, c, w * 0.05, 0, Math.PI * 2); g.fill();
    // grout and wear
    g.strokeStyle = 'rgba(40,30,20,.55)'; g.lineWidth = 3; g.strokeRect(1.5, 1.5, w - 3, w - 3);
    grime(g, w, w, 40, 'rgba(60,40,20,.10)', 60);
    grime(g, w, w, 25, 'rgba(255,240,210,.08)', 40);
  }, [repeat, repeat]);
}

/** Wall: glossy green oil paint on the lower half, worn cream plaster above. */
export function wallTexture(repeatX: number) {
  return canvasTex(512, 512, (g, w, h) => {
    const split = h * 0.58;
    const up = g.createLinearGradient(0, 0, 0, split);
    up.addColorStop(0, '#b9a37c'); up.addColorStop(1, '#d4c19b');
    g.fillStyle = up; g.fillRect(0, 0, w, split);
    grime(g, w, split, 60, 'rgba(90,65,35,.12)', 90);
    const low = g.createLinearGradient(0, split, 0, h);
    low.addColorStop(0, '#2f6c5c'); low.addColorStop(1, '#244f45');
    g.fillStyle = low; g.fillRect(0, split, w, h - split);
    g.fillStyle = '#1b3a33'; g.fillRect(0, split - 6, w, 10);
    g.fillStyle = '#7f2d22'; g.fillRect(0, split + 4, w, 4);
    grime(g, w, h, 30, 'rgba(10,20,15,.18)', 50);
    for (let i = 0; i < 40; i++) { g.fillStyle = 'rgba(220,210,180,.25)'; g.fillRect(Math.random() * w, split + Math.random() * (h - split), rnd(2, 10), 1); }
  }, [repeatX, 1]);
}

export function marbleTexture() {
  return canvasTex(512, 512, (g, w, h) => {
    g.fillStyle = '#e9e4da'; g.fillRect(0, 0, w, h);
    grime(g, w, h, 50, 'rgba(160,150,135,.10)', 120);
    for (let i = 0; i < 14; i++) {
      g.strokeStyle = `rgba(110,100,90,${rnd(0.08, 0.3)})`; g.lineWidth = rnd(0.6, 2.4);
      g.beginPath(); let x = Math.random() * w, y = Math.random() * h; g.moveTo(x, y);
      for (let k = 0; k < 8; k++) { x += rnd(-80, 80); y += rnd(-80, 80); g.quadraticCurveTo(x + rnd(-40, 40), y + rnd(-40, 40), x, y); }
      g.stroke();
    }
  });
}

export function woodTexture() {
  return canvasTex(256, 256, (g, w, h) => {
    g.fillStyle = '#5a3820'; g.fillRect(0, 0, w, h);
    for (let i = 0; i < 70; i++) {
      const y = Math.random() * h;
      g.strokeStyle = `rgba(${Math.random() < 0.5 ? '30,16,8' : '120,80,45'},${rnd(0.1, 0.3)})`;
      g.lineWidth = rnd(1, 3); g.beginPath(); g.moveTo(0, y);
      for (let x = 0; x <= w; x += 24) g.lineTo(x, y + Math.sin(x / 50 + i) * 3);
      g.stroke();
    }
  }, [1, 1]);
}

/** Dark walnut tabletop with a soft sheen and old tea-glass rings: ivory tiles read clearly on it. */
export function darkTopTexture() {
  return canvasTex(1024, 1024, (g, w, h) => {
    const base = g.createLinearGradient(0, 0, w, h);
    base.addColorStop(0, '#2a180d'); base.addColorStop(0.5, '#311d10'); base.addColorStop(1, '#24150b');
    g.fillStyle = base; g.fillRect(0, 0, w, h);
    for (let i = 0; i < 160; i++) {
      const y = Math.random() * h;
      g.strokeStyle = `rgba(${Math.random() < 0.6 ? '15,8,3' : '90,58,32'},${rnd(0.12, 0.35)})`;
      g.lineWidth = rnd(1, 4); g.beginPath(); g.moveTo(0, y);
      for (let x = 0; x <= w; x += 40) g.lineTo(x, y + Math.sin(x / 90 + i * 0.7) * 6);
      g.stroke();
    }
    for (let i = 0; i < 5; i++) {
      const x = rnd(120, w - 120), y = rnd(120, h - 120), r = rnd(34, 44);
      g.strokeStyle = 'rgba(120,80,45,.18)'; g.lineWidth = 4; g.beginPath(); g.arc(x, y, r, rnd(0, 1), rnd(4, 6.2)); g.stroke();
    }
    grime(g, w, h, 30, 'rgba(0,0,0,.18)', 160);
  });
}

const mats = {
  iron: new THREE.MeshStandardMaterial({ color: 0x1d1b19, roughness: 0.55, metalness: 0.6 }),
  brass: new THREE.MeshStandardMaterial({ color: 0xc89b45, roughness: 0.3, metalness: 0.9 }),
  copper: new THREE.MeshStandardMaterial({ color: 0xb06a3c, roughness: 0.35, metalness: 0.85 }),
  glass: new THREE.MeshStandardMaterial({ color: 0xf2f5f2, roughness: 0.05, metalness: 0, transparent: true, opacity: 0.28 }),
  tea: new THREE.MeshStandardMaterial({ color: 0x8a2b0e, roughness: 0.2, transparent: true, opacity: 0.85 }),
  coal: new THREE.MeshStandardMaterial({ color: 0xff6a1a, emissive: 0xff4a10, emissiveIntensity: 2.2 }),
  clay: new THREE.MeshStandardMaterial({ color: 0x8a4a2a, roughness: 0.9 }),
};
let woodMat: THREE.MeshStandardMaterial | null = null;
export const wood = () => (woodMat ??= new THREE.MeshStandardMaterial({ map: woodTexture(), roughness: 0.7 }));
let marbleMat: THREE.MeshStandardMaterial | null = null;
const marble = () => (marbleMat ??= new THREE.MeshStandardMaterial({ map: marbleTexture(), roughness: 0.22, metalness: 0.02 }));
let topMat: THREE.MeshStandardMaterial | null = null;
const darkTop = () => (topMat ??= new THREE.MeshStandardMaterial({ map: darkTopTexture(), roughness: 0.45, metalness: 0.05 }));

function mesh(geo: THREE.BufferGeometry, mat: THREE.Material | THREE.Material[], x = 0, y = 0, z = 0, parent?: THREE.Object3D) {
  const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); parent?.add(m); return m;
}
const shadows = (o: THREE.Object3D, cast = true, receive = true) => { o.traverse(c => { if ((c as THREE.Mesh).isMesh) { c.castShadow = cast; c.receiveShadow = receive; } }); return o; };

/** Small square ahwa table: dark walnut top, wooden frame, cast iron legs. Top surface at y = 0. */
export function makeTable(half: number) {
  const g = new THREE.Group();
  mesh(new THREE.BoxGeometry(half * 2, 0.3, half * 2), darkTop(), 0, -0.15, 0, g);
  const w = new THREE.MeshStandardMaterial({ color: 0x1a0f08, roughness: 0.4, metalness: 0.1 }), t = 0.35;
  mesh(new THREE.BoxGeometry(half * 2 + t * 2, 0.42, t), w, 0, -0.17, half + t / 2, g);
  mesh(new THREE.BoxGeometry(half * 2 + t * 2, 0.42, t), w, 0, -0.17, -half - t / 2, g);
  mesh(new THREE.BoxGeometry(t, 0.42, half * 2), w, half + t / 2, -0.17, 0, g);
  mesh(new THREE.BoxGeometry(t, 0.42, half * 2), w, -half - t / 2, -0.17, 0, g);
  const legH = -FLOOR_Y - 0.4;
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) mesh(new THREE.CylinderGeometry(0.12, 0.16, legH, 10), mats.iron, sx * (half - 0.4), -0.4 - legH / 2, sz * (half - 0.4), g);
  const bar = mesh(new THREE.TorusGeometry(half * 0.85, 0.05, 6, 32), mats.iron, 0, FLOOR_Y + 1.2, 0, g); bar.rotation.x = Math.PI / 2;
  return shadows(g);
}

/** Wooden ahwa chair with a woven seat. Seat top at y = -1.9 when placed at y = 0. */
export function makeChair() {
  const g = new THREE.Group(), w = wood();
  mesh(new THREE.BoxGeometry(2.4, 0.22, 2.2), new THREE.MeshStandardMaterial({ color: 0x9a7a4a, roughness: 0.95 }), 0, -2.0, -0.3, g);
  mesh(new THREE.BoxGeometry(2.6, 0.18, 2.4), w, 0, -2.15, -0.3, g);
  for (const [x, z] of [[-1.1, 0.75], [1.1, 0.75], [-1.1, -1.35], [1.1, -1.35]]) mesh(new THREE.BoxGeometry(0.18, -FLOOR_Y - 2.15, 0.18), w, x, (FLOOR_Y - 2.15) / 2, z, g);
  for (const x of [-1.1, 1.1]) mesh(new THREE.BoxGeometry(0.18, 3.2, 0.18), w, x, -0.6, -1.4, g);
  for (const y of [-0.1, 0.6]) mesh(new THREE.BoxGeometry(2.3, 0.3, 0.12), w, 0, y, -1.4, g);
  return shadows(g);
}

/** A glass of red tea on a small saucer. */
export function makeTeaGlass() {
  const g = new THREE.Group();
  mesh(new THREE.CylinderGeometry(0.42, 0.36, 0.06, 20), new THREE.MeshStandardMaterial({ color: 0xf1ece2, roughness: 0.35 }), 0, 0.03, 0, g);
  mesh(new THREE.CylinderGeometry(0.2, 0.16, 0.62, 16, 1, true), mats.glass, 0, 0.37, 0, g);
  mesh(new THREE.CylinderGeometry(0.185, 0.155, 0.45, 16), mats.tea, 0, 0.29, 0, g);
  const spoon = mesh(new THREE.CylinderGeometry(0.015, 0.015, 0.8, 6), mats.brass, 0.08, 0.5, 0, g); spoon.rotation.z = -0.25;
  return g;
}

export interface SmokeSource { at: THREE.Vector3 }

/** Shisha: glass base, brass stem, clay head with glowing coals and a hose. Base sits on the floor. */
export function makeShisha(hoseTo?: THREE.Vector3): { group: THREE.Group; smoke: THREE.Vector3 } {
  const g = new THREE.Group();
  const profile = [[0, 0], [0.55, 0.02], [0.85, 0.35], [0.95, 0.8], [0.8, 1.25], [0.35, 1.6], [0.22, 1.85], [0.22, 2.0]].map(([x, y]) => new THREE.Vector2(x, y));
  const vase = new THREE.Mesh(new THREE.LatheGeometry(profile, 28), new THREE.MeshStandardMaterial({ color: 0x2f8a6a, roughness: 0.1, transparent: true, opacity: 0.55 }));
  g.add(vase);
  mesh(new THREE.CylinderGeometry(0.62, 0.62, 0.7, 20), new THREE.MeshStandardMaterial({ color: 0x9ccbb8, roughness: 0.1, transparent: true, opacity: 0.35 }), 0, 0.4, 0, g);
  mesh(new THREE.CylinderGeometry(0.1, 0.12, 2.4, 10), mats.brass, 0, 3.2, 0, g);
  for (const y of [2.3, 3.0, 3.7]) mesh(new THREE.SphereGeometry(0.2, 12, 10), mats.brass, 0, y, 0, g);
  mesh(new THREE.CylinderGeometry(0.75, 0.75, 0.05, 24), mats.brass, 0, 4.35, 0, g);
  mesh(new THREE.CylinderGeometry(0.3, 0.2, 0.45, 14), mats.clay, 0, 4.6, 0, g);
  for (let i = 0; i < 3; i++) { const c = mesh(new THREE.BoxGeometry(0.16, 0.12, 0.16), mats.coal, Math.cos(i * 2.1) * 0.12, 4.86, Math.sin(i * 2.1) * 0.12, g); c.rotation.y = i; }
  if (hoseTo) {
    const start = new THREE.Vector3(0.12, 3.0, 0);
    const end = hoseTo.clone();
    const mid = start.clone().lerp(end, 0.5); mid.y = Math.min(start.y, end.y) - 1.8;
    const curve = new THREE.CatmullRomCurve3([start, new THREE.Vector3(0.9, 2.2, 0.2), mid, end]);
    mesh(new THREE.TubeGeometry(curve, 40, 0.07, 8), new THREE.MeshStandardMaterial({ color: 0x5b2a5e, roughness: 0.6 }), 0, 0, 0, g);
  }
  shadows(g, true, false);
  return { group: g, smoke: new THREE.Vector3(0, 5, 0) };
}

/** Open backgammon (طاولة) board for a neighbouring table. */
export function makeBackgammon() {
  const g = new THREE.Group();
  const tex = canvasTex(512, 256, (c, w, h) => {
    c.fillStyle = '#6b3d1f'; c.fillRect(0, 0, w, h);
    c.fillStyle = '#e9d7ad'; c.fillRect(14, 14, w / 2 - 22, h - 28); c.fillRect(w / 2 + 8, 14, w / 2 - 22, h - 28);
    for (let half = 0; half < 2; half++) for (let i = 0; i < 6; i++) for (const top of [true, false]) {
      const x0 = 14 + half * (w / 2 - 6) + i * ((w / 2 - 22) / 6), bw = (w / 2 - 22) / 6;
      c.fillStyle = (i + (top ? 0 : 1)) % 2 ? '#7a2a1f' : '#244a3e';
      c.beginPath();
      if (top) { c.moveTo(x0, 14); c.lineTo(x0 + bw, 14); c.lineTo(x0 + bw / 2, h * 0.45); }
      else { c.moveTo(x0, h - 14); c.lineTo(x0 + bw, h - 14); c.lineTo(x0 + bw / 2, h * 0.55); }
      c.fill();
    }
  });
  const box = mesh(new THREE.BoxGeometry(3.2, 0.18, 1.6), [wood(), wood(), new THREE.MeshStandardMaterial({ map: tex, roughness: 0.5 }), wood(), wood(), wood()], 0, 0.09, 0, g);
  box.castShadow = true;
  const white = new THREE.MeshStandardMaterial({ color: 0xf0e8d6, roughness: 0.4 }), black = new THREE.MeshStandardMaterial({ color: 0x241a14, roughness: 0.4 });
  for (let i = 0; i < 9; i++) mesh(new THREE.CylinderGeometry(0.11, 0.11, 0.06, 14), i % 2 ? white : black, rnd(-1.4, 1.4), 0.21, rnd(-0.6, 0.6), g);
  return g;
}

function framedPicture(w: number, h: number, draw: Draw, frame = mats.brass) {
  const g = new THREE.Group();
  mesh(new THREE.BoxGeometry(w + 0.3, h + 0.3, 0.12), frame, 0, 0, 0, g);
  const pic = mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshStandardMaterial({ map: canvasTex(256, Math.round(256 * h / w), draw), roughness: 0.6 }), 0, 0, 0.07, g);
  pic.receiveShadow = true;
  return g;
}

/** Builds the room around the table and returns things that animate. */
export function buildAhwa(scene: THREE.Scene) {
  const room = new THREE.Group(); scene.add(room);
  const floor = mesh(new THREE.PlaneGeometry(90, 90), new THREE.MeshStandardMaterial({ map: floorTexture(90 / 1.8), roughness: 0.75 }), 0, FLOOR_Y, 0, room);
  floor.rotation.x = -Math.PI / 2; floor.receiveShadow = true;

  const backZ = -16, sideX = 24, wallH = 22;
  const back = mesh(new THREE.PlaneGeometry(sideX * 2, wallH), new THREE.MeshStandardMaterial({ map: wallTexture(6), roughness: 0.8 }), 0, FLOOR_Y + wallH / 2, backZ, room);
  back.receiveShadow = true;
  for (const s of [-1, 1]) {
    const side = mesh(new THREE.PlaneGeometry(40, wallH), new THREE.MeshStandardMaterial({ map: wallTexture(5), roughness: 0.8 }), s * sideX, FLOOR_Y + wallH / 2, 4, room);
    side.rotation.y = -s * Math.PI / 2;
  }
  // skirting
  mesh(new THREE.BoxGeometry(sideX * 2, 0.5, 0.15), new THREE.MeshStandardMaterial({ color: 0x1d2a25 }), 0, FLOOR_Y + 0.25, backZ + 0.08, room);

  // back wall decoration
  const mirror = new THREE.Group();
  mesh(new THREE.BoxGeometry(6.6, 3.8, 0.2), mats.brass, 0, 0, 0, mirror);
  mesh(new THREE.PlaneGeometry(6, 3.2), new THREE.MeshStandardMaterial({
    map: canvasTex(256, 140, (c, w, h) => { const gr = c.createLinearGradient(0, 0, w, h); gr.addColorStop(0, '#5d6b68'); gr.addColorStop(0.5, '#a3aba3'); gr.addColorStop(1, '#3d4644'); c.fillStyle = gr; c.fillRect(0, 0, w, h); grime(c, w, h, 12, 'rgba(255,255,255,.18)', 40); }),
    roughness: 0.05, metalness: 0.4,
  }), 0, 0, 0.11, mirror);
  mirror.position.set(-1.5, 6.4, backZ + 0.12); room.add(mirror);

  const pyramids: Draw = (c, w, h) => {
    const sky = c.createLinearGradient(0, 0, 0, h); sky.addColorStop(0, '#c9ae7c'); sky.addColorStop(1, '#8a6a40'); c.fillStyle = sky; c.fillRect(0, 0, w, h);
    c.fillStyle = '#5a4126';
    for (const [x, s] of [[0.35, 0.5], [0.62, 0.38], [0.82, 0.22]]) { c.beginPath(); c.moveTo(w * x - w * s * 0.5, h * 0.82); c.lineTo(w * x, h * 0.82 - w * s * 0.45); c.lineTo(w * x + w * s * 0.5, h * 0.82); c.fill(); }
    c.fillStyle = '#4a3420'; c.fillRect(0, h * 0.82, w, h * 0.18);
    grime(c, w, h, 20, 'rgba(60,40,20,.2)', 40);
  };
  const p1 = framedPicture(3, 2.2, pyramids, wood()); p1.position.set(-9.5, 5.6, backZ + 0.1); room.add(p1);
  const nile: Draw = (c, w, h) => {
    c.fillStyle = '#b9a173'; c.fillRect(0, 0, w, h);
    c.fillStyle = '#6f8a86'; c.fillRect(0, h * 0.6, w, h * 0.4);
    c.fillStyle = '#efe4c8'; c.beginPath(); c.moveTo(w * 0.45, h * 0.62); c.lineTo(w * 0.5, h * 0.2); c.lineTo(w * 0.62, h * 0.62); c.fill();
    c.fillStyle = '#3d2c1c'; c.fillRect(w * 0.38, h * 0.62, w * 0.3, h * 0.05);
    grime(c, w, h, 20, 'rgba(60,40,20,.2)', 40);
  };
  const p2 = framedPicture(2.4, 3, nile, wood()); p2.position.set(5.5, 6.2, backZ + 0.1); room.add(p2);

  // wall clock
  const clock = new THREE.Group();
  const face = mesh(new THREE.CylinderGeometry(1, 1, 0.12, 32), new THREE.MeshStandardMaterial({
    map: canvasTex(256, 256, (c, w) => {
      c.fillStyle = '#f1e7cf'; c.fillRect(0, 0, w, w); c.fillStyle = '#2a1d14';
      for (let i = 0; i < 12; i++) { const a = (i / 12) * Math.PI * 2; c.fillRect(w / 2 + Math.sin(a) * 100 - 4, w / 2 - Math.cos(a) * 100 - 10, 8, 20); }
      c.lineWidth = 8; c.strokeStyle = '#2a1d14'; c.beginPath(); c.moveTo(w / 2, w / 2); c.lineTo(w / 2 + 50, w / 2 - 40); c.moveTo(w / 2, w / 2); c.lineTo(w / 2 - 10, w / 2 - 85); c.stroke();
    }),
  }), 0, 0, 0, clock);
  face.rotation.x = Math.PI / 2;
  mesh(new THREE.TorusGeometry(1.02, 0.1, 8, 32), mats.brass, 0, 0, 0.05, clock);
  clock.position.set(9.5, 8.2, backZ + 0.12); room.add(clock);

  // doorway to the street, with daylight
  const door = new THREE.Group();
  mesh(new THREE.PlaneGeometry(4.2, 8), new THREE.MeshBasicMaterial({
    map: canvasTex(128, 256, (c, w, h) => {
      const gr = c.createLinearGradient(0, 0, 0, h); gr.addColorStop(0, '#fff3d6'); gr.addColorStop(0.55, '#f4d9a0'); gr.addColorStop(1, '#a88a5c');
      c.fillStyle = gr; c.fillRect(0, 0, w, h);
      c.fillStyle = 'rgba(120,90,60,.5)'; c.fillRect(0, h * 0.35, w * 0.3, h * 0.65); c.fillRect(w * 0.75, h * 0.3, w * 0.25, h * 0.7);
    }),
  }), 0, 0, 0, door);
  for (const x of [-2.3, 2.3]) mesh(new THREE.BoxGeometry(0.4, 8.4, 0.4), wood(), x, 0, 0.1, door);
  mesh(new THREE.BoxGeometry(5, 0.45, 0.4), wood(), 0, 4.2, 0.1, door);
  door.position.set(16.5, FLOOR_Y + 4, backZ + 0.05); room.add(door);
  const daylight = new THREE.PointLight(0xffe2b0, 40, 26, 1.6); daylight.position.set(16, 2, backZ + 3); room.add(daylight);

  // tea station (النصبة): counter, kettle, glasses
  const counter = new THREE.Group();
  mesh(new THREE.BoxGeometry(7, 3.6, 2.2), new THREE.MeshStandardMaterial({ map: wallTexture(1), roughness: 0.6 }), 0, FLOOR_Y + 1.8, 0, counter);
  mesh(new THREE.BoxGeometry(7.3, 0.25, 2.5), marble(), 0, FLOOR_Y + 3.72, 0, counter);
  const top = FLOOR_Y + 3.85;
  const kettle = mesh(new THREE.SphereGeometry(0.65, 20, 14), mats.copper, -2, top + 0.6, 0, counter); kettle.scale.y = 0.85;
  const spout = mesh(new THREE.CylinderGeometry(0.06, 0.12, 0.9, 8), mats.copper, -1.35, top + 0.75, 0, counter); spout.rotation.z = -0.9;
  mesh(new THREE.CylinderGeometry(0.75, 0.75, 0.3, 20), mats.iron, -2, top + 0.15, 0, counter);
  mesh(new THREE.CylinderGeometry(1.2, 1.2, 0.05, 28), mats.brass, 1, top + 0.03, 0, counter);
  for (let i = 0; i < 6; i++) { const tg = makeTeaGlass(); tg.position.set(0.55 + (i % 3) * 0.45, top, -0.4 + Math.floor(i / 3) * 0.6); counter.add(tg); }
  mesh(new THREE.CylinderGeometry(0.35, 0.35, 0.8, 16), new THREE.MeshStandardMaterial({ color: 0xdedad0, transparent: true, opacity: 0.6 }), 2.8, top + 0.4, 0.2, counter);
  // shelf above with glasses
  mesh(new THREE.BoxGeometry(6, 0.18, 0.9), wood(), 0, FLOOR_Y + 8, -0.6, counter);
  for (let i = 0; i < 9; i++) { const tg = makeTeaGlass(); tg.position.set(-2.6 + i * 0.65, FLOOR_Y + 8.09, -0.6); counter.add(tg); }
  counter.position.set(-15, 0, backZ + 1.4); room.add(shadows(counter));

  // neighbouring tables with shisha, tawla and regulars
  const smoke: THREE.Vector3[] = [];
  const extraTables: { pos: [number, number]; tawla?: boolean; shisha?: boolean }[] = [
    { pos: [-12.5, -8.5], shisha: true }, { pos: [12.5, -9], tawla: true, shisha: true }, { pos: [-19, 3] }, { pos: [19, 2.5], shisha: true },
  ];
  const tables: THREE.Vector3[] = [];
  for (const t of extraTables) {
    const g = new THREE.Group();
    g.add(makeTable(2.4));
    for (const a of [0, Math.PI]) { const ch = makeChair(); ch.position.set(Math.sin(a) * 3.6, 0, Math.cos(a) * 3.6); ch.lookAt(0, 0, 0); g.add(ch); }
    const tg = makeTeaGlass(); tg.position.set(0.9, 0, 0.6); g.add(tg);
    const tg2 = makeTeaGlass(); tg2.position.set(-0.8, 0, -0.9); g.add(tg2);
    if (t.tawla) { const bg = makeBackgammon(); bg.position.set(0, 0, 0); bg.rotation.y = 0.15; g.add(bg); }
    if (t.shisha) {
      const sh = makeShisha(new THREE.Vector3(-2.2, 1.6, 2.6));
      sh.group.position.set(2.8, FLOOR_Y, -1.5); g.add(sh.group);
      const p = new THREE.Vector3(2.8 + t.pos[0], FLOOR_Y + 5, -1.5 + t.pos[1]); smoke.push(p);
    }
    g.position.set(t.pos[0], 0, t.pos[1]); g.rotation.y = rnd(-0.2, 0.2);
    room.add(g); tables.push(g.position.clone());
  }

  // hanging bulbs
  const bulbs: THREE.Vector3[] = [new THREE.Vector3(-12.5, 7.5, -8.5), new THREE.Vector3(12.5, 7.8, -9), new THREE.Vector3(-1, 10.5, -11)];
  for (const b of bulbs) {
    mesh(new THREE.CylinderGeometry(0.03, 0.03, 12, 4), mats.iron, b.x, b.y + 6.2, b.z, room);
    const shade = mesh(new THREE.ConeGeometry(0.9, 0.7, 20, 1, true), new THREE.MeshStandardMaterial({ color: 0x2f6c5c, side: THREE.DoubleSide, roughness: 0.5 }), b.x, b.y + 0.35, b.z, room);
    shade.castShadow = false;
    mesh(new THREE.SphereGeometry(0.28, 14, 10), new THREE.MeshBasicMaterial({ color: 0xffe0a0 }), b.x, b.y, b.z, room);
    const l = new THREE.PointLight(0xffc27a, 30, 20, 1.7); l.position.copy(b); room.add(l);
  }

  return { smoke, tables };
}

/** Soft smoke puffs rising from shisha heads. */
export class Smoke {
  private puffs: { s: THREE.Sprite; src: THREE.Vector3; t: number; life: number; drift: number }[] = [];
  private mat: THREE.SpriteMaterial;
  constructor(scene: THREE.Scene, sources: THREE.Vector3[]) {
    const tex = canvasTex(64, 64, (g, w) => { const gr = g.createRadialGradient(w / 2, w / 2, 0, w / 2, w / 2, w / 2); gr.addColorStop(0, 'rgba(235,228,215,.55)'); gr.addColorStop(1, 'rgba(235,228,215,0)'); g.fillStyle = gr; g.fillRect(0, 0, w, w); });
    this.mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthWrite: false });
    for (const src of sources) for (let i = 0; i < 7; i++) {
      const s = new THREE.Sprite(this.mat.clone()); scene.add(s);
      this.puffs.push({ s, src, t: Math.random() * 6, life: 6, drift: rnd(-0.3, 0.3) });
    }
  }
  tick(dt: number) {
    for (const p of this.puffs) {
      p.t += dt; if (p.t > p.life) p.t -= p.life;
      const k = p.t / p.life;
      p.s.position.set(p.src.x + Math.sin(k * 6 + p.drift * 10) * 0.6 + p.drift * k * 4, p.src.y + k * 6, p.src.z + p.drift * k * 2);
      const size = 0.6 + k * 3.2; p.s.scale.set(size, size, 1);
      (p.s.material as THREE.SpriteMaterial).opacity = Math.sin(k * Math.PI) * 0.45;
    }
  }
}
