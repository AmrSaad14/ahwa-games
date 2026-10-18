import * as THREE from 'three';
import type { BoardView } from '../../shared/protocol';
import { BAR, OFF } from '../../shared/games/tawla';
import { COLOR_OFFSET, HOME, LAP, TRACK } from '../../shared/games/ludo';
import { TEX_HI, canvasTex } from './ahwa';
import { CHESS_LOOKS, ChessLook, DICE_LOOKS, DiceLook, LUDO_LOOKS, LudoLook, TAWLA_LOOKS, TawlaLook } from './looks';
import { IS_PHONE, clearDisposing, onTop } from './perf';
/** Board artwork: drawn at the device's texture detail (2x where there is memory for it). */
const hiTex = (w: number, h: number, draw: (g: CanvasRenderingContext2D, w: number, h: number) => void) => canvasTex(w, h, draw, undefined, TEX_HI);

/** What the player has selected and where it can go (chess squares, tawla points, ludo pawns). */
export interface BoardUi { sel: number | null; targets: number[]; movable?: number[] }
export type Pick = { kind: 'square'; sq: number } | { kind: 'point'; p: number } | { kind: 'pawn'; i: number } | { kind: 'dice' };

export interface BoardRenderer {
  root: THREE.Group;
  pickables: THREE.Object3D[];
  update(v: BoardView, ui: BoardUi, clock: number): void;
  pick(hit: THREE.Intersection): Pick | null;
  tick(t: number): void;
  /** Drag and drop: hold the piece at `from` over a world point, or put it back with null. */
  carry?(from: Pick, world: THREE.Vector3 | null): void;
  /** Where the last dragged piece was dropped, so the move that follows is not animated again. */
  dropped?(to: Pick): void;
  /** World position of a square, point or pawn (used by the how-to-play recorder). */
  spot?(p: Pick): THREE.Vector3 | null;
}

const ease = (k: number) => 1 - Math.pow(1 - Math.min(1, Math.max(0, k)), 3);
const shadow = <T extends THREE.Object3D>(o: T) => { o.traverse(c => { if ((c as THREE.Mesh).isMesh) { c.castShadow = true; c.receiveShadow = true; } }); return o; };
/** Highlights and rings drawn on a board: always over the printed card. */
const lift = (m: THREE.Mesh) => { onTop(m.material as THREE.Material); return m; };
function overlay(size: number, color: number, opacity: number) {
  const m = new THREE.Mesh(new THREE.PlaneGeometry(size, size), new THREE.MeshBasicMaterial({ color, transparent: true, opacity, depthWrite: false }));
  m.rotation.x = -Math.PI / 2; return m;
}

/* ---------------- realistic materials ---------------- */
// A warm "ahwa" reflection map (overhead lamp, doorway daylight) so polished wood, lacquer and plastic catch light.
let ENV: THREE.Texture | null = null;
const envMats = new Set<THREE.MeshStandardMaterial>();
export function phys(p: THREE.MeshPhysicalMaterialParameters & { envMapIntensity?: number }) {
  const m = new THREE.MeshPhysicalMaterial(p);
  if (ENV) m.envMap = ENV;
  envMats.add(m);
  return m;
}
/** Called once by the scene when the renderer exists. */
export function initBoardEnvironment(renderer: THREE.WebGLRenderer) {
  if (ENV) return;
  const gen = new THREE.PMREMGenerator(renderer);
  const s = new THREE.Scene();
  s.add(new THREE.Mesh(new THREE.BoxGeometry(20, 10, 20), new THREE.MeshBasicMaterial({ color: 0x24170e, side: THREE.BackSide })));
  const panel = (x: number, y: number, z: number, w: number, h: number, c: number, k: number) => {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshBasicMaterial({ color: new THREE.Color(c).multiplyScalar(k), side: THREE.DoubleSide }));
    m.position.set(x, y, z); m.lookAt(0, 0, 0); s.add(m);
  };
  panel(0, 4.9, 0, 3.5, 3.5, 0xffd49a, 2);      // lamp over the table
  panel(9.9, 2.5, -3, 3, 5, 0xfff0d8, 2.5);     // sunlit doorway
  panel(-9.9, 3, 4, 2, 2, 0xffb070, 1.4);       // wall lamp
  panel(0, 2, -9.9, 7, 1.4, 0xffc890, 0.7);     // tea counter
  ENV = gen.fromScene(s, 0.04).texture;
  gen.dispose();
  envMats.forEach(m => { m.envMap = ENV; m.needsUpdate = true; });
}

const rand = (a: number, b: number) => a + Math.random() * (b - a);
/** Wood grain on a canvas: long wavy fibres, growth rings and pores. */
/** Polished stone: a base colour with soft veins. */
function marbleCanvas(size: number, base: string, light: string, vein: string) {
  const c = document.createElement('canvas'); c.width = c.height = size;
  const g = c.getContext('2d')!;
  const bg = g.createLinearGradient(0, 0, size, size); bg.addColorStop(0, light); bg.addColorStop(0.5, base); bg.addColorStop(1, light);
  g.fillStyle = bg; g.fillRect(0, 0, size, size);
  g.strokeStyle = vein; g.lineCap = 'round';
  for (let i = 0; i < 9; i++) {
    g.lineWidth = 1 + Math.random() * 3; g.beginPath();
    let x = Math.random() * size, y = 0; g.moveTo(x, y);
    while (y < size) { x += (Math.random() - 0.5) * 60; y += 20 + Math.random() * 40; g.lineTo(x, y); }
    g.stroke();
  }
  return c;
}
/** A plain colour with a faint print texture (tournament boards). */
function flatCanvas(size: number, base: string, speck: string) {
  const c = document.createElement('canvas'); c.width = c.height = size;
  const g = c.getContext('2d')!;
  g.fillStyle = base; g.fillRect(0, 0, size, size);
  g.globalAlpha = 0.18; g.fillStyle = speck;
  for (let i = 0; i < 2500; i++) g.fillRect(Math.random() * size, Math.random() * size, 2, 2);
  g.globalAlpha = 1;
  return c;
}
function woodCanvas(w: number, h: number, base: string, dark: string, light: string, fibres = 120, wave = 6) {
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  const g = c.getContext('2d')!;
  g.fillStyle = base; g.fillRect(0, 0, w, h);
  const ph = Math.random() * 10;
  for (let i = 0; i < fibres; i++) {
    const y0 = Math.random() * h, amp = rand(0.5, wave), f = rand(0.004, 0.012);
    g.strokeStyle = Math.random() < 0.6 ? dark : light;
    g.globalAlpha = rand(0.05, 0.28); g.lineWidth = rand(0.6, 3.2);
    g.beginPath();
    for (let x = -10; x <= w + 10; x += 8) {
      const y = y0 + Math.sin(x * f + ph + i * 0.37) * amp + Math.sin(x * f * 3.1 + i) * amp * 0.25;
      x < 0 ? g.moveTo(x, y) : g.lineTo(x, y);
    }
    g.stroke();
  }
  g.globalAlpha = 0.12; g.fillStyle = dark;
  for (let i = 0; i < w * h / 180; i++) g.fillRect(Math.random() * w, Math.random() * h, rand(2, 7), 1);
  g.globalAlpha = 1;
  return c;
}
function texOf(c: HTMLCanvasElement, repeat?: [number, number]) {
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = IS_PHONE ? 8 : 16;
  if (repeat) { t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(repeat[0], repeat[1]); }
  return t;
}
const woodTex = (base: string, dark: string, light: string, repeat?: [number, number], size = 512) => texOf(woodCanvas(size, size, base, dark, light), repeat);

/** A smooth curve through profile points (Catmull-Rom), for turned pieces. */
function smooth(pts: [number, number][], steps = 6): THREE.Vector2[] {
  const out: THREE.Vector2[] = [];
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[Math.max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.min(pts.length - 1, i + 2)];
    for (let k = 0; k < steps; k++) {
      const t = k / steps, t2 = t * t, t3 = t2 * t;
      const f = (a: number, b: number, c: number, d: number) => 0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
      out.push(new THREE.Vector2(Math.max(0, f(p0[0], p1[0], p2[0], p3[0])), f(p0[1], p1[1], p2[1], p3[1])));
    }
  }
  const last = pts[pts.length - 1]; out.push(new THREE.Vector2(last[0], last[1]));
  return out;
}
/** Phones: turned pieces with fewer facets (the difference is invisible at phone size, the GPU load is not). */
const DETAIL = IS_PHONE ? 0.6 : 1;
const turned = (pts: [number, number][], seg = 40, steps = 6) => new THREE.LatheGeometry(smooth(pts, Math.max(3, Math.round(steps * DETAIL))), Math.max(18, Math.round(seg * DETAIL)));

/** A cube with rounded edges and corners (keeps the six face groups for per-face materials). */
function roundedBox(size: number, r: number, seg = 5) {
  const g = new THREE.BoxGeometry(size, size, size, seg, seg, seg);
  const p = g.attributes.position, a = size / 2 - r, v = new THREE.Vector3(), c = new THREE.Vector3();
  for (let i = 0; i < p.count; i++) {
    v.fromBufferAttribute(p, i);
    c.set(Math.max(-a, Math.min(a, v.x)), Math.max(-a, Math.min(a, v.y)), Math.max(-a, Math.min(a, v.z)));
    v.sub(c).normalize().multiplyScalar(r).add(c);
    p.setXYZ(i, v.x, v.y, v.z);
  }
  g.computeVertexNormals();
  return g;
}

/* ---------------- dice (tawla and ludo) ---------------- */
const dieMats = new Map<string, THREE.MeshPhysicalMaterial>();
/** The equipped dice skin (null: the classic ivory / white dice). */
let diceLook: DiceLook | null = null, diceKey = 'classic';
export function setDiceLook(id: string) { diceLook = DICE_LOOKS[id] ?? null; diceKey = diceLook ? id : 'classic'; }
const PIPS: Record<number, [number, number][]> = { 1: [[1, 1]], 2: [[0, 0], [2, 2]], 3: [[0, 0], [1, 1], [2, 2]], 4: [[0, 0], [2, 0], [0, 2], [2, 2]], 5: [[0, 0], [2, 0], [1, 1], [0, 2], [2, 2]], 6: [[0, 0], [2, 0], [0, 1], [2, 1], [0, 2], [2, 2]] };
function dieFace(v: number, ivory: boolean) {
  const key = v + (ivory ? 'i' : 'w') + diceKey, look = diceLook;
  let m = dieMats.get(key);
  if (!m) {
    const draw = (bump: boolean) => {
      const c = document.createElement('canvas'); c.width = c.height = 256;
      const g = c.getContext('2d')!;
      if (bump) { g.fillStyle = '#fff'; g.fillRect(0, 0, 256, 256); }
      else {
        const bg = g.createRadialGradient(128, 128, 30, 128, 128, 190);
        bg.addColorStop(0, look ? look.bg[0] : ivory ? '#f6eedb' : '#fbfaf6'); bg.addColorStop(1, look ? look.bg[1] : ivory ? '#e6d8b8' : '#ebe8e0');
        g.fillStyle = bg; g.fillRect(0, 0, 256, 256);
      }
      for (const [x, y] of PIPS[v]) {
        const cx = 58 + x * 70, cy = 58 + y * 70, r = v === 1 ? 30 : 22;
        if (bump) { const gr = g.createRadialGradient(cx, cy, 0, cx, cy, r); gr.addColorStop(0, '#000'); gr.addColorStop(0.8, '#222'); gr.addColorStop(1, '#fff'); g.fillStyle = gr; }
        else {
          const gr = g.createRadialGradient(cx - r * 0.3, cy - r * 0.3, 1, cx, cy, r);
          const col = look ? (v === 1 && look.one ? look.one : look.pip) : v === 1 && !ivory ? ['#d23a2a', '#7a120c'] : ['#3a2a20', '#0b0705'];
          gr.addColorStop(0, col[0]); gr.addColorStop(1, col[1]); g.fillStyle = gr;
        }
        g.beginPath(); g.arc(cx, cy, r, 0, Math.PI * 2); g.fill();
      }
      return c;
    };
    const map = texOf(draw(false)), bumpMap = new THREE.CanvasTexture(draw(true));
    m = phys({ map, bumpMap, bumpScale: 2.5, roughness: look?.rough ?? 0.28, metalness: look?.metal ?? 0, clearcoat: 0.8, clearcoatRoughness: 0.18, envMapIntensity: look?.metal ? 1.2 : 0.7 });
    dieMats.set(key, m);
  }
  return m;
}
const dieGeo = roundedBox(1, 0.16, 6);
/** A die showing `v` on top. Face order of a box: +x, -x, +y (top), -y, +z, -z. */
function makeDie(v: number, ivory = true) {
  const others = [1, 2, 3, 4, 5, 6].filter(x => x !== v && x !== 7 - v);
  const f = (n: number) => dieFace(n, ivory);
  const mesh = new THREE.Mesh(dieGeo, [f(others[0]), f(7 - others[0]), f(v), f(7 - v), f(others[1]), f(7 - others[1])]);
  return shadow(mesh);
}

/* =================================================================== chess */

const SQ = 1.05;
const pieceMats = {
  // polished boxwood and ebony, as on a club Staunton set
  w: phys({ map: woodTex('#e3c592', '#b98d55', '#f3dfb6', [1, 2], 256), roughness: 0.38, clearcoat: 0.7, clearcoatRoughness: 0.22, envMapIntensity: 0.85 }),
  b: phys({ map: woodTex('#1d130e', '#0a0604', '#3a281d', [1, 2], 256), roughness: 0.32, clearcoat: 1, clearcoatRoughness: 0.12, envMapIntensity: 1 }),
};
const feltMat = new THREE.MeshStandardMaterial({ color: 0x1f4a2c, roughness: 1 });
// profile shared by every piece: a stepped base and a bead
const BASE: [number, number][] = [[0, 0], [0.36, 0], [0.385, 0.015], [0.385, 0.055], [0.365, 0.075], [0.335, 0.09], [0.345, 0.115], [0.33, 0.135], [0.28, 0.16]];
const pieceGeo: Record<string, { geo: THREE.BufferGeometry; dark?: boolean }[]> = {};
function crenels(r1: number, r2: number, y: number, h: number, n = 6) {
  const out: THREE.BufferGeometry[] = [];
  const span = (Math.PI * 2) / n, gap = span * 0.32;
  for (let i = 0; i < n; i++) {
    const a0 = i * span + gap / 2, a1 = (i + 1) * span - gap / 2;
    const sh = new THREE.Shape();
    sh.absarc(0, 0, r2, a0, a1, false);
    sh.absarc(0, 0, r1, a1, a0, true);
    const g = new THREE.ExtrudeGeometry(sh, { depth: h, bevelEnabled: true, bevelSize: 0.008, bevelThickness: 0.008, bevelSegments: 1, curveSegments: 6 });
    g.rotateX(-Math.PI / 2); g.translate(0, y, 0);
    out.push(g);
  }
  return out;
}
function geosFor(t: string) {
  if (pieceGeo[t]) return pieceGeo[t];
  const out: { geo: THREE.BufferGeometry; dark?: boolean }[] = [];
  const add = (geo: THREE.BufferGeometry, dark = false) => out.push({ geo, dark });
  add(new THREE.CylinderGeometry(0.35, 0.35, 0.01, 32).translate(0, 0.004, 0), true);   // baize underneath
  if (t === 'p') add(turned([...BASE, [0.22, 0.2], [0.15, 0.3], [0.12, 0.38], [0.2, 0.41], [0.21, 0.435], [0.11, 0.46], [0.1, 0.48], [0.15, 0.51], [0.175, 0.58], [0.15, 0.66], [0.08, 0.7], [0, 0.71]]));
  if (t === 'r') {
    add(turned([...BASE, [0.27, 0.22], [0.25, 0.4], [0.235, 0.58], [0.25, 0.64], [0.3, 0.68], [0.3, 0.71], [0.27, 0.73], [0.29, 0.76], [0.3, 0.88], [0.22, 0.88], [0.21, 0.84], [0, 0.84]]));
    crenels(0.22, 0.3, 0.88, 0.09).forEach(g => add(g));
  }
  if (t === 'n') {
    add(turned([...BASE, [0.27, 0.22], [0.25, 0.27], [0.3, 0.3], [0.3, 0.33], [0, 0.33]]));
    const s = new THREE.Shape();
    s.moveTo(-0.25, 0); s.lineTo(0.25, 0);
    s.bezierCurveTo(0.22, 0.18, 0.3, 0.36, 0.33, 0.52);       // chest
    s.bezierCurveTo(0.36, 0.7, 0.3, 0.86, 0.18, 0.95);         // back of the head and mane
    s.lineTo(0.16, 1.04); s.lineTo(0.08, 0.97);                  // ear
    s.bezierCurveTo(-0.05, 0.98, -0.2, 0.9, -0.33, 0.72);      // forehead
    s.lineTo(-0.41, 0.6); s.quadraticCurveTo(-0.43, 0.53, -0.36, 0.51);   // muzzle
    s.bezierCurveTo(-0.25, 0.5, -0.14, 0.52, -0.08, 0.46);     // jaw
    s.bezierCurveTo(-0.16, 0.34, -0.26, 0.2, -0.25, 0);         // throat
    const head = new THREE.ExtrudeGeometry(s, { depth: 0.24, bevelEnabled: true, bevelSize: 0.05, bevelThickness: 0.07, bevelSegments: 4, curveSegments: 14 });
    head.translate(0, 0.31, -0.12); head.rotateY(Math.PI / 2);
    // carve it: the head narrows toward the muzzle and is rounded over the top, like a hand-cut knight
    const hp = head.attributes.position;
    for (let i = 0; i < hp.count; i++) {
      const x = hp.getX(i), y = hp.getY(i), z = hp.getZ(i);
      const front = Math.min(1, Math.max(0, (z + 0.05) / 0.45)), top = Math.min(1, Math.max(0, (y - 1.05) / 0.3));
      hp.setX(i, x * (1 - 0.5 * front) * (1 - 0.35 * top));
    }
    head.computeVertexNormals();
    add(head);
    // mane ridge and eyes
    const mane = new THREE.CylinderGeometry(0.04, 0.03, 0.46, 8); mane.rotateX(-0.45); mane.translate(0, 1.0, -0.27);
    add(mane);
    for (const side of [-1, 1]) add(new THREE.SphereGeometry(0.03, 10, 8).translate(side * 0.13, 1.1, 0.12), true);
  }
  if (t === 'b') {
    add(turned([...BASE, [0.24, 0.22], [0.17, 0.36], [0.13, 0.52], [0.12, 0.6], [0.21, 0.63], [0.22, 0.66], [0.12, 0.69], [0.11, 0.71], [0.16, 0.76], [0.19, 0.86], [0.165, 0.97], [0.1, 1.04], [0.05, 1.08], [0.06, 1.1], [0.07, 1.14], [0.04, 1.18], [0, 1.19]]));
    const slit = new THREE.BoxGeometry(0.03, 0.2, 0.36); slit.rotateX(0); slit.rotateZ(-0.6); slit.translate(0.07, 0.94, 0);
    add(slit, true);
  }
  if (t === 'q') {
    add(turned([...BASE, [0.27, 0.22], [0.19, 0.38], [0.14, 0.58], [0.13, 0.72], [0.23, 0.75], [0.24, 0.78], [0.14, 0.81], [0.14, 0.84], [0.2, 0.9], [0.27, 1.0], [0.25, 1.02], [0.14, 1.03], [0.12, 1.08], [0.06, 1.12], [0, 1.13]]));
    for (let i = 0; i < 9; i++) add(new THREE.SphereGeometry(0.045, 10, 8).translate(Math.cos(i * Math.PI * 2 / 9) * 0.25, 1.04, Math.sin(i * Math.PI * 2 / 9) * 0.25));
    add(new THREE.SphereGeometry(0.07, 14, 10).translate(0, 1.18, 0));
  }
  if (t === 'k') {
    add(turned([...BASE, [0.28, 0.22], [0.2, 0.38], [0.15, 0.6], [0.14, 0.76], [0.24, 0.79], [0.25, 0.82], [0.15, 0.85], [0.15, 0.88], [0.22, 0.96], [0.25, 1.03], [0.2, 1.06], [0.13, 1.09], [0.09, 1.13], [0, 1.14]]));
    const v = new THREE.BoxGeometry(0.075, 0.28, 0.075, 1, 1, 1); v.translate(0, 1.27, 0);
    const h = new THREE.BoxGeometry(0.22, 0.075, 0.075); h.translate(0, 1.3, 0);
    add(v); add(h);
  }
  return (pieceGeo[t] = out);
}
const darkerMat = { w: phys({ color: 0x6b4a26, roughness: 0.6 }), b: phys({ color: 0x050302, roughness: 0.6 }) };
function makePiece(p: string) {
  const white = p === p.toUpperCase(), g = new THREE.Group();
  for (const { geo, dark } of geosFor(p.toLowerCase())) {
    const m = new THREE.Mesh(geo, geo.type === 'CylinderGeometry' && dark ? feltMat : dark ? darkerMat[white ? 'w' : 'b'] : pieceMats[white ? 'w' : 'b']);
    g.add(m);
  }
  g.scale.setScalar(0.9);
  return shadow(g);
}

export class ChessBoard implements BoardRenderer {
  root = new THREE.Group();
  pickables: THREE.Object3D[] = [];
  private pieces = new THREE.Group();
  private marks = new THREE.Group();
  private lastK = -1;
  private slide: { obj: THREE.Object3D; from: THREE.Vector3; to: THREE.Vector3; t0: number } | null = null;
  private clock = 0;
  private bySq = new Map<number, THREE.Object3D>();
  private dropTo: { sq: number; at: number } | null = null;

  constructor(look: ChessLook = CHESS_LOOKS.chess_wood) {
    const B = SQ * 8, F = 0.55;   // playing area and frame width
    const [fr0, fr1, fr2] = look.frame;
    // the frame: rosewood with a boxwood stringing line and engraved coordinates
    const frameTex = (() => {
      const px = 1024, c = woodCanvas(px, px, fr0, fr1, fr2, 160, 4), g = c.getContext('2d')!;
      const k = px / (B + 2 * F), inner = F * k, sq = SQ * k;
      g.strokeStyle = '#e8cf9c'; g.lineWidth = 4; g.strokeRect(inner - 9, inner - 9, B * k + 18, B * k + 18);
      g.strokeStyle = '#1a0a05'; g.lineWidth = 2; g.strokeRect(inner - 15, inner - 15, B * k + 30, B * k + 30);
      g.fillStyle = 'rgba(232,207,156,.85)'; g.font = `600 ${Math.round(F * k * 0.42)}px Georgia, serif`; g.textAlign = 'center'; g.textBaseline = 'middle';
      for (let i = 0; i < 8; i++) {
        g.fillText('abcdefgh'[i], inner + sq * (i + 0.5), px - inner / 2); g.fillText('abcdefgh'[i], inner + sq * (i + 0.5), inner / 2);
        g.fillText(String(8 - i), inner / 2, inner + sq * (i + 0.5)); g.fillText(String(8 - i), px - inner / 2, inner + sq * (i + 0.5));
      }
      return texOf(c);
    })();
    const side = phys({ map: woodTex(fr0, fr1, fr2, [2, 1]), roughness: 0.4, clearcoat: 0.6, envMapIntensity: 0.6 });
    const frameTop = phys({ map: frameTex, roughness: 0.4, clearcoat: 0.7, clearcoatRoughness: 0.3, envMapIntensity: 0.35 });
    const frame = new THREE.Mesh(new THREE.BoxGeometry(B + 2 * F, 0.3, B + 2 * F), [side, side, frameTop, side, side, side]);
    frame.position.y = 0.15;
    // inlaid squares: maple and walnut veneers, each with its own piece of grain
    // the squares: wood veneers, plain tournament colours, or polished marble
    const sq = (w: [string, string, string], dark: boolean) => look.kind === 'wood' ? woodCanvas(512, 512, w[0], w[1], w[2], dark ? 110 : 90, 5)
      : look.kind === 'marble' ? marbleCanvas(512, w[0], w[2], dark ? 'rgba(255,255,255,.28)' : 'rgba(90,90,100,.32)') : flatCanvas(512, w[0], w[1]);
    const lightW = sq(look.light, false), darkW = sq(look.dark, true);
    const tex = hiTex(1024, 1024, (g, w) => {
      const c = w / 8;
      for (let r = 0; r < 8; r++) for (let f = 0; f < 8; f++) {
        const src = (r + f) % 2 ? lightW : darkW;
        g.drawImage(src, Math.random() * (512 - c / 2), Math.random() * (512 - c / 2), c / 2, c / 2, f * c, r * c, c, c);
        g.strokeStyle = 'rgba(20,10,5,.35)'; g.lineWidth = 1.5; g.strokeRect(f * c, r * c, c, c);
      }
    });
    const top = new THREE.Mesh(new THREE.PlaneGeometry(B, B), onTop(phys({ map: tex, roughness: 0.58, clearcoat: 0.35, clearcoatRoughness: 0.6, envMapIntensity: 0.3, emissive: 0xffffff, emissiveMap: tex, emissiveIntensity: 0.1 })));   // satin lacquer: a sheen, no hot spot
    top.rotation.x = -Math.PI / 2; top.position.y = 0.302; top.receiveShadow = true;
    frame.receiveShadow = true; frame.castShadow = true;
    this.root.add(frame, top, this.marks, this.pieces);
    this.pickables = [top];
  }

  /** Square to local position: a1 at your bottom-left when you play white. */
  private pos(sq: number) { return new THREE.Vector3(((sq & 7) - 3.5) * SQ, 0.3, (3.5 - (sq >> 3)) * SQ); }

  update(v: BoardView, ui: BoardUi, clock: number) {
    this.clock = clock;
    const c = v.chess; if (!c) return;
    this.root.rotation.y = c.you === 'b' ? Math.PI : 0;
    this.pieces.clear(); clearDisposing(this.marks); this.bySq.clear();
    let moved: THREE.Object3D | null = null;
    c.board.forEach((p, sq) => {
      if (!p) return;
      const g = makePiece(p);
      this.bySq.set(sq, g);
      g.position.copy(this.pos(sq));
      if (p.toLowerCase() === 'n') g.rotation.y = p === 'N' ? Math.PI : 0;   // knights face the opponent
      if (c.last && c.last.to === sq) moved = g;
      this.pieces.add(g);
    });
    const mark = (sq: number, color: number, op: number, size = SQ) => { const m = overlay(size, color, op); m.position.copy(this.pos(sq)); m.position.y = 0.305; this.marks.add(lift(m)); return m; };
    if (c.last) { mark(c.last.from, 0xf2c14e, 0.35); mark(c.last.to, 0xf2c14e, 0.45); }
    if (c.check >= 0) mark(c.check, 0xd33b2c, 0.6);
    if (ui.sel !== null) mark(ui.sel, 0x3aa392, 0.55);
    for (const t of ui.targets) {
      const occupied = !!c.board[t];
      const m = new THREE.Mesh(occupied ? new THREE.RingGeometry(0.38, 0.48, 32) : new THREE.CircleGeometry(0.16, 24),
        new THREE.MeshBasicMaterial({ color: 0x1f6d63, transparent: true, opacity: 0.75, depthWrite: false }));
      m.rotation.x = -Math.PI / 2; m.position.copy(this.pos(t)); m.position.y = 0.31; this.marks.add(lift(m));
    }
    if (v.last && v.last.k !== this.lastK) {
      this.lastK = v.last.k;
      const wasDropped = this.dropTo && this.dropTo.sq === v.last.to && clock - this.dropTo.at < 3;
      this.dropTo = null;
      if (v.last.t === 'move' && moved && v.last.from !== undefined && !wasDropped) this.slide = { obj: moved, from: this.pos(v.last.from), to: this.pos(v.last.to!), t0: clock };
    } else if (this.slide && moved) this.slide.obj = moved;
  }

  carry(from: Pick, world: THREE.Vector3 | null) {
    if (from.kind !== 'square') return;
    const g = this.bySq.get(from.sq); if (!g) return;
    if (this.slide?.obj === g) this.slide = null;
    if (!world) { g.position.copy(this.pos(from.sq)); return; }
    const l = this.root.worldToLocal(world.clone());
    g.position.set(l.x, 0.95, l.z);
  }
  dropped(to: Pick) { if (to.kind === 'square') this.dropTo = { sq: to.sq, at: this.clock }; }
  spot(p: Pick) { return p.kind === 'square' ? this.root.localToWorld(this.pos(p.sq).setY(0.5)) : null; }

  pick(hit: THREE.Intersection): Pick | null {
    const local = this.root.worldToLocal(hit.point.clone());
    const f = Math.floor(local.x / SQ + 4), r = Math.floor(4 - local.z / SQ);
    if (f < 0 || f > 7 || r < 0 || r > 7) return null;
    return { kind: 'square', sq: r * 8 + f };
  }

  tick(t: number) {
    if (!this.slide) return;
    const k = ease((t - this.slide.t0) / 0.35);
    this.slide.obj.position.lerpVectors(this.slide.from, this.slide.to, k);
    this.slide.obj.position.y = 0.3 + Math.sin(k * Math.PI) * 0.5;
    if (k >= 1) this.slide = null;
  }
}

/* =================================================================== tawla */

const TW = 11.6, TD = 7.8, EDGE = 0.25, TRAY = 1.0, BARW = 0.7;
const COLW = (TW - TRAY - BARW - EDGE * 2) / 12, CR = 0.34;
// turned checkers with a chamfered rim and a groove on top, centred on their middle
const checkerGeo = turned([[0, -0.07], [CR - 0.025, -0.07], [CR, -0.05], [CR, 0.045], [CR - 0.02, 0.07], [CR * 0.78, 0.07], [CR * 0.74, 0.058], [CR * 0.7, 0.07], [CR * 0.3, 0.07], [CR * 0.27, 0.062], [CR * 0.24, 0.07], [0, 0.07]], 40, 3);
const checkerMats = [
  phys({ map: woodTex('#efe3c6', '#d9c49a', '#fbf3e0', [1, 1], 256), roughness: 0.3, clearcoat: 0.9, clearcoatRoughness: 0.15, envMapIntensity: 0.8 }),   // bone
  phys({ map: woodTex('#5e1610', '#300805', '#7e2a1e', [1, 1], 256), roughness: 0.3, clearcoat: 1, clearcoatRoughness: 0.12, envMapIntensity: 0.9 }),     // red-stained walnut
];
const RAIL_H = 0.22, FIELD_Y = 0.401;
let sadafTile: HTMLCanvasElement | null = null;
/** Egyptian mother-of-pearl (sadaf) inlay: eight-point stars and diamonds set in rosewood. */
function sadafCanvas(w: number, h: number) {
  const c = woodCanvas(w, h, '#3a1a10', '#1e0b05', '#58291a', 60, 3), g = c.getContext('2d')!;
  const cell = h * 0.9, n = Math.ceil(w / cell);
  const pearl = (x: number, y: number, r: number) => { const gr = g.createLinearGradient(x - r, y - r, x + r, y + r); gr.addColorStop(0, '#fbf7ee'); gr.addColorStop(0.45, '#dfe9ea'); gr.addColorStop(0.7, '#f3e3d2'); gr.addColorStop(1, '#cfdad8'); return gr; };
  g.strokeStyle = '#c9a25a'; g.lineWidth = Math.max(1, h * 0.03);
  g.strokeRect(h * 0.06, h * 0.06, w - h * 0.12, h - h * 0.12);
  for (let i = 0; i < n; i++) {
    const cx = (i + 0.5) * (w / n), cy = h / 2, r = cell * 0.36;
    g.fillStyle = pearl(cx, cy, r); g.beginPath();
    for (let k = 0; k < 16; k++) { const a = (k * Math.PI) / 8, rr = k % 2 ? r * 0.55 : r; g.lineTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr); }
    g.closePath(); g.fill(); g.stroke();
    g.fillStyle = '#3a1a10'; g.beginPath(); g.arc(cx, cy, r * 0.22, 0, Math.PI * 2); g.fill();
    if (i < n - 1) {
      const dx = (i + 1) * (w / n), d = cell * 0.14;
      g.fillStyle = pearl(dx, cy, d); g.beginPath(); g.moveTo(dx, cy - d * 1.6); g.lineTo(dx + d, cy); g.lineTo(dx, cy + d * 1.6); g.lineTo(dx - d, cy); g.closePath(); g.fill(); g.stroke();
    }
  }
  return c;
}

export class TawlaBoard implements BoardRenderer {
  root = new THREE.Group();
  pickables: THREE.Object3D[] = [];
  private checkers = new THREE.Group();
  private marks = new THREE.Group();
  private dice: THREE.Mesh[] = [];
  private rollAt = -10;
  private lastK = -1;
  private arrive: { obj: THREE.Object3D; from: THREE.Vector3; to: THREE.Vector3; t0: number } | null = null;
  private topAt = new Map<number, { obj: THREE.Object3D; home: THREE.Vector3 }>();   // the top checker of each point (and the bar)
  private dropTo: { p: number; at: number } | null = null;
  private clock = 0;

  constructor(look: TawlaLook = TAWLA_LOOKS.tawla_walnut) {
    const sideMat = phys({ map: woodTex(look.side[0], look.side[1], look.side[2], [3, 1]), roughness: 0.4, clearcoat: 0.7, envMapIntensity: 0.6 });
    const box = new THREE.Mesh(new THREE.BoxGeometry(TW, FIELD_Y, TD), sideMat);
    box.position.y = FIELD_Y / 2;
    // the playing fields: olive-wood veneer with inlaid points of walnut and red-stained wood
    const fieldW = woodCanvas(1024, 1024, look.field[0], look.field[1], look.field[2], 140, 5);
    const darkW = woodCanvas(512, 512, look.pointA[0], look.pointA[1], look.pointA[2], 90, 4), redW = woodCanvas(512, 512, look.pointB[0], look.pointB[1], look.pointB[2], 90, 4);
    const tex = hiTex(1392, 936, (g, w, h) => {
      const sx = w / TW, x0 = (x: number) => (x + TW / 2) * sx;
      g.fillStyle = look.side[1]; g.fillRect(0, 0, w, h);
      const fieldL = x0(-TW / 2 + EDGE), barL = x0(this.barX() - BARW / 2), barR = x0(this.barX() + BARW / 2), fieldR = x0(TW / 2 - EDGE - TRAY);
      g.drawImage(fieldW, 0, 0, 1024, 1024, fieldL, EDGE * sx, barL - fieldL, h - 2 * EDGE * sx);
      g.drawImage(fieldW, 0, 0, 1024, 1024, barR, EDGE * sx, fieldR - barR, h - 2 * EDGE * sx);
      for (let c = 0; c < 12; c++) {
        const cx = x0(this.colX(c)), half = (COLW / 2) * sx * 0.92, len = TD * 0.42 * sx;
        for (const bottom of [true, false]) {
          const src = (c + (bottom ? 0 : 1)) % 2 ? redW : darkW;
          const yb = bottom ? h - EDGE * sx : EDGE * sx, yt = bottom ? yb - len : yb + len;
          g.save(); g.beginPath(); g.moveTo(cx - half, yb); g.lineTo(cx + half, yb); g.lineTo(cx, yt); g.closePath(); g.clip();
          g.drawImage(src, Math.random() * 200, Math.random() * 200, 300, 300, cx - half, Math.min(yb, yt), half * 2, len);
          g.restore();
          g.strokeStyle = look.line; g.lineWidth = 2; g.beginPath(); g.moveTo(cx - half, yb); g.lineTo(cx, yt); g.lineTo(cx + half, yb); g.stroke();
        }
      }
      // the bearing-off tray is lined with dark felt
      g.fillStyle = look.tray; g.fillRect(x0(TW / 2 - EDGE - TRAY + 0.05), EDGE * sx, (TRAY - 0.1) * sx, h - 2 * EDGE * sx);
    });
    const top = new THREE.Mesh(new THREE.PlaneGeometry(TW, TD), onTop(phys({ map: tex, roughness: 0.6, clearcoat: 0.3, clearcoatRoughness: 0.6, envMapIntensity: 0.25, emissive: 0xffffff, emissiveMap: tex, emissiveIntensity: 0.1 })));   // satin lacquer: a sheen, no hot spot
    top.rotation.x = -Math.PI / 2; top.position.y = FIELD_Y; top.receiveShadow = true;
    // raised rails with sadaf inlay on top: the four sides, the middle bar and the tray divider
    const rails = new THREE.Group();
    const rail = (x: number, z: number, w: number, d: number) => {
      const long = Math.max(w, d), short = Math.min(w, d);
      const inlay = texOf(sadafTile ??= sadafCanvas(1024, 256), [Math.max(1, long / (short * 4)), 1]);
      if (d > w) { inlay.center.set(0.5, 0.5); inlay.rotation = Math.PI / 2; }
      const topMat = phys({ map: inlay, roughness: 0.3, clearcoat: 1, clearcoatRoughness: 0.1, envMapIntensity: 0.9 });
      const m = new THREE.Mesh(new THREE.BoxGeometry(w, RAIL_H, d), [sideMat, sideMat, topMat, sideMat, sideMat, sideMat]);
      m.position.set(x, FIELD_Y + RAIL_H / 2, z); rails.add(m);
    };
    rail(0, TD / 2 - EDGE / 2, TW, EDGE); rail(0, -TD / 2 + EDGE / 2, TW, EDGE);
    rail(-TW / 2 + EDGE / 2, 0, EDGE, TD - 2 * EDGE); rail(TW / 2 - EDGE / 2, 0, EDGE, TD - 2 * EDGE);
    rail(this.barX(), 0, BARW, TD - 2 * EDGE);
    rail(TW / 2 - EDGE - TRAY, 0, 0.08, TD - 2 * EDGE);
    // brass hinges on the bar
    const brass = phys({ color: 0xc9a25a, metalness: 1, roughness: 0.3, envMapIntensity: 1.2 });
    for (const z of [-TD / 4, TD / 4]) { const hg = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 0.7, 16), brass); hg.rotation.x = Math.PI / 2; hg.position.set(this.barX(), FIELD_Y + RAIL_H + 0.02, z); rails.add(hg); }
    this.root.add(shadow(box), top, shadow(rails), this.checkers, this.marks);
    this.pickables = [top];
  }

  private barX() { return -TW / 2 + EDGE + 6 * COLW + BARW / 2; }
  private trayX() { return TW / 2 - EDGE - TRAY / 2; }
  private colX(c: number) { return -TW / 2 + EDGE + COLW * (c + 0.5) + (c >= 6 ? BARW : 0); }
  /** Where the k-th checker of a stack on point i sits. */
  private stackPos(i: number, k: number, n: number) {
    const bottom = i <= 11, c = bottom ? 11 - i : i - 12;
    // like real players: five in a row along the point, further checkers piled on top of them
    const row = k % 5, layer = Math.floor(k / 5);
    const z = bottom ? TD / 2 - EDGE - CR - row * CR * 2 : -TD / 2 + EDGE + CR + row * CR * 2;
    return new THREE.Vector3(this.colX(c), 0.47 + layer * 0.145, z);
  }
  private barPos(p: number, k: number) { return new THREE.Vector3(this.barX(), FIELD_Y + RAIL_H + 0.08 + k * 0.15, p === 0 ? 0.7 : -0.7); }
  private offPos(p: number, k: number) { return new THREE.Vector3(this.trayX(), 0.42 + 0.0, (p === 0 ? TD / 2 - EDGE - 0.15 : -TD / 2 + EDGE + 0.15) + (p === 0 ? -1 : 1) * k * 0.16); }

  update(v: BoardView, ui: BoardUi, clock: number) {
    const s = v.tawla; if (!s) return;
    this.clock = clock; this.lastView = s;
    this.root.rotation.y = s.you === 1 ? Math.PI : 0;
    this.checkers.clear(); clearDisposing(this.marks); this.topAt.clear();
    const add = (p: number, pos: THREE.Vector3, flat = true) => {
      const m = new THREE.Mesh(checkerGeo, checkerMats[p]); m.position.copy(pos); m.castShadow = true;
      if (!flat) { m.rotation.x = Math.PI / 2; m.scale.set(1, 0.6, 1); }
      this.checkers.add(m); return m;
    };
    let arrived: THREE.Object3D | null = null;
    const lastTo = v.last?.t === 'step' ? v.last.to : undefined;
    for (let i = 0; i < 24; i++) {
      const n = Math.abs(s.pts[i]), p = s.pts[i] > 0 ? 0 : 1;
      const trapped = s.pinned?.[i] ?? -1;
      if (trapped >= 0 && n) {
        // محبوسة: the trapped checker lies under the stack, a little out of line so it shows
        const m = add(trapped, this.stackPos(i, 0, n + 1)); m.position.y -= 0.02;
        m.position.x += 0.06;
      }
      const base = trapped >= 0 && n ? 1 : 0;
      for (let k = 0; k < n; k++) {
        const m = add(p, this.stackPos(i, k + base, n + base));
        if (i === lastTo && k === n - 1) arrived = m;
        if (k === n - 1 && p === s.turn) this.topAt.set(i, { obj: m, home: m.position.clone() });
      }
    }
    for (const p of [0, 1]) {
      for (let k = 0; k < s.bar[p]; k++) { const m = add(p, this.barPos(p, k)); if (p === s.turn && k === s.bar[p] - 1) this.topAt.set(BAR, { obj: m, home: m.position.clone() }); }
      for (let k = 0; k < s.off[p]; k++) { const m = add(p, this.offPos(p, k), false); if (lastTo === OFF && p === v.last?.s && k === s.off[p] - 1) arrived = m; }
    }
    // selection and targets
    const under = (i: number) => (s.pinned && s.pinned[i] >= 0 && s.pts[i] ? 1 : 0);
    const top = (i: number) => (i === BAR ? this.barPos(s.turn, Math.max(0, s.bar[s.turn] - 1)) : this.stackPos(i, Math.max(0, Math.abs(s.pts[i]) - 1) + under(i), Math.abs(s.pts[i]) + under(i)));
    if (ui.sel !== null) {
      const ring = new THREE.Mesh(new THREE.RingGeometry(CR * 0.9, CR * 1.25, 32), new THREE.MeshBasicMaterial({ color: 0x3aa392, transparent: true, opacity: 0.9, depthWrite: false }));
      ring.rotation.x = -Math.PI / 2; ring.position.copy(top(ui.sel)); ring.position.y += 0.09; this.marks.add(lift(ring));
    }
    // before picking: a soft ring on every checker that can move
    for (const f of ui.movable ?? []) {
      const ring = new THREE.Mesh(new THREE.RingGeometry(CR * 0.88, CR * 1.3, 32), new THREE.MeshBasicMaterial({ color: 0xffcf6e, transparent: true, opacity: 0.95, depthWrite: false }));
      ring.rotation.x = -Math.PI / 2; ring.position.copy(top(f)); ring.position.y += 0.09; this.marks.add(lift(ring));
    }
    for (const tgt of ui.targets) {
      const disc = new THREE.Mesh(new THREE.CircleGeometry(CR, 28), new THREE.MeshBasicMaterial({ color: 0xffcf6e, transparent: true, opacity: 0.55, depthWrite: false }));
      disc.rotation.x = -Math.PI / 2;
      if (tgt === OFF) { disc.scale.set(1.3, 2.2, 1); disc.position.copy(this.offPos(s.turn, Math.min(14, s.off[s.turn]))); disc.position.y = 0.43; }
      else { const n = Math.abs(s.pts[tgt]) * (s.pts[tgt] * (s.turn === 0 ? 1 : -1) > 0 ? 1 : 0) + under(tgt); disc.position.copy(this.stackPos(tgt, n, n + 1)); disc.position.y = 0.46; }
      this.marks.add(lift(disc));
    }
    // dice on the right half
    this.dice.forEach(d => this.root.remove(d)); this.dice = [];
    if (s.rolled) {
      const climbing = s.rolled[0] === s.rolled[1] && s.dice.length > 0 && s.dice[0] !== s.rolled[0];
      const [a, b] = climbing ? [s.dice[0], s.dice[0]] : s.rolled;
      const remaining = s.dice.slice();
      [a, b].forEach((val, i) => {
        const d = makeDie(val, true); d.scale.setScalar(0.5);
        d.position.set(this.colX(9) + (i ? 0.45 : -0.45), 0.68, 0);
        // a die that has been played shrinks a little (doubles: each die stands for two moves)
        const usedCount = (a === b ? 4 : 2) - remaining.length;
        const used = a === b ? usedCount >= (i === 0 ? 2 : 4) : !remaining.includes(val);
        if (used) { d.scale.setScalar(0.4); d.position.y = 0.62; }
        this.root.add(d); this.dice.push(d);
      });
    }
    if (v.last && v.last.k !== this.lastK) {
      this.lastK = v.last.k;
      if (v.last.t === 'roll') this.rollAt = clock;
      const wasDropped = this.dropTo && this.dropTo.p === v.last.to && clock - this.dropTo.at < 3;
      this.dropTo = null;
      if (v.last.t === 'step' && arrived && v.last.from !== undefined && !wasDropped) {
        const from = v.last.from === BAR ? this.barPos(v.last.s, 0) : this.stackPos(v.last.from, Math.abs(s.pts[v.last.from]), Math.abs(s.pts[v.last.from]) + 1);
        this.arrive = { obj: arrived, from, to: (arrived as THREE.Object3D).position.clone(), t0: clock };
      }
    } else if (this.arrive && arrived) this.arrive.obj = arrived;
  }

  carry(from: Pick, world: THREE.Vector3 | null) {
    if (from.kind !== 'point') return;
    const c = this.topAt.get(from.p); if (!c) return;
    if (this.arrive?.obj === c.obj) this.arrive = null;
    if (!world) { c.obj.position.copy(c.home); return; }
    const l = this.root.worldToLocal(world.clone());
    c.obj.position.set(l.x, 1.0, l.z);
  }
  dropped(to: Pick) { if (to.kind === 'point') this.dropTo = { p: to.p, at: this.clock }; }
  private lastView: NonNullable<BoardView['tawla']> | null = null;
  spot(p: Pick) {
    const s = this.lastView; if (p.kind !== 'point' || !s) return null;
    let v: THREE.Vector3;
    if (p.p === OFF) v = this.offPos(s.turn, Math.min(14, s.off[s.turn]));
    else if (p.p === BAR) v = this.barPos(s.turn, 0);
    else { const n = Math.abs(s.pts[p.p]); v = this.stackPos(p.p, Math.max(0, n - 1), n); }
    return this.root.localToWorld(v.setY(v.y + 0.1));
  }

  pick(hit: THREE.Intersection): Pick | null {
    const l = this.root.worldToLocal(hit.point.clone());
    if (Math.abs(l.x - this.barX()) < BARW / 2) return { kind: 'point', p: BAR };
    if (l.x > TW / 2 - EDGE - TRAY) return { kind: 'point', p: OFF };
    let best = 0, bd = Infinity;
    for (let c = 0; c < 12; c++) { const d = Math.abs(this.colX(c) - l.x); if (d < bd) { bd = d; best = c; } }
    return { kind: 'point', p: l.z > 0 ? 11 - best : 12 + best };
  }

  tick(t: number) {
    const k = (t - this.rollAt) / 0.7;
    this.dice.forEach((d, i) => {
      if (k < 1) { d.rotation.set(t * (9 + i * 3), t * (7 - i * 2), t * 5); d.position.y = 0.68 + Math.abs(Math.sin(k * Math.PI * 3)) * 0.6 * (1 - k); }
      else d.rotation.set(0, i * 0.4 - 0.2, 0);
    });
    if (this.arrive) {
      const a = ease((t - this.arrive.t0) / 0.35);
      this.arrive.obj.position.lerpVectors(this.arrive.from, this.arrive.to, a);
      this.arrive.obj.position.y += Math.sin(a * Math.PI) * 0.8;
      if (a >= 1) this.arrive = null;
    }
  }
}

/* =================================================================== ludo */

/** Soft round shadow under a pawn (the ludo card takes no cast shadows, see below). */
const BLOB_GEO = new THREE.PlaneGeometry(0.66, 0.66);
const BLOB_MAT = new THREE.MeshBasicMaterial({ transparent: true, depthWrite: false, map: canvasTex(64, 64, (g, w) => {
  const gr = g.createRadialGradient(w / 2, w / 2, 0, w / 2, w / 2, w / 2); gr.addColorStop(0, 'rgba(0,0,0,.5)'); gr.addColorStop(0.55, 'rgba(0,0,0,.28)'); gr.addColorStop(1, 'rgba(0,0,0,0)');
  g.fillStyle = gr; g.fillRect(0, 0, w, w);
}) });

export const LUDO_COLORS = ['#c8402f', '#2f8a4a', '#e0b030', '#2f6fb8'];
/** The colours of the ludo board in use (they change with the board skin); labels use them too. */
let ludoPalette = LUDO_COLORS;
export const ludoColor = (i: number) => ludoPalette[i] ?? LUDO_COLORS[i];
const CELL = 0.7;
/** The 52 track squares as grid cells (column, row), clockwise from red's start. */
const PATH: [number, number][] = (() => {
  const p: [number, number][] = [];
  for (let c = 1; c <= 5; c++) p.push([c, 6]);
  for (let r = 5; r >= 0; r--) p.push([6, r]);
  p.push([7, 0]); p.push([8, 0]);
  for (let r = 1; r <= 5; r++) p.push([8, r]);
  for (let c = 9; c <= 14; c++) p.push([c, 6]);
  p.push([14, 7]); p.push([14, 8]);
  for (let c = 13; c >= 9; c--) p.push([c, 8]);
  for (let r = 9; r <= 14; r++) p.push([8, r]);
  p.push([7, 14]); p.push([6, 14]);
  for (let r = 13; r >= 9; r--) p.push([6, r]);
  for (let c = 5; c >= 0; c--) p.push([c, 8]);
  p.push([0, 7]); p.push([0, 6]);
  return p;
})();
const HOME_COL: [number, number][][] = [
  [1, 2, 3, 4, 5].map(c => [c, 7] as [number, number]),
  [1, 2, 3, 4, 5].map(r => [7, r] as [number, number]),
  [13, 12, 11, 10, 9].map(c => [c, 7] as [number, number]),
  [13, 12, 11, 10, 9].map(r => [7, r] as [number, number]),
];
const BASE_ORIGIN: [number, number][] = [[0, 0], [9, 0], [9, 9], [0, 9]];
const STARS = new Set([8, 21, 34, 47]);

export class LudoBoard implements BoardRenderer {
  root = new THREE.Group();
  pickables: THREE.Object3D[] = [];
  private pawns = new THREE.Group();
  private marks = new THREE.Group();
  private die: THREE.Mesh | null = null;
  private rollAt = -10;
  private lastK = -1;
  private prevPos = new Map<string, THREE.Vector3>();
  private hops: { obj: THREE.Object3D; from: THREE.Vector3; to: THREE.Vector3; t0: number }[] = [];
  private movable: THREE.Object3D[] = [];
  private top!: THREE.Mesh;
  // classic turned pawn: wide foot, slim waist, collar and a round head
  private pawnGeo = [turned([[0, 0], [0.25, 0], [0.265, 0.02], [0.26, 0.05], [0.2, 0.09], [0.14, 0.16], [0.105, 0.26], [0.1, 0.32], [0.16, 0.34], [0.165, 0.365], [0.1, 0.38], [0.09, 0.4], [0.13, 0.43], [0.15, 0.49], [0.13, 0.56], [0.07, 0.6], [0, 0.61]], 36, 5)];
  private pawnMats: THREE.MeshPhysicalMaterial[];

  constructor(look: LudoLook = LUDO_LOOKS.ludo_classic) {
    const COLS = look.colors; ludoPalette = COLS;
    this.pawnMats = COLS.map(c => phys({ color: c, roughness: 0.22, clearcoat: 1, clearcoatRoughness: 0.08, envMapIntensity: 1 }));
    const size = CELL * 15;
    const tex = hiTex(1050, 1050, (g, w) => {
      const c = w / 15;
      g.fillStyle = look.base; g.fillRect(0, 0, w, w);
      // printed card laid on wood: a faint paper fibre texture under the print
      g.globalAlpha = 0.05; for (let i = 0; i < 4000; i++) { g.fillStyle = Math.random() < 0.5 ? '#000' : '#fff'; g.fillRect(Math.random() * w, Math.random() * w, Math.random() * 6, 1); } g.globalAlpha = 1;
      // bases
      BASE_ORIGIN.forEach(([bx, by], i) => {
        g.fillStyle = COLS[i]; g.fillRect(bx * c, by * c, 6 * c, 6 * c);
        g.fillStyle = look.base; g.fillRect((bx + 1) * c, (by + 1) * c, 4 * c, 4 * c);
        for (const [dx, dy] of [[1.5, 1.5], [3.5, 1.5], [1.5, 3.5], [3.5, 3.5]]) { g.fillStyle = COLS[i]; g.beginPath(); g.arc((bx + dx + 0.5) * c, (by + dy + 0.5) * c, c * 0.42, 0, Math.PI * 2); g.fill(); }
      });
      // track cells
      g.strokeStyle = look.line; g.lineWidth = 2;
      PATH.forEach(([x, y], i) => {
        const start = COLOR_OFFSET.indexOf(i);
        g.fillStyle = start >= 0 ? COLS[start] : look.track;
        g.fillRect(x * c, y * c, c, c); g.strokeRect(x * c, y * c, c, c);
        if (STARS.has(i)) star(g, (x + 0.5) * c, (y + 0.5) * c, c * 0.32, look.star);
      });
      HOME_COL.forEach((cells, i) => cells.forEach(([x, y]) => { g.fillStyle = COLS[i]; g.fillRect(x * c, y * c, c, c); g.strokeRect(x * c, y * c, c, c); }));
      // centre home triangles
      const m = 7.5 * c;
      [[[6, 6], [6, 9]], [[6, 6], [9, 6]], [[9, 6], [9, 9]], [[6, 9], [9, 9]]].forEach(([[ax, ay], [bx2, by2]], i) => {
        g.fillStyle = COLS[i]; g.beginPath(); g.moveTo(ax * c, ay * c); g.lineTo(bx2 * c, by2 * c); g.lineTo(m, m); g.fill();
      });
    });
    const walnut = phys({ map: woodTex(look.frame[0], look.frame[1], look.frame[2], [2, 1]), roughness: 0.38, clearcoat: 0.8, clearcoatRoughness: 0.15, envMapIntensity: 0.6 });
    const board = new THREE.Mesh(new THREE.BoxGeometry(size + 0.6, 0.3, size + 0.6), walnut);
    board.position.y = 0.15;
    // base cups: shallow wooden rings each pawn sits in at home
    const cupMat = onTop(phys({ color: 0x000000, transparent: true, opacity: 0.18, roughness: 1 }));
    BASE_ORIGIN.forEach(([bx, by]) => [[1.5, 1.5], [3.5, 1.5], [1.5, 3.5], [3.5, 3.5]].forEach(([dx, dy]) => {
      const cup = new THREE.Mesh(new THREE.RingGeometry(0.2, 0.3, 32), cupMat); cup.rotation.x = -Math.PI / 2;
      cup.position.copy(this.cellPos(bx + dx, by + dy)); cup.position.y = 0.306; this.root.add(cup);
    }));
    // a matte printed card, lit evenly: no lamp glare, no hot spot and no shadows from the room across it,
    // so every colour and square reads the same from corner to corner (pawns get their own soft contact shadow)
    const top = new THREE.Mesh(new THREE.PlaneGeometry(size, size), onTop(phys({ map: tex, roughness: 0.92, metalness: 0, envMapIntensity: 0, emissive: 0xffffff, emissiveMap: tex, emissiveIntensity: 0.42 })));
    top.rotation.x = -Math.PI / 2; top.position.y = 0.301; top.receiveShadow = false;
    this.root.add(shadow(board), top, this.marks, this.pawns);
    this.top = top;
    this.pickables = [top];
  }

  private cellPos(x: number, y: number) { return new THREE.Vector3((x + 0.5 - 7.5) * CELL, 0.3, (y + 0.5 - 7.5) * CELL); }
  private pawnPos(v: NonNullable<BoardView['ludo']>, seat: number, i: number) {
    const color = v.colors[seat], prog = v.pawns[seat][i];
    if (prog === LAP) { const [x, y] = PATH[(COLOR_OFFSET[color] + 51) % TRACK]; return this.cellPos(x, y); }
    if (prog < 0) { const [bx, by] = BASE_ORIGIN[color]; const [dx, dy] = [[1.5, 1.5], [3.5, 1.5], [1.5, 3.5], [3.5, 3.5]][i]; return this.cellPos(bx + dx, by + dy); }
    if (prog <= 50) { const [x, y] = PATH[(COLOR_OFFSET[color] + prog) % TRACK]; return this.cellPos(x, y); }
    if (prog < HOME) { const [x, y] = HOME_COL[color][prog - 51]; return this.cellPos(x, y); }
    const ctr = this.cellPos(7, 7), dir = [[-1, 0], [0, -1], [1, 0], [0, 1]][color];
    return ctr.add(new THREE.Vector3(dir[0] * 0.55 + (i - 1.5) * 0.12 * (dir[1] ? 1 : 0), 0, dir[1] * 0.55 + (i - 1.5) * 0.12 * (dir[0] ? 1 : 0)));
  }

  update(v: BoardView, ui: BoardUi, clock: number) {
    const s = v.ludo; if (!s) return;
    const myColor = s.you >= 0 ? s.colors[s.you] : 3;
    this.root.rotation.y = [Math.PI / 2, Math.PI, Math.PI * 1.5, 0][myColor];   // your base at the bottom-left
    this.pawns.clear(); clearDisposing(this.marks); this.movable = [];
    const bySpot = new Map<string, number>();
    const newKey = v.last && v.last.k !== this.lastK;
    s.pawns.forEach((arr, seat) => arr.forEach((_, i) => {
      const pos = this.pawnPos(s, seat, i);
      const key = `${Math.round(pos.x * 10)},${Math.round(pos.z * 10)}`;
      const k = bySpot.get(key) ?? 0; bySpot.set(key, k + 1);
      if (k && s.pawns[seat][i] !== -1) pos.add(new THREE.Vector3((k % 2 ? 1 : -1) * 0.14, 0, (k > 1 ? 0.14 : -0.14)));
      const g = new THREE.Group();
      const mat = this.pawnMats[s.colors[seat]];
      for (const geo of this.pawnGeo) g.add(new THREE.Mesh(geo, mat));
      g.position.copy(pos); shadow(g);
      const blob = new THREE.Mesh(BLOB_GEO, BLOB_MAT); blob.rotation.x = -Math.PI / 2; blob.position.y = 0.004; blob.renderOrder = 1; g.add(blob);
      g.userData = { seat, i };
      const id = `${seat}:${i}`, prev = this.prevPos.get(id);
      if (newKey && prev && prev.distanceTo(pos) > 0.01) this.hops.push({ obj: g, from: prev, to: pos.clone(), t0: clock });
      else { const h = this.hops.find(x => x.obj.userData.seat === seat && x.obj.userData.i === i); if (h) h.obj = g; }
      this.prevPos.set(id, pos.clone());
      if (seat === s.you && ui.targets.includes(i)) {
        this.movable.push(g);
        const ring = new THREE.Mesh(new THREE.RingGeometry(0.24, 0.32, 28), new THREE.MeshBasicMaterial({ color: 0xffcf6e, transparent: true, opacity: 0.9, depthWrite: false }));
        ring.rotation.x = -Math.PI / 2; ring.position.copy(pos); ring.position.y = 0.31; this.marks.add(lift(ring));
      }
      this.pawns.add(g);
    }));
    if (this.die) { this.root.remove(this.die); this.die = null; }
    if (s.die !== null || v.last?.t === 'roll' || v.last?.t === 'pass') {
      const val = s.die ?? v.last?.dice?.[0] ?? 1;
      this.die = makeDie(Math.max(1, Math.min(6, val || 1)), false); this.die.scale.setScalar(0.55);
      this.die.position.copy(this.cellPos(7, 7)); this.die.position.y = 0.62;
      this.root.add(this.die);
    }
    if (newKey) { this.lastK = v.last!.k; if (v.last!.t === 'roll') this.rollAt = clock; }
    this.pickables = [this.top, ...this.movable];
  }

  spot(p: Pick) {
    if (p.kind === 'dice') return this.die ? this.die.getWorldPosition(new THREE.Vector3()) : null;
    if (p.kind !== 'pawn') return null;
    const g = this.movable.find(m => m.userData.i === p.i);
    return g ? g.getWorldPosition(new THREE.Vector3()).setY(0.6) : null;
  }

  pick(hit: THREE.Intersection): Pick | null {
    let o: THREE.Object3D | null = hit.object;
    while (o && o.userData.i === undefined) o = o.parent;
    if (o && this.movable.includes(o)) return { kind: 'pawn', i: o.userData.i };
    // tap near a movable pawn
    let best: THREE.Object3D | null = null, bd = 0.6;
    for (const m of this.movable) { const d = m.getWorldPosition(new THREE.Vector3()).distanceTo(hit.point); if (d < bd) { bd = d; best = m; } }
    if (best) return { kind: 'pawn', i: best.userData.i };
    if (this.die && this.die.getWorldPosition(new THREE.Vector3()).distanceTo(hit.point) < 0.9) return { kind: 'dice' };
    return null;
  }

  tick(t: number) {
    if (this.die) {
      const k = (t - this.rollAt) / 0.7;
      if (k < 1) { this.die.rotation.set(t * 11, t * 8, t * 6); this.die.position.y = 0.62 + Math.abs(Math.sin(k * Math.PI * 3)) * 0.7 * (1 - k); }
      else { this.die.rotation.set(0, 0.3, 0); this.die.position.y = 0.62; }
    }
    this.hops = this.hops.filter(h => {
      const a = ease((t - h.t0) / 0.4);
      h.obj.position.lerpVectors(h.from, h.to, a); h.obj.position.y = 0.3 + Math.sin(a * Math.PI) * 0.7;
      return a < 1;
    });
    // movable pawns bob gently so they are easy to spot
    this.movable.forEach((m, i) => { if (!this.hops.some(h => h.obj === m)) m.position.y = 0.3 + Math.abs(Math.sin(t * 4 + i)) * 0.12; });
  }
}

function star(g: CanvasRenderingContext2D, x: number, y: number, r: number, color = '#b9a68a') {
  g.fillStyle = color; g.beginPath();
  for (let i = 0; i < 10; i++) { const a = -Math.PI / 2 + (i * Math.PI) / 5, rr = i % 2 ? r * 0.45 : r; g.lineTo(x + Math.cos(a) * rr, y + Math.sin(a) * rr); }
  g.closePath(); g.fill();
}
