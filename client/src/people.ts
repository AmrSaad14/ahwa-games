import * as THREE from 'three';
import { phys } from './boards';

/* Realistic, procedurally sculpted Egyptian coffeehouse regulars.
   Units: one scene unit is about 20 cm. A person is built around a pivot at the hips (piv), facing +z. */

type V3 = [number, number, number];
type Sex = 'm' | 'f';

export interface PersonSpec {
  skin: number; sex: Sex; age: 0 | 1 | 2;            // 0 young, 1 middle-aged, 2 old
  hair: number; hairStyle: 'short' | 'curly' | 'receding' | 'long' | 'none';
  brows: number;
  mustache?: 'thick' | 'trim'; beard?: 'full' | 'short'; stubble?: number; // stubble strength 0..1
  headwear?: 'tarboosh' | 'emma' | 'hijab';
  scarf?: number;                                         // hijab colour
  top: 'galabeya' | 'suit' | 'polo' | 'tee' | 'blouse';
  cloth: number; cloth2?: number; pattern?: 'stripe' | 'plaid' | 'plain';
  pants: number; shoes: number;
  glasses?: boolean; sebha?: boolean; watch?: boolean; earrings?: boolean; belly?: number;
  iris?: string;
  swatch: number;
}

/* ---------- small maths ---------- */
const clamp = (v: number, a = 0, b = 1) => Math.min(b, Math.max(a, v));
const sstep = (a: number, b: number, v: number) => { const t = clamp((v - a) / (b - a)); return t * t * (3 - 2 * t); };
const g2 = (dx: number, dy: number, sx: number, sy = sx) => Math.exp(-(dx * dx) / (2 * sx * sx) - (dy * dy) / (2 * sy * sy));
const lerp = (a: number, b: number, t: number) => a + (b - a) * t;
/** Cheap deterministic value noise. */
function hash(x: number, y: number) { const s = Math.sin(x * 127.1 + y * 311.7) * 43758.5453; return s - Math.floor(s); }
function noise(x: number, y: number) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
  return lerp(lerp(hash(xi, yi), hash(xi + 1, yi), u), lerp(hash(xi, yi + 1), hash(xi + 1, yi + 1), u), v);
}
const rgb = (c: number): V3 => [(c >> 16) & 255, (c >> 8) & 255, c & 255];
const mixC = (a: V3, b: V3, t: number): V3 => [lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t)];
const shade = (c: number, k: number) => { const [r, g, b] = rgb(c); return (clamp(r * k, 0, 255) << 16) | (clamp(g * k, 0, 255) << 8) | clamp(b * k, 0, 255); };

function tex(w: number, h: number, draw: (g: CanvasRenderingContext2D) => void, repeat?: [number, number]) {
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  draw(c.getContext('2d')!);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4;
  if (repeat) { t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(repeat[0], repeat[1]); }
  return t;
}
/** Paint a texture pixel by pixel. */
function pixTex(w: number, h: number, fn: (u: number, v: number) => V3) {
  return tex(w, h, g => {
    const img = g.createImageData(w, h), d = img.data;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const c = fn((x + 0.5) / w, (y + 0.5) / h), i = (y * w + x) * 4;
      d[i] = c[0]; d[i + 1] = c[1]; d[i + 2] = c[2]; d[i + 3] = 255;
    }
    g.putImageData(img, 0, 0);
  });
}

/* ---------- the head ---------- */
// Head in "head units": half its height is 1. The face looks along +z. Sphere directions are mapped with the seam at the back.
const SX = 0.77, SY = 1, SZ = 0.95;
const dirOf = (u: number, v: number): V3 => { const ph = -Math.PI / 2 + u * Math.PI * 2, th = v * Math.PI; return [-Math.cos(ph) * Math.sin(th), Math.cos(th), Math.sin(ph) * Math.sin(th)]; };
/** Direction pointing at a spot on the face, given in face coordinates (x right, y up, both before the jaw taper). */
export function faceDir(fx: number, fy: number): V3 { const x = fx / SX, y = fy / SY; return [x, y, Math.sqrt(Math.max(0, 1 - x * x - y * y))]; }

interface Face { sex: Sex; age: number; nose: number; jaw: number; lips: number }
/** How far the face surface stands out at a face position (x, y). */
function relief(x: number, y: number, f: Face) {
  const ax = Math.abs(x), m = f.sex === 'm' ? 1 : 0.55;
  let d = 0;
  d -= 0.1 * g2(ax - 0.31, y - 0.0, 0.13, 0.1);                         // eye sockets
  d += 0.06 * m * g2(ax - 0.27, y - 0.19, 0.2, 0.05) + 0.03 * g2(x, y - 0.14, 0.08, 0.06);                     // brow ridge
  d += 0.025 * g2(x, y - 0.3, 0.35, 0.12);                                // forehead
  // nose: bridge widening to a rounded tip
  const t = clamp((0.14 - y) / 0.36);
  const prof = (0.12 + 0.88 * Math.pow(t, 1.1)) * (1 - sstep(-0.2, -0.33, y)) * sstep(0.17, 0.08, y);
  const w = 0.045 + 0.05 * t + 0.04 * sstep(0.75, 1, t);
  d += 0.21 * f.nose * prof * g2(x, 0, w);
  d += (f.sex === 'm' ? 0.05 : 0.025) * f.nose * g2(ax - 0.09, y + 0.27, 0.045, 0.03);              // nostril wings
  d += 0.03 * g2(ax - 0.43, y + 0.12, 0.13, 0.11);                        // cheekbones
  d += 0.035 * g2(x, y + 0.43, 0.13, 0.05) * f.lips;                      // upper lip
  d += 0.04 * g2(x, y + 0.55, 0.12, 0.05) * f.lips;                       // lower lip
  d -= 0.03 * g2(x, y + 0.5, 0.16, 0.012);                                // mouth line
  d -= 0.025 * g2(x, y + 0.66, 0.15, 0.04);                               // under the lip
  d += 0.1 * g2(x, y + 0.8, 0.17, 0.1) * (f.sex === 'm' ? 1 : 0.75);    // chin
  if (f.age > 1) d -= 0.025 * g2(ax - 0.36, y + 0.3, 0.12, 0.12);        // hollow cheeks
  return d;
}
/** Position on the sculpted head surface for a unit direction. */
export function headPoint(dir: V3, f: Face, lift = 0): V3 {
  const [x, y, z] = dir;
  let X = x * SX, Y = y * SY, Z = z * SZ;
  if (z < 0) { Z *= 1.12; Y += 0.05 * -z; }                               // the back of the skull
  if (y > 0.5 && z > 0) Z -= 0.08 * (y - 0.5) * z;                         // forehead slopes back
  const taper = 1 - f.jaw * sstep(-0.15, -1, Y) * (0.6 + 0.4 * Math.max(0, z));
  const fr = z > 0 ? relief(X, Y, f) * sstep(0.1, 0.55, z) : 0;
  X *= taper;
  if (Y < -0.2) Z -= 0.12 * sstep(-0.2, -1, Y) * Math.max(0, -z + 0.2);   // under the jaw tucks in
  const k = 1 + lift;
  return [X * k, Y * k, (Z + fr) * k + (lift ? lift * 0.3 * Math.max(0, z) : 0)];
}

/** A grid surface: fn(u, v) gives a point for u, v in 0..1. */
function sheet(nu: number, nv: number, fn: (u: number, v: number) => V3, closeU = false) {
  const pos: number[] = [], uv: number[] = [], idx: number[] = [];
  for (let j = 0; j <= nv; j++) for (let i = 0; i <= nu; i++) {
    const u = i / nu, v = j / nv, p = fn(u, v);
    pos.push(p[0], p[1], p[2]); uv.push(u, 1 - v);
  }
  const W = nu + 1;
  for (let j = 0; j < nv; j++) for (let i = 0; i < nu; i++) {
    const a = j * W + i, b = a + 1, c = a + W, d = c + 1;
    idx.push(a, c, b, b, c, d);
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  geo.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  geo.setIndex(idx);
  geo.computeVertexNormals();
  if (closeU) {
    // average normals across the seam
    const n = geo.getAttribute('normal');
    for (let j = 0; j <= nv; j++) {
      const a = j * W, b = j * W + nu;
      const x = n.getX(a) + n.getX(b), y = n.getY(a) + n.getY(b), z = n.getZ(a) + n.getZ(b), l = Math.hypot(x, y, z) || 1;
      n.setXYZ(a, x / l, y / l, z / l); n.setXYZ(b, x / l, y / l, z / l);
    }
  }
  return geo;
}
/** Layer that follows the head surface over a region of directions. */
const onHead = (f: Face, dir: (u: number, v: number) => V3, lift: (u: number, v: number) => number, nu = 48, nv = 24, closeU = false) =>
  sheet(nu, nv, (u, v) => headPoint(dir(u, v), f, lift(u, v)), closeU);

const slerpDir = (a: V3, b: V3, t: number): V3 => {
  const d = clamp(a[0] * b[0] + a[1] * b[1] + a[2] * b[2], -1, 1), om = Math.acos(d);
  if (om < 1e-4) return a;
  const s = Math.sin(om), ka = Math.sin((1 - t) * om) / s, kb = Math.sin(t * om) / s;
  return [a[0] * ka + b[0] * kb, a[1] * ka + b[1] * kb, a[2] * ka + b[2] * kb];
};

function skinTexture(p: PersonSpec, f: Face) {
  const base = rgb(p.skin), lip = mixC(base, [118, 48, 46], p.sex === 'f' ? 0.55 : 0.4), blush = mixC(base, [190, 80, 70], 0.5);
  const stubCol = rgb(p.hair === 0xd8d4cc ? 0x8a8680 : shade(p.hair, 0.9));
  const dark = mixC(base, [40, 22, 14], 0.45);
  return pixTex(1024, 512, (u, v) => {
    const d = dirOf(u, v), fx = d[0] * SX, fy = d[1] * SY, front = sstep(0, 0.45, d[2]);
    let c: V3 = mixC(base, [255, 235, 220], (noise(u * 40, v * 20) - 0.5) * 0.08);
    c = mixC(c, dark, (noise(u * 9, v * 5) - 0.5) * 0.12);
    const ax = Math.abs(fx);
    if (front > 0) {
      c = mixC(c, blush, 0.22 * g2(ax - 0.42, fy + 0.25, 0.14, 0.12) * front);
      const half = 0.17 * (p.sex === 'f' ? 0.95 : 1), wx = clamp(1 - Math.pow(ax / half, 2.5));
      const upTop = -0.455 + 0.012 * Math.cos(ax / half * Math.PI * 1.5) * wx - (1 - wx) * 0.04;   // cupid's bow
      const loBot = -0.5 - (p.sex === 'f' ? 0.075 : 0.06) * Math.sqrt(wx);
      const inLip = sstep(upTop + 0.008, upTop - 0.008, fy) * sstep(loBot - 0.008, loBot + 0.008, fy) * sstep(0, 0.12, wx);
      c = mixC(c, lip, inLip * front);
      c = mixC(c, mixC(lip, [255, 210, 200], 0.25), 0.4 * inLip * g2(fx, fy + 0.535, 0.07, 0.012) * front);  // light on the lower lip
      for (const nx of [-0.055, 0.055]) c = mixC(c, [45, 22, 16], (p.sex === 'f' ? 0.4 : 0.6) * g2(fx - nx, fy + 0.31, 0.018, 0.009) * front);
      c = mixC(c, [50, 25, 20], 0.7 * sstep(0.007, 0.002, Math.abs(fy + 0.5 - 0.006 * Math.cos(ax * 9))) * clamp(1 - ax / 0.18) * front); // mouth line
      c = mixC(c, dark, 0.35 * g2(ax - 0.31, fy + 0.13, 0.09, 0.035) * front);             // under the eyes
      c = mixC(c, dark, 0.3 * g2(ax - 0.31, fy - 0.08, 0.1, 0.03) * front);               // lid creases
      if (p.sex === 'f') {
        c = mixC(c, [70, 40, 30], 0.35 * g2(ax - 0.33, fy - 0.05, 0.1, 0.025) * front);    // soft shadow on the lids
        c = mixC(c, [205, 110, 100], 0.18 * g2(ax - 0.4, fy + 0.22, 0.1, 0.08) * front);   // blush
      }
      if (f.age > 0) {
        const fold = g2(ax - (0.14 + (-0.3 - fy) * 0.45), 0, 0.012) * sstep(-0.24, -0.32, fy) * sstep(-0.62, -0.5, fy);
        c = mixC(c, dark, (f.age > 1 ? 0.45 : 0.15) * fold * front);
      }
      if (f.age > 1) {
        const lines = Math.pow(Math.abs(Math.sin((fy - 0.32) * 70)), 18) * g2(fx, fy - 0.42, 0.3, 0.08);
        c = mixC(c, dark, 0.35 * lines * front);
        const crow = Math.pow(Math.abs(Math.sin(Math.atan2(fy, ax - 0.55) * 9)), 10) * g2(ax - 0.55, fy, 0.06);
        c = mixC(c, dark, 0.3 * crow * front);
      }
    }
    if (p.stubble) {
      // beard shadow over the jaw, chin and upper lip, speckled
      const jaw = sstep(-0.1, -0.35, fy) * sstep(-0.05, 0.25, d[2] + 0.3) * clamp(1 - g2(fx, fy + 0.52, 0.17, 0.09) * 1.4);
      const lipTop = g2(fx, fy + 0.4, 0.2, 0.05);
      const sp = 0.55 + 0.45 * hash(Math.floor(u * 1024), Math.floor(v * 512));
      c = mixC(c, stubCol, clamp((jaw + lipTop) * p.stubble * sp) * 0.75);
    }
    return c.map(x => clamp(Math.round(x), 0, 255)) as V3;
  });
}

function eyeTexture(iris: string) {
  return tex(256, 128, g => {
    const sc = g.createRadialGradient(128, 64, 10, 128, 64, 128);
    sc.addColorStop(0, '#f7f2ea'); sc.addColorStop(0.55, '#efe6db'); sc.addColorStop(1, '#d9a49a');
    g.fillStyle = sc; g.fillRect(0, 0, 256, 128);
    g.save(); g.translate(128, 64); g.scale(1, 2);
    const ir = g.createRadialGradient(0, 0, 3, 0, 0, 24);
    ir.addColorStop(0, '#000'); ir.addColorStop(0.3, '#1a0f08'); ir.addColorStop(0.45, iris); ir.addColorStop(0.9, iris); ir.addColorStop(1, '#120a05');
    g.fillStyle = ir; g.beginPath(); g.arc(0, 0, 24, 0, Math.PI * 2); g.fill();
    g.strokeStyle = 'rgba(255,220,170,.18)'; g.lineWidth = 0.8;
    for (let a = 0; a < Math.PI * 2; a += 0.22) { g.beginPath(); g.moveTo(Math.cos(a) * 9, Math.sin(a) * 9); g.lineTo(Math.cos(a) * 22, Math.sin(a) * 22); g.stroke(); }
    g.fillStyle = '#050302'; g.beginPath(); g.arc(0, 0, 8.5, 0, Math.PI * 2); g.fill();
    g.restore();
  });
}

/** Hair strands running down the v direction. */
function strandTexture(col: number, curly = false) {
  const [r, g0, b] = rgb(col);
  return tex(256, 256, g => {
    g.fillStyle = `rgb(${r},${g0},${b})`; g.fillRect(0, 0, 256, 256);
    if (curly) {
      for (let i = 0; i < 900; i++) {
        const k = 0.6 + Math.random() * 0.8;
        g.strokeStyle = `rgba(${r * k | 0},${g0 * k | 0},${b * k | 0},.8)`; g.lineWidth = 1.5;
        g.beginPath(); g.arc(Math.random() * 256, Math.random() * 256, 2 + Math.random() * 3, 0, Math.PI * 2); g.stroke();
      }
    } else {
      for (let i = 0; i < 700; i++) {
        const k = 0.55 + Math.random() * 0.9, x = Math.random() * 256;
        g.strokeStyle = `rgba(${Math.min(255, r * k + 12) | 0},${Math.min(255, g0 * k + 10) | 0},${Math.min(255, b * k + 8) | 0},.55)`;
        g.lineWidth = 0.6 + Math.random();
        g.beginPath(); g.moveTo(x, 0); g.bezierCurveTo(x + 4, 85, x - 4, 170, x + 2, 256); g.stroke();
      }
    }
  });
}

/** Woven cloth with an optional pattern. */
function clothTexture(base: number, pattern: PersonSpec['pattern'] = 'plain', second?: number) {
  const [r, g0, b] = rgb(base);
  const s2 = second ?? shade(base, 0.7);
  return tex(256, 256, g => {
    g.fillStyle = `rgb(${r},${g0},${b})`; g.fillRect(0, 0, 256, 256);
    if (pattern === 'stripe') {
      const [r2, gg2, b2] = rgb(s2);
      g.fillStyle = `rgba(${r2},${gg2},${b2},.85)`;
      for (let x = 0; x < 256; x += 16) { g.fillRect(x, 0, 2, 256); g.fillRect(x + 5, 0, 1, 256); }
    } else if (pattern === 'plaid') {
      const [r2, gg2, b2] = rgb(s2);
      g.fillStyle = `rgba(${r2},${gg2},${b2},.35)`;
      for (let x = 0; x < 256; x += 32) { g.fillRect(x, 0, 10, 256); g.fillRect(0, x, 256, 10); }
      g.fillStyle = 'rgba(255,255,255,.12)';
      for (let x = 16; x < 256; x += 32) { g.fillRect(x, 0, 2, 256); g.fillRect(0, x, 256, 2); }
    }
    // weave
    for (let y = 0; y < 256; y += 2) { g.fillStyle = `rgba(0,0,0,${0.03 + Math.random() * 0.04})`; g.fillRect(0, y, 256, 1); }
    for (let x = 0; x < 256; x += 2) { g.fillStyle = `rgba(255,255,255,${Math.random() * 0.04})`; g.fillRect(x, 0, 1, 256); }
  }, [3, 3]);
}

/* ---------- the person ---------- */

export interface Person { group: THREE.Group; piv: THREE.Group; head: THREE.Group; arms: THREE.Group[]; elbows: THREE.Group[]; hands: THREE.Group[] }

const HEAD_R = 0.6;            // head units to scene units
export const HEAD_Y = 3.62;    // head centre above the hips
const SHOULDER_Y = 2.6;

const texCache = new Map<string, THREE.Texture>();
const cached = (k: string, make: () => THREE.Texture) => { let t = texCache.get(k); if (!t) { t = make(); texCache.set(k, t); } return t; };

export function makePerson(p: PersonSpec, key: string): Person {
  const group = new THREE.Group(), piv = new THREE.Group(); piv.position.y = -1.75; group.add(piv);
  const legs = new THREE.Group(); legs.position.y = -1.75; group.add(legs);
  const mesh = (parent: THREE.Object3D, geo: THREE.BufferGeometry, mat: THREE.Material, x = 0, y = 0, z = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); parent.add(m); return m; };
  const f: Face = { sex: p.sex, age: p.age, nose: p.sex === 'm' ? 1.1 : 0.92, jaw: p.sex === 'm' ? 0.28 : 0.36, lips: p.sex === 'f' ? 1.15 : 0.95 };

  const skin = phys({ color: 0xffffff, map: cached('skin' + key, () => skinTexture(p, f)), roughness: 0.52, sheen: 0.25, sheenRoughness: 0.45, sheenColor: new THREE.Color(0xc98a78), envMapIntensity: 0.35 });
  const handSkin = phys({ color: p.skin, roughness: 0.55, sheen: 0.25, sheenRoughness: 0.45, sheenColor: new THREE.Color(0xc98a78), envMapIntensity: 0.35 });
  const hairMat = (curly = false) => phys({ color: 0xffffff, map: cached('hair' + p.hair + curly, () => strandTexture(p.hair, curly)), roughness: 0.6, sheen: 0.8, sheenRoughness: 0.35, sheenColor: new THREE.Color(shade(p.hair, 2.2)), envMapIntensity: 0.3, side: THREE.DoubleSide });
  const browMat = phys({ color: p.brows, roughness: 0.85, envMapIntensity: 0.2, side: THREE.DoubleSide });
  const clothMat = (col: number, pat: PersonSpec['pattern'] = 'plain', second?: number) =>
    phys({ color: 0xffffff, map: cached('cloth' + col + pat + second, () => clothTexture(col, pat, second)), roughness: 0.88, sheen: 0.6, sheenRoughness: 0.7, sheenColor: new THREE.Color(shade(col, 1.4)), envMapIntensity: 0.25 });
  const robe = p.top === 'galabeya';
  const topMat = clothMat(p.cloth, p.pattern, p.cloth2);
  const sleeveMat = p.top === 'suit' ? clothMat(p.cloth2 ?? 0x4a3d33) : topMat;
  const pantsMat = robe ? topMat : clothMat(p.pants);
  const shoeMat = phys({ color: p.shoes, roughness: 0.4, clearcoat: 0.4, clearcoatRoughness: 0.4, envMapIntensity: 0.5 });
  const gold = phys({ color: 0xd8a84a, metalness: 1, roughness: 0.25 });

  /* torso */
  const fem = p.sex === 'f', belly = p.belly ?? 0;
  const prof: [number, number][] = [
    [0.001, 0.0], [0.7, 0.02], [fem ? 0.98 : 0.88, 0.4], [fem ? 0.82 : 0.86 + belly * 0.12, 0.95], [fem ? 0.78 : 0.86 + belly * 0.16, 1.3],
    [fem ? 0.9 : 0.9 + belly * 0.08, 1.75], [fem ? 0.92 : 0.98, 2.15], [fem ? 0.9 : 1.0, 2.42], [0.82, 2.68], [0.52, 2.86], [0.33, 2.93], [0.3, 2.95],
  ];
  const lathe = (pts: [number, number][], segs = 40, start = 0, len = Math.PI * 2) => new THREE.LatheGeometry(pts.map(([r, y]) => new THREE.Vector2(r, y)), segs, start, len);
  const torso = mesh(piv, lathe(prof), topMat); torso.scale.z = 0.62;
  if (p.top === 'suit') {
    // jacket open at the front over a white shirt and tie
    const jacket = mesh(piv, lathe(prof.map(([r, y]) => [r * 1.05 + 0.03, y] as [number, number]).slice(0, 10), 40, 0.34, Math.PI * 2 - 0.68), sleeveMat);
    jacket.scale.z = 0.66; (jacket.material as THREE.Material).side = THREE.DoubleSide;
    const tie = phys({ color: 0x5a1d1d, roughness: 0.5, sheen: 0.5, sheenColor: new THREE.Color(0xa04040) });
    const tg = mesh(piv, new THREE.BoxGeometry(0.16, 1.15, 0.04), tie, 0, 2.2, 0.58); tg.rotation.x = -0.12;
    mesh(piv, new THREE.BoxGeometry(0.14, 0.1, 0.05), tie, 0, 2.8, 0.36);
    for (const sx of [-1, 1]) { const l = mesh(piv, new THREE.BoxGeometry(0.22, 0.95, 0.04), sleeveMat, sx * 0.22, 2.35, 0.6); l.rotation.set(-0.15, 0, sx * 0.35); }
    for (const y of [1.35, 1.75]) mesh(piv, new THREE.SphereGeometry(0.045, 8, 6), phys({ color: 0x1c140e, roughness: 0.3 }), 0.3, y, 0.6);
  }
  // necklines
  const trim = phys({ color: shade(p.cloth, 0.8), roughness: 0.8 });
  if (robe) {
    const v = mesh(piv, new THREE.BoxGeometry(0.08, 0.6, 0.03), trim, 0, 2.5, 0.56); v.rotation.x = -0.25;
    for (const y of [2.35, 2.55]) mesh(piv, new THREE.SphereGeometry(0.035, 8, 6), phys({ color: 0x3a2a1a, roughness: 0.4 }), 0, y, 0.6);
    const col = mesh(piv, new THREE.TorusGeometry(0.33, 0.05, 8, 24), topMat, 0, 2.92, 0.02); col.rotation.x = Math.PI / 2; col.scale.y = 0.75;
  } else if (p.top === 'polo' || p.top === 'suit') {
    const cmat = p.top === 'suit' ? clothMat(0xf1eee6) : topMat;
    for (const sx of [-1, 1]) { const c = mesh(piv, new THREE.BoxGeometry(0.32, 0.05, 0.22), cmat, sx * 0.2, 2.95, 0.3); c.rotation.set(0.5, sx * 0.55, sx * -0.3); }
    if (p.top === 'polo') for (const y of [2.62, 2.78]) mesh(piv, new THREE.SphereGeometry(0.03, 8, 6), phys({ color: 0xeeeeee, roughness: 0.4 }), 0, y, 0.57);
    if (p.top === 'suit') { const sh = mesh(piv, new THREE.CylinderGeometry(0.33, 0.36, 0.25, 20), cmat, 0, 2.9, 0.02); sh.scale.z = 0.8; }
  } else {
    const col = mesh(piv, new THREE.TorusGeometry(0.31, 0.04, 8, 24), trim, 0, 2.93, 0.02); col.rotation.x = Math.PI / 2; col.scale.y = 0.75;
  }

  /* legs: seated, thighs forward, shins down */
  const capsuleBetween = (parent: THREE.Object3D, a: V3, b: V3, r0: number, r1: number, mat: THREE.Material) => {
    const va = new THREE.Vector3(...a), vb = new THREE.Vector3(...b), len = va.distanceTo(vb);
    const m = mesh(parent, new THREE.CylinderGeometry(r1, r0, len, 16, 1), mat);
    m.position.copy(va).add(vb).multiplyScalar(0.5);
    m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), vb.clone().sub(va).normalize());
    mesh(parent, new THREE.SphereGeometry(r1, 16, 10), mat, ...b);
    return m;
  };
  for (const sx of [-1, 1]) {
    const hip: V3 = [sx * 0.45, 0.3, 0.05], knee: V3 = [sx * 0.5, 0.36, 1.9], ankle: V3 = [sx * 0.52, -1.62, 2.0];
    capsuleBetween(legs, hip, knee, 0.42, 0.33, pantsMat);
    capsuleBetween(legs, knee, ankle, 0.3, 0.22, pantsMat);
    const shoe = mesh(legs, new THREE.CapsuleGeometry(0.2, 0.55, 6, 12), shoeMat, sx * 0.52, -1.72, 2.3); shoe.rotation.x = Math.PI / 2; shoe.scale.set(1.05, 1, 0.65);
  }
  if (robe) {
    // the galabeya drapes from the lap down between the knees to the ankles
    const drape = sheet(18, 24, (u, v) => {
      const x = (u - 0.5) * 1.85 * (1 + Math.max(0, v - 0.45) * 0.2), sag = Math.sin(u * Math.PI);
      if (v < 0.45) {                                   // over the lap, sagging between the thighs
        const k = v / 0.45;
        return [x, 0.66 + 0.06 * Math.sin(k * Math.PI) - 0.18 * sag * Math.sin(k * Math.PI) * 0.6 + 0.04 * Math.cos(u * Math.PI * 2), lerp(0.05, 2.12, k)];
      }
      const k = (v - 0.45) / 0.55;                      // and down from the knees to the ankles
      return [x, lerp(0.66, -1.5, k) - (1 - k) * 0, lerp(2.12, 2.1, k) + 0.1 * Math.sin(k * Math.PI) - 0.2 * sag * Math.sin(k * Math.PI)];
    });
    const dm = mesh(legs, drape, topMat); (dm.material as THREE.Material).side = THREE.DoubleSide;
  } else mesh(legs, new THREE.BoxGeometry(1.25, 0.55, 0.9), pantsMat, 0, 0.35, 0.2);

  /* neck and head */
  const neck = mesh(piv, new THREE.CylinderGeometry(fem ? 0.22 : 0.27, fem ? 0.26 : 0.31, 0.8, 20), handSkin, 0, 3.1, 0.0); neck.rotation.x = 0.12;
  const head = new THREE.Group(); head.position.set(0, HEAD_Y, 0.08); piv.add(head);
  const hs = new THREE.Group(); hs.scale.setScalar(HEAD_R); head.add(hs);
  // denser around the face, so the nose, lips and eyelids keep their shape
  const skull = (() => {
    const nu = 120, nv = 84, pos: number[] = [], uv: number[] = [], idx: number[] = [];
    const wu = (u: number) => u - 0.62 * Math.sin(2 * Math.PI * (u - 0.5)) / (2 * Math.PI);
    const sv = (v: number) => Math.sin(2 * Math.PI * (v - 0.53));
    const wv = (v: number) => v - 0.5 * (sv(v) - sv(0) - v * (sv(1) - sv(0))) / (2 * Math.PI);
    for (let j = 0; j <= nv; j++) for (let i = 0; i <= nu; i++) {
      const u = wu(i / nu), v = clamp(wv(j / nv)), q = headPoint(dirOf(u, v), f);
      pos.push(q[0], q[1], q[2]); uv.push(u, 1 - v);
    }
    const W = nu + 1;
    for (let j = 0; j < nv; j++) for (let i = 0; i < nu; i++) { const a = j * W + i, b = a + 1, c = a + W, d = c + 1; idx.push(a, c, b, b, c, d); }
    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    geo.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
    geo.setIndex(idx); geo.computeVertexNormals();
    return geo;
  })();
  mesh(hs, skull, skin);

  // ears
  for (const sx of [-1, 1]) {
    const e = mesh(hs, new THREE.SphereGeometry(1, 20, 14), skin, sx * SX * 0.97, -0.04, -0.06); e.scale.set(0.07, 0.27, 0.16); e.rotation.y = sx * 0.35;
    const rim = mesh(hs, new THREE.TorusGeometry(0.13, 0.03, 8, 18, Math.PI * 1.4), handSkin, sx * (SX * 0.97 + 0.04), -0.02, -0.06); rim.rotation.set(0, sx * Math.PI / 2, Math.PI * 0.3); rim.scale.set(1, 1.8, 1);
  }
  // eyes with lids
  const eyeM = phys({ color: 0xffffff, map: cached('eye' + (p.iris ?? '#4a2c17'), () => eyeTexture(p.iris ?? '#4a2c17')), roughness: 0.1, clearcoat: 1, clearcoatRoughness: 0.05, envMapIntensity: 0.6 });
  const lidM = skin;
  for (const sx of [-1, 1]) {
    const c = headPoint(faceDir(sx * 0.31, 0.0), f);
    const ec: V3 = [c[0], c[1], c[2] - 0.095];
    const r = 0.12;
    const eye = mesh(hs, new THREE.SphereGeometry(r, 32, 20, -Math.PI / 2), eyeM, ...ec); eye.rotation.set(0.08, -sx * 0.06, 0);
    // upper lid covers the top, lower lid the bottom, leaving an almond-shaped opening
    const up = mesh(hs, new THREE.SphereGeometry(r * 1.06, 32, 12, 0, Math.PI * 2, 0, Math.PI * 0.4), lidM, ...ec); up.rotation.x = 0.05 + (p.age > 1 ? 0.1 : 0); up.rotation.z = sx * 0.06;
    const lo = mesh(hs, new THREE.SphereGeometry(r * 1.04, 32, 10, 0, Math.PI * 2, Math.PI * 0.64, Math.PI * 0.36), lidM, ...ec); lo.rotation.x = -0.05;
    // lash line
    const lash = mesh(hs, new THREE.TorusGeometry(r * 1.06, fem ? 0.02 : 0.009, 6, 20, Math.PI * 0.8), phys({ color: 0x120b06, roughness: 0.6 }), ...ec);
    lash.rotation.set(-0.12 - (p.age > 1 ? 0.18 : 0), 0, Math.PI * 0.125);
    lash.position.z += 0.01; lash.position.y += 0.012;
    // brows follow the brow ridge
    const brow = onHead(f, (u, v) => {
      const x = sx * lerp(0.12, 0.47, u), arch = fem ? 0.06 : 0.035;
      const yb = 0.2 + arch * Math.sin(Math.min(1, u * 1.4) * Math.PI * 0.85) - (fem ? 0.02 : 0) - u * 0.03;
      return faceDir(x, yb + lerp(-1, 1, v) * (fem ? 0.022 : 0.04) * (1 - u * 0.55));
    }, (u, v) => 0.012 + 0.02 * Math.sin(v * Math.PI) * (p.sex === 'm' ? 1.4 : 0.8) * (1 - u * 0.4), 14, 4);
    mesh(hs, brow, browMat);
  }

  /* facial hair */
  const facial = phys({ color: 0xffffff, map: cached('hair' + p.hair + false, () => strandTexture(p.hair)), roughness: 0.75, sheen: 0.6, sheenColor: new THREE.Color(shade(p.hair, 1.8)), envMapIntensity: 0.25, side: THREE.DoubleSide });
  if (p.mustache) {
    const W = p.mustache === 'thick' ? 0.25 : 0.2, Tk = p.mustache === 'thick' ? 0.095 : 0.045;
    mesh(hs, onHead(f, (u, v) => {
      const x = (u - 0.5) * 2 * W, ax = Math.abs(x) / W;
      const top = -0.335 - Math.pow(ax, 1.6) * 0.1 + 0.012 * g2(ax, 0, 0.12), bot = -0.47 - Math.pow(ax, 2) * (p.mustache === 'thick' ? 0.07 : 0.04);
      return faceDir(x, lerp(top, bot, v));
    }, (u, v) => 0.006 + Tk * Math.pow(Math.sin(u * Math.PI), 1.1) * Math.pow(Math.sin(Math.min(1, v * 1.1 + 0.08) * Math.PI), 0.8), 36, 10), facial);
  }
  if (p.beard) {
    const full = p.beard === 'full';
    // from ear to ear around the jaw and chin, under the mouth
    mesh(hs, onHead(f, (u, v) => {
      const a = lerp(-1, 1, u), ang = a * 1.7;                // angle around the face
      const side: V3 = [Math.sin(ang), 0, Math.cos(ang)];
      const topY = lerp(-0.56, -0.05, Math.pow(Math.abs(a), 1.4));   // under the lip in front, up to the sideburns
      const y0 = lerp(topY, -1.0, v);
      const r = Math.sqrt(Math.max(0, 1 - y0 * y0));
      return [side[0] * r, y0, side[2] * r];
    }, (u, v) => {
      const a = Math.abs(lerp(-1, 1, u));
      const th = full ? 0.1 + 0.3 * v * v * (1 - a * 0.8) : 0.035;
      return 0.01 + th * Math.sin(Math.min(1, v * 3) * Math.PI / 2) * (1 - Math.pow(a, 6));
    }, 40, 14), facial);
  }

  /* hair */
  if (p.hairStyle !== 'none' && p.headwear !== 'hijab') {
    const curly = p.hairStyle === 'curly', recede = p.hairStyle === 'receding' ? 1 : 0, long = p.hairStyle === 'long';
    const hm = hairMat(curly);
    const thMax = (ph: number) => {
      const s = Math.sin(ph), c = Math.abs(Math.cos(ph));
      // hairline: forehead in front, around the ears at the sides, the nape at the back
      let th = 1.5 - (s > 0 ? s * (0.5 - recede * 0.18 * (1 - c * 0.4)) : s * (long ? 0.85 : 0.62));
      if (s > 0 && recede) th -= 0.22 * Math.pow(Math.sin(ph * 2), 2) * s;      // temples
      if (fem && s > 0) th -= 0.04;
      return th;
    };
    const lift = (u: number, v: number) => {
      const ph = -Math.PI / 2 + u * Math.PI * 2, s = Math.sin(ph);
      const edge = sstep(1, long ? 0.55 : 0.7, v);                              // thins out toward the hairline
      let t = curly ? 0.07 + 0.16 * sstep(0.7, 0.1, v) : long ? 0.09 * (1 - 0.75 * Math.exp(-Math.pow((u - 0.5) / 0.012, 2)) * sstep(0.95, 0.3, v)) : 0.035 + 0.025 * sstep(0.8, 0.1, v);
      if (curly) t += 0.07 * (noise(u * 70, v * 22) - 0.45) + 0.03 * (noise(u * 30, v * 9) - 0.5);
      if (recede) t *= lerp(0.55, 1, sstep(0.2, 0.75, v + (s < 0 ? 0.5 : 0)));
      if (s > 0 && !long) t += 0.03 * s * sstep(0.6, 0.2, v);                  // a little volume at the front
      return 0.012 + t * edge;
    };
    mesh(hs, onHead(f, (u, v) => {
      const ph = -Math.PI / 2 + u * Math.PI * 2, th = v * thMax(ph);
      return [-Math.cos(ph) * Math.sin(th), Math.cos(th), Math.sin(ph) * Math.sin(th)];
    }, lift, curly ? 160 : 64, curly ? 48 : 24, true), hm);
    if (long) {
      // hair falling behind the shoulders, and a side part sweeping across
      const fall = sheet(24, 14, (u, v) => {
        const a = lerp(-1.65, 1.65, u), w = lerp(1.04, 0.95, v) * (1 + 0.08 * Math.sin(v * 9 + u * 3));
        const x = Math.sin(a) * 0.95 * w, z = -Math.cos(a) * 0.72 * w - 0.12 - v * 0.15;
        const y = lerp(-0.2, -3.0, v) - (1 - Math.cos(a)) * 0.45 * v;
        return [x, y, z];
      });
      mesh(hs, fall, hm);
    }
  }

  /* headwear */
  if (p.headwear === 'tarboosh') {
    const felt = phys({ color: 0x9e1b17, roughness: 0.75, sheen: 1, sheenRoughness: 0.5, sheenColor: new THREE.Color(0xff6a50), envMapIntensity: 0.3 });
    const tb = new THREE.Group(); tb.position.set(0, 0.62, -0.06); tb.rotation.x = -0.12; hs.add(tb);
    mesh(tb, new THREE.CylinderGeometry(0.6, 0.76, 0.85, 40, 1), felt, 0, 0.3, 0);
    mesh(tb, new THREE.CylinderGeometry(0.6, 0.6, 0.02, 40), felt, 0, 0.73, 0);
    const silk = phys({ color: 0x0d0b0a, roughness: 0.35, sheen: 1, sheenColor: new THREE.Color(0x555555) });
    mesh(tb, new THREE.CylinderGeometry(0.04, 0.04, 0.08, 8), silk, 0, 0.77, 0);
    for (let k = 0; k < 14; k++) {
      const a = (k / 14) * Math.PI - Math.PI / 2;
      const s = mesh(tb, new THREE.CylinderGeometry(0.012, 0.018, 0.75, 4), silk, Math.sin(a) * 0.07 + 0.08, 0.5, -0.28 - Math.cos(a) * 0.04);
      s.rotation.set(-0.55, 0, 0.25 + Math.sin(a) * 0.12);
    }
  } else if (p.headwear === 'emma') {
    const cotton = clothMat(0xf4f1ea);
    const em = new THREE.Group(); em.position.set(0, 0.5, -0.05); em.rotation.x = -0.1; hs.add(em);
    const cap = mesh(em, new THREE.SphereGeometry(0.86, 32, 16, 0, Math.PI * 2, 0, Math.PI / 2), cotton, 0, 0.05, 0); cap.scale.y = 0.75;
    for (let k = 0; k < 5; k++) {
      const tr = mesh(em, new THREE.TorusGeometry(0.84 - k * 0.075, 0.15, 12, 40), cotton, 0, 0.0 + k * 0.12, 0);
      tr.rotation.set(Math.PI / 2 + (k % 2 ? 0.12 : -0.1), (k % 2 ? 0.1 : -0.08), 0); tr.scale.set(1, 1.08, 0.72);
    }
    const tail = sheet(4, 8, (u, v) => [lerp(0.35, 0.65, u) + v * 0.05, lerp(0.15, -1.1, v), -0.82 - v * 0.08 - Math.sin(u * Math.PI) * 0.05]);
    const tm = mesh(em, tail, cotton); (tm.material as THREE.Material).side = THREE.DoubleSide;
  } else if (p.headwear === 'hijab') {
    const fab = clothMat(p.scarf ?? 0xb5523b);
    (fab as THREE.Material).side = THREE.DoubleSide;
    // a hood that frames the face, rays from the face edge meeting at the back
    const back: V3 = [0, 0.1, -1];
    mesh(hs, onHead(f, (u, v) => {
      const a = u * Math.PI * 2;
      const rim = faceDir(Math.cos(a) * 0.62, -0.12 + Math.sin(a) * (Math.sin(a) > 0 ? 0.78 : 0.92));
      return slerpDir(rim, back, v);
    }, (u, v) => {
      const a = u * Math.PI * 2;
      const vol = 0.07 + 0.1 * Math.sin(v * Math.PI) + 0.15 * Math.max(0, Math.sin(a)) * sstep(0.4, 0.9, v);
      return sstep(0, 0.08, v) * vol + 0.03;
    }, 64, 20, true), fab);
    // and drapes over the neck, shoulders and chest
    const cape = new THREE.Mesh(lathe([[0.4, 3.3], [0.46, 3.1], [0.7, 2.85], [1.0, 2.6], [1.04, 2.35], [0.98, 1.95], [0.95, 1.8]], 48), fab);
    cape.scale.z = 0.66; cape.position.z = 0.04; piv.add(cape);
  }

  if (p.glasses) {
    const frame = phys({ color: 0x2a1c12, roughness: 0.3, clearcoat: 1 });
    const lens = phys({ color: 0xffffff, transmission: 0.95, roughness: 0.05, transparent: true, opacity: 0.25, ior: 1.5, thickness: 0.02 });
    for (const sx of [-1, 1]) {
      const c = headPoint(faceDir(sx * 0.31, -0.01), f);
      const ring = mesh(hs, new THREE.TorusGeometry(0.15, 0.016, 8, 32), frame, c[0], c[1], c[2] + 0.12); ring.scale.set(1.15, 0.85, 1);
      const l = mesh(hs, new THREE.CircleGeometry(0.15, 24), lens, c[0], c[1], c[2] + 0.12); l.scale.set(1.15, 0.85, 1);
      const arm = mesh(hs, new THREE.BoxGeometry(0.02, 0.025, 0.6), frame, sx * 0.67, c[1] + 0.03, c[2] - 0.26); arm.rotation.y = sx * 0.2;
    }
    const nb = headPoint(faceDir(0, 0.02), f);
    const br = mesh(hs, new THREE.TorusGeometry(0.06, 0.014, 6, 12, Math.PI), frame, 0, nb[1] - 0.01, nb[2] + 0.06);
    br.rotation.z = 0;
  }
  if (p.earrings) for (const sx of [-1, 1]) mesh(hs, new THREE.SphereGeometry(0.045, 12, 10), gold, sx * SX * 1.0, -0.28, -0.02);

  /* arms */
  const arms: THREE.Group[] = [], elbows: THREE.Group[] = [], hands: THREE.Group[] = [];
  const shortSleeve = p.top === 'polo' || p.top === 'tee';
  for (const sx of [-1, 1]) {
    const sh = new THREE.Group(); sh.position.set(sx * (fem ? 0.9 : 1.0), SHOULDER_Y, -0.02); sh.rotation.set(0, -sx * 0.17, sx * 0.1); piv.add(sh);
    const arm = new THREE.Group(); arm.rotation.x = -1.1; sh.add(arm);
    mesh(arm, new THREE.SphereGeometry(fem ? 0.23 : 0.26, 18, 12), sleeveMat, 0, -0.08, 0);
    if (shortSleeve) {
      // a short sleeve, then bare upper arm
      const sl = mesh(arm, new THREE.CylinderGeometry(0.3, 0.31, 0.62, 18, 1, true), sleeveMat, 0, -0.33, 0); (sl.material as THREE.Material).side = THREE.DoubleSide;
      capsuleBetween(arm, [0, -0.1, 0], [0, -1.3, 0], 0.25, 0.21, handSkin);
    } else capsuleBetween(arm, [0, -0.05, 0], [0, -1.3, 0], fem ? 0.25 : 0.3, fem ? 0.2 : 0.24, sleeveMat);
    const el = new THREE.Group(); el.position.y = -1.3; el.rotation.x = -0.45; arm.add(el);
    const foreMat = shortSleeve ? handSkin : sleeveMat;
    capsuleBetween(el, [0, 0, 0], [0, -1.18, 0], fem ? 0.19 : 0.22, fem ? 0.13 : 0.16, foreMat);
    if (robe) { const cuff = mesh(el, new THREE.CylinderGeometry(0.3, 0.22, 0.6, 18, 1, true), sleeveMat, 0, -0.9, 0); (cuff.material as THREE.Material).side = THREE.DoubleSide; }
    if (p.top === 'suit') mesh(el, new THREE.CylinderGeometry(0.17, 0.17, 0.1, 14), clothMat(0xf1eee6), 0, -1.12, 0);
    // hand: palm facing down when the forearm rests on the table
    const hand = new THREE.Group(); hand.position.y = -1.2; el.add(hand);
    const palm = mesh(hand, new THREE.CapsuleGeometry(0.15, 0.22, 6, 12), handSkin, 0, -0.2, 0); palm.scale.set(fem ? 1.0 : 1.15, 1, 0.55);
    for (let k = 0; k < 4; k++) {
      const fx = sx * (-0.12 + k * 0.08) * (fem ? 0.9 : 1), len = [0.2, 0.24, 0.23, 0.18][k] * (fem ? 0.95 : 1);
      const fg = new THREE.Group(); fg.position.set(fx, -0.43, 0); fg.rotation.x = 0.45; hand.add(fg);
      const fm = mesh(fg, new THREE.CapsuleGeometry(fem ? 0.035 : 0.042, len, 4, 8), handSkin, 0, -len / 2, 0);
      fm.rotation.x = 0;
    }
    const th = new THREE.Group(); th.position.set(-sx * 0.15, -0.15, 0.02); th.rotation.set(0.3, 0, -sx * 0.7); hand.add(th);
    mesh(th, new THREE.CapsuleGeometry(0.05, 0.2, 4, 8), handSkin, 0, -0.14, 0);
    if (p.watch && sx === -1) { const w = mesh(el, new THREE.TorusGeometry(0.16, 0.035, 8, 20), phys({ color: 0x2a2a2a, metalness: 0.6, roughness: 0.3 }), 0, -1.05, 0); w.rotation.x = Math.PI / 2; mesh(el, new THREE.CylinderGeometry(0.1, 0.1, 0.05, 16), gold, 0, -1.05, 0.17).rotation.x = Math.PI / 2; }
    if (p.sebha && sx === -1) {
      const bead = phys({ color: 0x7a2a12, roughness: 0.25, clearcoat: 1 });
      const sb = new THREE.Group(); sb.position.set(0, -0.25, -0.1); hand.add(sb);
      for (let k = 0; k < 33; k++) { const a = (k / 33) * Math.PI * 2; mesh(sb, new THREE.SphereGeometry(0.035, 8, 6), bead, Math.sin(a) * 0.17, 0, -0.2 - Math.cos(a) * 0.2); }
      mesh(sb, new THREE.CylinderGeometry(0.03, 0.05, 0.18, 8), bead, 0, 0, -0.5).rotation.x = Math.PI / 2;
    }
    arms.push(arm); elbows.push(el); hands.push(hand);
  }

  group.traverse(o => { if ((o as THREE.Mesh).isMesh) o.castShadow = true; });
  mergeStatic(group);
  return { group, piv, head, arms, elbows, hands };
}

/** Fewer draw calls: meshes that share a parent and a material become one mesh. */
function mergeStatic(root: THREE.Object3D) {
  if ((new THREE.BufferGeometry() as unknown as { isBufferGeometry?: unknown }).isBufferGeometry !== true) return;
  const nodes: THREE.Object3D[] = [];
  root.traverse(n => nodes.push(n));
  for (const node of nodes) {
    const byMat = new Map<THREE.Material, THREE.Mesh[]>();
    for (const c of node.children) {
      const m = c as THREE.Mesh;
      if (!m.isMesh || m.children.length) continue;
      const mat = m.material as THREE.Material;
      if (!byMat.has(mat)) byMat.set(mat, []);
      byMat.get(mat)!.push(m);
    }
    for (const [mat, list] of byMat) {
      if (list.length < 2) continue;
      const indexed = list.every(m => m.geometry.index);
      const parts = list.map(m => { m.updateMatrix(); const g = indexed ? m.geometry.clone() : m.geometry.index ? m.geometry.toNonIndexed() : m.geometry.clone(); g.applyMatrix4(m.matrix); return g; });
      const idx: number[] = [];
      const total = parts.reduce((n, g) => n + g.getAttribute('position').count, 0);
      const pos = new Float32Array(total * 3), nor = new Float32Array(total * 3), uv = new Float32Array(total * 2);
      let o = 0;
      for (const g of parts) {
        const n = g.getAttribute('position').count;
        pos.set(g.getAttribute('position').array as Float32Array, o * 3);
        if (g.getAttribute('normal')) nor.set(g.getAttribute('normal').array as Float32Array, o * 3);
        if (g.getAttribute('uv')) uv.set(g.getAttribute('uv').array as Float32Array, o * 2);
        if (indexed) { const ix = g.index!.array; for (let k = 0; k < ix.length; k++) idx.push(ix[k] + o); }
        o += n; g.dispose();
      }
      const geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
      geo.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
      geo.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
      if (indexed) geo.setIndex(idx);
      const merged = new THREE.Mesh(geo, mat); merged.castShadow = true;
      for (const m of list) { node.remove(m); m.geometry.dispose(); }
      node.add(merged);
    }
  }
}

/* ---------- the regulars ---------- */
export const PEOPLE: PersonSpec[] = [
  // عم فتحي: an old-school effendi in a tarboosh, suit and glasses
  { skin: 0xa77a5e, sex: 'm', age: 2, hair: 0xd8d4cc, hairStyle: 'short', brows: 0xb9b2a6, mustache: 'thick', stubble: 0.25,
    headwear: 'tarboosh', top: 'suit', cloth: 0xf1eee6, cloth2: 0x4e4238, pants: 0x4e4238, shoes: 0x2a1a10, glasses: true, belly: 0.6, swatch: 0x9e1b17 },
  // كابتن سامي: a retired football captain, thick black moustache, navy polo, watch
  { skin: 0x8f654b, sex: 'm', age: 1, hair: 0x2c231d, hairStyle: 'receding', brows: 0x1a120c, mustache: 'thick', stubble: 0.35,
    top: 'polo', cloth: 0x24406a, pants: 0x7d6d55, shoes: 0x3a2618, watch: true, belly: 0.4, swatch: 0x24406a },
  // زيكو: a young man from the neighbourhood, curly hair and a short beard
  { skin: 0x7d5642, sex: 'm', age: 0, hair: 0x1f1610, hairStyle: 'curly', brows: 0x140e0a, mustache: 'trim', stubble: 0.95,
    top: 'tee', cloth: 0x2f7d5d, pants: 0x34465e, shoes: 0xe9e6e0, swatch: 0x2f7d5d },
  // منى: a mother of three in a terracotta hijab
  { skin: 0xb98c70, sex: 'f', age: 1, hair: 0x1e130c, hairStyle: 'none', brows: 0x24160d, headwear: 'hijab', scarf: 0x6b2a32,
    top: 'blouse', cloth: 0x3d3350, pants: 0x2a2433, shoes: 0x1e1814, swatch: 0xb5523b },
  // ندى: a university student with long dark hair
  { skin: 0xc79b7e, sex: 'f', age: 0, hair: 0x2a180f, hairStyle: 'long', brows: 0x2a180f, top: 'blouse', cloth: 0xd9a441,
    pants: 0x3a4a66, shoes: 0xd8cfc2, earrings: true, iris: '#5a3a1c', swatch: 0xd9a441 },
  // الحاج محمود: a Saidi elder in a striped galabeya and white emma, prayer beads in hand
  { skin: 0x825a42, sex: 'm', age: 2, hair: 0xd8d4cc, hairStyle: 'short', brows: 0xcfc8bc, mustache: 'thick', beard: 'full', stubble: 0.3,
    headwear: 'emma', top: 'galabeya', cloth: 0xd9d1c0, cloth2: 0x8a7f6c, pattern: 'stripe', pants: 0xd9d1c0, shoes: 0x5a3a22, sebha: true, belly: 0.3, swatch: 0x8a7f6c },
];
