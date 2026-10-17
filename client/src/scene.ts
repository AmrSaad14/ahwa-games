import * as THREE from 'three';
import { End, TILES } from '../../shared/engine';
import { layoutLine, previewPlacement } from '../../shared/layout';
import type { AnyView, TableView } from '../../shared/protocol';
import type { BoardGameId } from '../../shared/games/defs';
import { BoardRenderer, BoardUi, ChessBoard, LudoBoard, Pick, TawlaBoard, initBoardEnvironment, setDiceLook } from './boards';
import { CHESS_LOOKS, LUDO_LOOKS, TAWLA_LOOKS, TILE_LOOKS, TileLook } from './looks';
import { HEAD_Y, PEOPLE, makePerson } from './people';
import { Post } from './post';
import { IS_PHONE, disposeTree } from './perf';
import { FLOOR_Y, Smoke, TEX_HI, buildAhwa, canvasTex, makeChair, makeShisha, makeTable, makeTeaGlass } from './ahwa';

/** The six characters players pick from; the colour stands for them in lists and badges. */
export const CHARS = PEOPLE;
export const charColor = (i: number) => '#' + CHARS[i].swatch.toString(16).padStart(6, '0');

const PIPS: Record<number, number[]> = { 0: [], 1: [4], 2: [0, 8], 3: [0, 4, 8], 4: [0, 2, 6, 8], 5: [0, 2, 4, 6, 8], 6: [0, 2, 3, 5, 6, 8] };

/** Size of one layout unit in the world: a tile is 2 layout units long, so 1 world unit (about 5 cm next to the players). */
const TS = 0.62;
/** Half the side of the small square ahwa table. */
const T = 6;
/** How far the line may run sideways before it turns a corner (layout units). */
const X_MAX = (T - 1.9) / TS;
const SEAT_R = T + 2.0;

/** Visual positions around the table: 0 bottom (you), 1 left, 2 top, 3 right. */
export function positionsFor(n: number): number[] { return n === 2 ? [0, 2] : n === 3 ? [0, 1, 3] : [0, 1, 2, 3]; }

type Anim = { type: 'play' | 'draw' | 'pass' | 'win' | 'emote'; t0: number; d: number };
interface CharData { piv: THREE.Group; head: THREE.Group; arms: THREE.Group[]; elbows: THREE.Group[]; phase: number; anim: Anim | null; active: boolean; ch: number; seat: number }

/* ---------- realistic domino tile ---------- */
const TILE_W = 1.94, TILE_D = 0.96, TILE_H = 0.3;
function roundedRect(w: number, h: number, r: number) {
  const s = new THREE.Shape();
  s.moveTo(-w / 2 + r, -h / 2); s.lineTo(w / 2 - r, -h / 2); s.quadraticCurveTo(w / 2, -h / 2, w / 2, -h / 2 + r);
  s.lineTo(w / 2, h / 2 - r); s.quadraticCurveTo(w / 2, h / 2, w / 2 - r, h / 2); s.lineTo(-w / 2 + r, h / 2);
  s.quadraticCurveTo(-w / 2, h / 2, -w / 2, h / 2 - r); s.lineTo(-w / 2, -h / 2 + r); s.quadraticCurveTo(-w / 2, -h / 2, -w / 2 + r, -h / 2);
  return s;
}
function makeTileBody(): THREE.BufferGeometry {
  const bevel = 0.05;
  const geo = new THREE.ExtrudeGeometry(roundedRect(TILE_W - bevel * 2, TILE_D - bevel * 2, 0.1), {
    depth: TILE_H - bevel * 2, bevelEnabled: true, bevelThickness: bevel, bevelSize: bevel, bevelSegments: 3, curveSegments: 6,
  });
  geo.rotateX(-Math.PI / 2);
  geo.translate(0, -(TILE_H - bevel * 2) / 2, 0);
  geo.computeVertexNormals();
  return geo;
}

const GFX_KEY = 'ahwa.gfx';
/** Starting graphics level: what this device settled on before, else lighter on low-memory phones. */
function initialGfx(): 0 | 1 | 2 {
  try { const s = localStorage.getItem(GFX_KEY); if (s === '0' || s === '1' || s === '2') return Number(s) as 0 | 1 | 2; } catch { /* private mode */ }
  const mem = (navigator as { deviceMemory?: number }).deviceMemory;
  return mem !== undefined && mem <= 3 ? 1 : 2;
}

/** The café room built by buildAhwa (the first group added to the scene). */
const roomGroup = (s: THREE.Scene) => s.children.find(c => c.userData.room);

export class TableScene {
  private renderer: THREE.WebGLRenderer;
  private scene = new THREE.Scene();
  private camera = new THREE.PerspectiveCamera(38, 1, 0.1, 400);
  private boardG = new THREE.Group();   // the line of play, in layout units (scaled)
  private propsG = new THREE.Group();   // tiles held by opponents and the boneyard, in world units
  private ring: THREE.Mesh;
  private chars: (THREE.Group | null)[] = [null, null, null, null];
  private regulars: THREE.Group[] = [];
  private ghosts: THREE.Object3D[] = [];
  private body = makeTileBody();
  private faceGeo = new THREE.PlaneGeometry(TILE_W - 0.08, TILE_D - 0.08).rotateX(-Math.PI / 2);
  private bodyMat = new THREE.MeshStandardMaterial({ color: 0xefe5cf, roughness: 0.28, metalness: 0.0 });
  private ghostMat = new THREE.MeshBasicMaterial({ color: 0xffd27a, transparent: true, opacity: 0.5, depthWrite: false });
  private faceMats = new Map<string, THREE.MeshStandardMaterial>();
  private smoke: Smoke;
  private clock = 0; private lastT = performance.now();
  private dropAt = new Map<number, number>();
  private known = new Set<number>();
  private ray = new THREE.Raycaster();
  private v3 = new THREE.Vector3();
  private view: AnyView | null = null;
  private boards: Partial<Record<BoardGameId, BoardRenderer>> = {};
  /** The player's shop skins: which look each kind uses. */
  private skins = { tiles: 'tiles_ivory', dice: 'dice_ivory', tawla: 'tawla_walnut', chess: 'chess_wood', ludo: 'ludo_classic' };
  private tileLook: TileLook = TILE_LOOKS.tiles_ivory;
  private glasses = new THREE.Group();   // tea glasses on the table corners (moved aside for the big ludo board)
  private board: BoardRenderer | null = null;
  private boardUi: BoardUi = { sel: null, targets: [] };
  onPick: (p: Pick) => void = () => {};
  private sel: number | null = null;
  private choose: End[] | null = null;
  private homeCh = 0;
  private posToSeat: number[] = [-1, -1, -1, -1];
  private yardLayout: { x: number; z: number; r: number; y: number }[] = [];
  private reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  private zoom = 1;                 // camera distance factor (>1 when the player zooms out), 1 = default framing
  /** Player zoom: 1 = the whole table framed; above 1 a lens zoom (sharp, re-rendered) into part of it, below 1 backing off. */
  private mag = 1; private magT = 1;
  /** Where the zoomed-in view looks (screen px into the enlarged picture); 0,0 at the default framing. */
  private pan = { x: 0, y: 0 }; private panT = { x: 0, y: 0 };
  private panning: { id: number; x: number; y: number; moved: boolean } | null = null;
  private pinchMid = { x: 0, y: 0 }; private magStart = 1;
  /** Called when the zoom changes (to show or hide the reset button). */
  onZoom: (zoomed: boolean) => void = () => {};
  /** Screen space covered by the HUD and controls (CSS px); the table is framed in what is left. */
  private insets = { top: 0, right: 0, bottom: 0, left: 0, clear: 0 };
  /** Domino: where the played tiles and the next places to play are (world, on the table), and a key of their extent. */
  private chainPts: THREE.Vector3[] = [];
  private chainKey = '';
  private frameKey = ''; private frame = { d: 20, ox: 0, oy: 0 };
  private spot!: THREE.SpotLight;
  /** Graphics level: 2 full, 1 lighter, 0 lightest. Steps down by itself when frames are slow, and is remembered. */
  private gfx = initialGfx();
  private perf = { sum: 0, n: 0, slow: 0 };
  /** Phones: full frame rate only while something moves or the player touches; 30 fps otherwise (less heat and battery). */
  private busyUntil = 0; private lastDraw = 0; private shadowAt = 0;
  /** Something changes on screen: draw every frame and refresh the shadows for a moment. */
  /** The app is in the background (phone): draw nothing at all. */
  paused = false;
  setPaused(p: boolean) { this.paused = p; if (!p) { this.lastT = performance.now(); this.wake(); } }
  wake(ms = 1500) { this.busyUntil = Math.max(this.busyUntil, performance.now() + ms); }
  /** Film look (glow on highlights, grade, vignette) on the top graphics level. Everything stays sharp. */
  private post: Post | null = null;
  private pinch = new Map<number, { x: number; y: number }>();
  private pinchStart = 0; private zoomStart = 1;
  onGhost: (e: End) => void = () => {};
  /** Board games: may the piece at this spot be dragged? (also selects it) */
  onDragStart: (p: Pick) => boolean = () => false;
  onDrop: (from: Pick, to: Pick | null) => boolean = () => false;
  private press: { id: number; x: number; y: number; from: Pick; dragging: boolean } | null = null;
  private cancelPress() { const pr = this.press; this.press = null; if (pr?.dragging) this.board?.carry?.(pr.from, null); }
  /** The board spot and world point under a screen position. */
  private boardPoint(x: number, y: number): { pick: Pick | null; world: THREE.Vector3 } | null {
    if (!this.board) return null;
    const r = this.canvas.getBoundingClientRect();
    this.ray.setFromCamera(new THREE.Vector2(((x - r.left) / r.width) * 2 - 1, -((y - r.top) / r.height) * 2 + 1), this.camera);
    const hit = this.ray.intersectObjects(this.board.pickables, true)[0];
    return hit ? { pick: this.board.pick(hit), world: hit.point.clone() } : null;
  }
  /** Domino: the open end whose glowing spot is nearest a screen position (for dropping a dragged tile). */
  ghostAt(x: number, y: number, radius = 110): End | null {
    let best: End | null = null, bd = radius;
    const r = this.canvas.getBoundingClientRect();
    for (const g of this.ghosts) {
      const p = g.getWorldPosition(new THREE.Vector3()).project(this.camera);
      const d = Math.hypot(r.left + (p.x + 1) / 2 * r.width - x, r.top + (1 - p.y) / 2 * r.height - y);
      if (d < bd) { bd = d; best = g.userData.end as End; }
    }
    return best;
  }
  onFrame: () => void = () => {};
  /** Called once with a small face picture of each character (data URLs). */
  onPortraits: (urls: string[]) => void = () => {};
  private portraitsDone = false;

  /** Draw each character's face into the corner of the canvas and copy it out, before the first real frame. */
  private makePortraits() {
    this.portraitsDone = true;
    const r = this.renderer, el = (r as unknown as { domElement?: HTMLCanvasElement }).domElement;
    if (!el || !el.height) return;
    try {
      const pr = r.getPixelRatio(), S = 96, px = Math.round(S * pr);
      const sc = new THREE.Scene(); sc.background = new THREE.Color(0x3b2616);
      sc.add(new THREE.HemisphereLight(0xffe2b8, 0x3a2416, 1.1));
      const key = new THREE.DirectionalLight(0xffdcae, 2.2); key.position.set(2, 3, 4); sc.add(key);
      const rim = new THREE.DirectionalLight(0xffc27a, 1.2); rim.position.set(-3, 2, -3); sc.add(rim);
      const cam = new THREE.PerspectiveCamera(26, 1, 0.1, 50);
      const out = document.createElement('canvas'); out.width = out.height = px;
      const g2 = out.getContext('2d')!;
      const size = r.getSize(new THREE.Vector2());
      const urls: string[] = [];
      r.setScissorTest(true); r.setViewport(0, 0, S, S); r.setScissor(0, 0, S, S);
      PEOPLE.forEach((spec, i) => {
        const p = makePerson(spec, String(i));
        sc.add(p.group);
        const hy = HEAD_Y - 1.75 + (spec.headwear === 'tarboosh' || spec.headwear === 'emma' ? 0.12 : 0) - 0.05;
        cam.position.set(0.9, hy + 0.1, 4.1); cam.lookAt(0, hy, 0);
        r.render(sc, cam);
        g2.drawImage(el, 0, el.height - px, px, px, 0, 0, px, px);
        urls.push(out.toDataURL('image/jpeg', 0.88));
        sc.remove(p.group);
        p.group.traverse(o => (o as THREE.Mesh).geometry?.dispose());
      });
      r.setScissorTest(false); r.setViewport(0, 0, size.x, size.y);
      this.onPortraits(urls);
    } catch { /* no faces: the colour dots stay */ }
  }

  constructor(private canvas: HTMLCanvasElement) {
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: IS_PHONE ? 'default' : 'high-performance', stencil: false });
    this.renderer.shadowMap.enabled = true;
    // the shadow map is redrawn only when something moves (see tick), not every frame
    this.renderer.shadowMap.autoUpdate = false;
    this.renderer.info.autoReset = false;
    this.renderer.shadowMap.type = this.gfx === 2 && !IS_PHONE ? THREE.PCFSoftShadowMap : THREE.PCFShadowMap;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.05;
    initBoardEnvironment(this.renderer);
    // the glow pass costs about 7 extra full-screen draws: desktops only (phones keep a sharp, direct picture)
    if (this.gfx === 2 && !IS_PHONE) { try { this.post = new Post(this.renderer); } catch { this.post = null; } }
    const s = this.scene;
    s.background = new THREE.Color(0x24170e);
    s.fog = new THREE.Fog(0x2a1b10, 30, 75);
    // phones skip the room's 4 point lights (each one costs every pixel); a little more ambient light makes up for them
    const lamps = this.gfx === 2 && !IS_PHONE;
    s.add(new THREE.HemisphereLight(0xffe2b8, 0x3a2416, lamps ? 0.9 : 1.15));
    // the bulb over our table
    const spot = new THREE.SpotLight(0xffd59a, 200, 45, 0.78, 0.55, 1.4);
    spot.position.set(0.5, 15, 1.5); spot.target.position.set(0, 0, 0);
    spot.castShadow = true; spot.shadow.mapSize.setScalar(this.gfx === 2 ? (TEX_HI > 1 && (this.renderer.capabilities?.maxTextureSize ?? 0) >= 8192 ? 3072 : 2048) : 1024); this.spot = spot; spot.shadow.bias = -0.0004; spot.shadow.radius = 3;
    s.add(spot, spot.target);
    const fill = new THREE.DirectionalLight(0xffe9cc, 0.35); fill.position.set(-10, 12, 18); s.add(fill);

    const room = buildAhwa(s, lamps);
    this.smoke = new Smoke(s, room.smoke);
    // a couple of regulars at the next tables
    const r1 = this.makeChar(0, true); r1.position.set(-12.5 + 3.6 * Math.sin(0.1), 0, -8.5 + 3.6); r1.lookAt(-12.5, 0, -8.5); s.add(r1);
    const r2 = this.makeChar(3, true); r2.position.set(12.5, 0, -9 - 3.6); r2.lookAt(12.5, 0, -9); s.add(r2);
    const r3 = this.makeChar(1, true); r3.position.set(12.5, 0, -9 + 3.6); r3.lookAt(12.5, 0, -9); s.add(r3);
    this.regulars.push(r1, r2, r3);
    // phones: the neighbouring tables don't draw into the shadow map (it then holds just our table and its players)
    if (!lamps) for (const o of [...this.regulars, roomGroup(s)]) o?.traverse(m => { m.castShadow = false; });

    // our table, tea glasses at the corners and a shisha by the top-right chair
    s.add(makeTable(T));
    for (const [x, z] of [[T - 0.7, T - 0.7], [-T + 0.7, -T + 0.7], [T - 0.7, -T + 0.7]]) { const g = makeTeaGlass(); g.position.set(x, 0, z); this.glasses.add(g); }
    s.add(this.glasses);
    const sh = makeShisha(new THREE.Vector3(-2.6, 2.6, 2.2));
    sh.group.position.set(T + 2.6, FLOOR_Y, -T - 1.2); sh.group.rotation.y = 0.6; s.add(sh.group);

    this.boardG.scale.setScalar(TS);
    s.add(this.boardG, this.propsG);
    this.ring = new THREE.Mesh(new THREE.RingGeometry(0.75, 0.9, 40), new THREE.MeshBasicMaterial({ color: 0xffcf6e, transparent: true, opacity: 0.8, side: THREE.DoubleSide, depthWrite: false }));
    this.ring.rotation.x = -Math.PI / 2; this.ring.position.y = 0.02; this.ring.visible = false; s.add(this.ring);

    canvas.addEventListener('pointerdown', e => {
      const r = canvas.getBoundingClientRect();
      const ndc = new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
      this.ray.setFromCamera(ndc, this.camera);
      if (this.board && this.view && this.view.game !== 'domino') {
        // a tap picks on release; pressing and moving drags the piece instead
        const hit = this.ray.intersectObjects(this.board.pickables, true)[0];
        const p = hit && this.board.pick(hit);
        if (this.press) { this.cancelPress(); return; }   // a second finger: pinch zoom, not a drag
        this.press = p && this.pinch.size === 0 ? { id: e.pointerId, x: e.clientX, y: e.clientY, from: p, dragging: false } : null;
        if (!this.press) this.startPan(e);
        return;
      }
      const hit = this.ghosts.length ? this.ray.intersectObjects(this.ghosts, true)[0] : undefined;
      if (hit) { let o: THREE.Object3D | null = hit.object; while (o && !o.userData.end) o = o.parent; if (o) { this.onGhost(o.userData.end as End); return; } }
      this.startPan(e);
    });
    // zoom: mouse wheel on desktop, two-finger pinch on phones, both toward the point under the pointer
    canvas.addEventListener('wheel', e => {
      e.preventDefault();
      const r = canvas.getBoundingClientRect();
      this.zoomAt(Math.exp(-e.deltaY * 0.0015), e.clientX - r.left, e.clientY - r.top, true);
    }, { passive: false });
    canvas.addEventListener('pointerdown', e => {
      this.pinch.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (this.pinch.size === 2) {
        const [a, b] = [...this.pinch.values()];
        this.pinchStart = Math.hypot(a.x - b.x, a.y - b.y); this.magStart = this.magT;
        this.pinchMid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
        this.panning = null;
      }
    });
    canvas.addEventListener('pointermove', e => {
      if (this.pinch.has(e.pointerId)) {
        this.pinch.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (this.pinch.size === 2 && this.pinchStart > 0) {
          const [a, b] = [...this.pinch.values()], r = canvas.getBoundingClientRect();
          const mid = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
          const want = this.magStart * Math.hypot(a.x - b.x, a.y - b.y) / Math.max(1, this.pinchStart);
          this.zoomAt(want / this.magT, mid.x - r.left, mid.y - r.top, true);
          this.panBy(this.pinchMid.x - mid.x, this.pinchMid.y - mid.y);   // two fingers moving together slide the view
          this.pinchMid = mid;
          return;
        }
      }
      // one finger (or the mouse) on the table, not on a piece: slide the zoomed-in view
      const pn = this.panning;
      if (pn && pn.id === e.pointerId) {
        const dx = e.clientX - pn.x, dy = e.clientY - pn.y;
        if (!pn.moved && Math.hypot(dx, dy) < 6) return;
        pn.moved = true; pn.x = e.clientX; pn.y = e.clientY;
        this.panBy(-dx, -dy);
      }
    });
    window.addEventListener('pointerup', e => { if (this.panning?.id === e.pointerId) this.panning = null; });
    // dragging chess pieces and tawla checkers
    window.addEventListener('pointermove', e => {
      const pr = this.press; if (!pr || pr.id !== e.pointerId || !this.board) return;
      if (this.pinch.size > 1) { this.cancelPress(); return; }
      if (!pr.dragging) {
        if (Math.hypot(e.clientX - pr.x, e.clientY - pr.y) < 10) return;
        if (!this.onDragStart(pr.from)) { this.press = null; return; }
        pr.dragging = true;
      }
      const pt = this.boardPoint(e.clientX, e.clientY);
      if (pt) this.board.carry?.(pr.from, pt.world);
    });
    window.addEventListener('pointerup', e => {
      const pr = this.press; if (!pr || pr.id !== e.pointerId) return;
      this.press = null;
      if (!pr.dragging) { this.onPick(pr.from); return; }
      const to = this.boardPoint(e.clientX, e.clientY)?.pick ?? null;
      if (to && this.board) this.board.dropped?.(to);
      // a legal drop stays where it was let go until the next view moves it there; anything else goes back
      if (!this.onDrop(pr.from, to)) this.board?.carry?.(pr.from, null);
    });
    window.addEventListener('pointercancel', () => this.cancelPress());
    const end = (e: PointerEvent) => { this.pinch.delete(e.pointerId); if (this.pinch.size < 2) this.pinchStart = 0; };
    canvas.addEventListener('pointerup', end); canvas.addEventListener('pointercancel', end);
    window.addEventListener('resize', () => this.resize());
    window.visualViewport?.addEventListener('resize', () => this.resize());
    if (typeof ResizeObserver === 'function') new ResizeObserver(() => this.resize()).observe(canvas);
    this.resize();
    // any touch, click or wheel: full speed for a moment
    for (const ev of ['pointerdown', 'pointermove', 'wheel', 'keydown'] as const) window.addEventListener(ev, () => this.wake(), { passive: true });
    document.addEventListener('visibilitychange', () => { this.lastT = performance.now(); this.wake(); });
    const loop = (now: number) => {
      requestAnimationFrame(loop);
      if (document.hidden || this.paused) return;
      // idle on a phone: 30 fps, the café still breathes but the GPU rests
      if (IS_PHONE && now - this.lastDraw < (now > this.busyUntil ? 30 : 14)) return;   // and never over 60 fps on 90/120 Hz screens
      this.lastDraw = now;
      this.tick(now);
    };
    requestAnimationFrame(loop);
  }

  /* ---------- public API ---------- */

  showHome(ch: number) { this.wake(); this.view = null; this.homeCh = ch; this.sel = null; this.choose = null; this.refresh(); }

  showTable(view: AnyView, sel: number | null, choose: End[] | null, boardUi: BoardUi = { sel: null, targets: [] }) {
    const newRound = !this.view || this.view.round !== view.round || this.view.id !== view.id;
    if (newRound) { this.known.clear(); this.dropAt.clear(); this.yardLayout = []; }
    this.view = view; this.sel = sel; this.choose = choose; this.boardUi = boardUi;
    this.wake(1800);
    this.refresh();
  }

  animate(seat: number, type: Anim['type']) {
    const pos = this.posToSeat.indexOf(seat);
    const c = pos >= 0 ? this.chars[pos] : null;
    if (c) { (c.userData as CharData).anim = { type, t0: this.clock, d: type === 'win' ? 2.2 : 0.9 }; this.wake(type === 'win' ? 2500 : 1200); }
  }

  seatAt(pos: number) { return this.posToSeat[pos] ?? -1; }
  posOf(seat: number) { return this.posToSeat.indexOf(seat); }

  /** Screen position (CSS px) just above a player's head. Your own seat has no character, so it reports the table edge. */
  /** Screen position of a board spot (how-to-play recorder). */
  spotScreen(p: Pick): { x: number; y: number } | null {
    const w = this.board?.spot?.(p); if (!w) return null;
    w.project(this.camera);
    const r = this.canvas.getBoundingClientRect();
    return { x: r.left + (w.x + 1) / 2 * r.width, y: r.top + (1 - w.y) / 2 * r.height };
  }
  /** Screen position of the open end of the domino line where a dragged tile can go. */
  ghostScreen(e: End): { x: number; y: number } | null {
    const g = this.ghosts.find(o => o.userData.end === e); if (!g) return null;
    const w = g.getWorldPosition(new THREE.Vector3()).project(this.camera);
    const r = this.canvas.getBoundingClientRect();
    return { x: r.left + (w.x + 1) / 2 * r.width, y: r.top + (1 - w.y) / 2 * r.height };
  }
  headScreen(pos: number): { x: number; y: number } | null {
    const c = this.chars[pos];
    const P = this.seatPoint(pos);
    if (c) { const d = c.userData as CharData; this.v3.set(c.position.x, c.position.y + HEAD_Y - 1.75 + 1.55, c.position.z); }
    else if (this.view && this.posToSeat[pos] >= 0) this.v3.set(P.x * 0.8, 0.5, P.z * 0.8);
    else return null;
    this.v3.project(this.camera);
    if (this.v3.z > 1) return null;
    const r = this.canvas.getBoundingClientRect();
    return { x: ((this.v3.x + 1) / 2) * r.width + r.left, y: ((1 - this.v3.y) / 2) * r.height + r.top };
  }

  /** Numbers for tuning: draw calls, triangles, textures and the drawing size (?promo test hook only). */
  stats() {
    const i = this.renderer.info, b = this.renderer.getDrawingBufferSize(new THREE.Vector2());
    let meshes = 0, lights = 0; this.scene.traverse(o => { if ((o as THREE.Mesh).isMesh) meshes++; if ((o as THREE.Light).isLight) lights++; });
    return { calls: i.render.calls, triangles: i.render.triangles, geometries: i.memory.geometries, textures: i.memory.textures, programs: i.programs?.length ?? 0, meshes, lights, px: `${b.x}x${b.y}`, gfx: this.gfx, post: !!this.post };
  }

  /* ---------- shop skins ---------- */

  /** Wear these skins (ids per kind). Rebuilds only what changed. */
  setSkins(s: Partial<Record<'tiles' | 'dice' | 'tawla' | 'chess' | 'ludo', string>>) {
    let rebuild = false;
    if (s.tiles && s.tiles !== this.skins.tiles && TILE_LOOKS[s.tiles]) {
      this.skins.tiles = s.tiles; this.tileLook = TILE_LOOKS[s.tiles];
      this.faceMats.forEach(m => { m.map?.dispose(); m.dispose(); }); this.faceMats.clear();
      this.bodyMat.color.setHex(this.tileLook.body); this.bodyMat.roughness = this.tileLook.rough; this.bodyMat.metalness = this.tileLook.metal; this.bodyMat.needsUpdate = true;
      rebuild = true;
    }
    for (const k of ['dice', 'tawla', 'chess', 'ludo'] as const) {
      if (!s[k] || s[k] === this.skins[k]) continue;
      this.skins[k] = s[k]!;
      if (k === 'dice') setDiceLook(s[k]!);
      // boards are rebuilt with the new look the next time they are shown (dice live inside the tawla and ludo boards)
      const drop = k === 'dice' ? (['tawla', 'ludo'] as const) : [k];
      for (const g of drop) { const b = this.boards[g]; if (b) { this.scene.remove(b.root); disposeTree(b.root); delete this.boards[g]; } }
      rebuild = true;
    }
    if (rebuild) this.rebuildBoard();
  }

  /* ---------- tiles ---------- */

  private faceMat(a: number, b: number) {
    const k = a + '-' + b;
    let m = this.faceMats.get(k);
    if (!m) {
      const tx = canvasTex(512, 256, g => {
        // the face (ivory by default, or the tile skin) with a soft vignette
        const L = this.tileLook;
        const bg = g.createRadialGradient(256, 128, 40, 256, 128, 300);
        bg.addColorStop(0, L.face[0]); bg.addColorStop(1, L.face[1]);
        g.fillStyle = bg; g.fillRect(0, 0, 512, 256);
        if (L.veins) {
          g.strokeStyle = L.veins; g.lineCap = 'round';
          for (let i = 0; i < 6; i++) { g.lineWidth = 1 + (i % 3); g.beginPath(); let x = (i * 97) % 512, y = 0; g.moveTo(x, y); while (y < 256) { x += ((i * 37 + y) % 50) - 25; y += 28; g.lineTo(x, y); } g.stroke(); }
        }
        // centre groove
        g.fillStyle = L.groove; g.fillRect(253, 22, 5, 212);
        g.fillStyle = 'rgba(255,255,255,.7)'; g.fillRect(258, 22, 2, 212);
        // engraved pips
        [a, b].forEach((v, h) => PIPS[v].forEach(i => {
          // the pattern runs along the tile's length, as on a real domino (a 6 is two rows of three)
          const cx = h * 256 + 52 + Math.floor(i / 3) * 76, cy = 52 + (i % 3) * 76, r = 21;
          const pg = g.createRadialGradient(cx - 5, cy - 5, 2, cx, cy, r);
          pg.addColorStop(0, L.pip[0]); pg.addColorStop(0.8, L.pip[1]); pg.addColorStop(1, L.pip[2]);
          g.fillStyle = pg; g.beginPath(); g.arc(cx, cy, r, 0, Math.PI * 2); g.fill();
          g.strokeStyle = 'rgba(255,255,255,.55)'; g.lineWidth = 2; g.beginPath(); g.arc(cx, cy, r + 1, 0.1 * Math.PI, 0.6 * Math.PI); g.stroke();
        }));
        // brass spinner pin
        const pin = g.createRadialGradient(252, 124, 1, 256, 128, 13);
        pin.addColorStop(0, '#fff2c4'); pin.addColorStop(0.5, '#d4a347'); pin.addColorStop(1, '#7a5520');
        g.fillStyle = pin; g.beginPath(); g.arc(256, 128, 12, 0, Math.PI * 2); g.fill();
      }, undefined, TEX_HI > 1 ? 1.5 : 1);
      m = new THREE.MeshStandardMaterial({ map: tx, roughness: this.tileLook.rough, metalness: this.tileLook.metal });
      this.faceMats.set(k, m);
    }
    return m;
  }

  /** A tile in layout units: 2 long, 1 wide. Face-up shows pips; face-down shows the plain back. */
  private tile(a: number, b: number, faceUp = true) {
    const g = new THREE.Group();
    const body = new THREE.Mesh(this.body, this.bodyMat); body.castShadow = true; body.receiveShadow = true; g.add(body);
    if (faceUp) { const f = new THREE.Mesh(this.faceGeo, this.faceMat(a, b)); f.position.y = TILE_H / 2 + 0.003; f.receiveShadow = true; g.add(f); }
    return g;
  }

  /* ---------- people ---------- */

  private seatPoint(pos: number) {
    return [{ x: 0, z: SEAT_R }, { x: -SEAT_R, z: 0 }, { x: 0, z: -SEAT_R }, { x: SEAT_R, z: 0 }][pos];
  }

  /** Characters who left a seat wait here to be seated again (building one is slow, and nothing leaks). */
  private charPool = new Map<number, THREE.Group[]>();
  private parkChar(g: THREE.Group) {
    const u = g.userData as CharData; u.anim = null; u.active = false;
    const list = this.charPool.get(u.ch) ?? []; list.push(g); this.charPool.set(u.ch, list);
  }
  private takeChar(i: number): THREE.Group {
    const g = this.charPool.get(i)?.pop();
    if (!g) return this.makeChar(i);
    g.position.set(0, 0, 0); g.rotation.set(0, 0, 0);
    return g;
  }

  private makeChar(i: number, regular = false): THREE.Group {
    const p = makePerson(PEOPLE[i], String(i));
    const g = p.group;
    g.add(makeChair());
    const data: CharData = { piv: p.piv, head: p.head, arms: p.arms, elbows: p.elbows, phase: Math.random() * 6, anim: null, active: false, ch: i, seat: -1 };
    g.userData = data;
    if (regular) {
      // holding a glass of tea
      const tea = makeTeaGlass(); tea.scale.setScalar(0.75); tea.rotation.x = Math.PI / 2 + 0.45; tea.position.set(0, -0.35, -0.12);
      p.hands[1].add(tea);
      p.arms[1].rotation.x = -1.6;
    }
    return g;
  }

  /* ---------- building the table state ---------- */

  private refresh() {
    const v = this.view;
    let chs: number[] = [-1, -1, -1, -1];
    this.posToSeat = [-1, -1, -1, -1];
    if (!v) {
      chs = [-1, (this.homeCh + 1) % 6, this.homeCh, (this.homeCh + 4) % 6];
    } else {
      const n = v.seats.length, base = v.you >= 0 ? v.you : 0, list = positionsFor(n);
      for (let k = 0; k < n; k++) {
        const seat = (base + k) % n, pos = list[k], s = v.seats[seat];
        this.posToSeat[pos] = seat;
        // you are the camera: your own chair stays empty so nothing blocks your view of the table
        if (s && !(pos === 0 && v.you >= 0)) chs[pos] = s.ch;
      }
      if (v.you < 0 && v.seats[base]) chs[0] = v.seats[base]!.ch;
    }
    for (let pos = 0; pos < 4; pos++) {
      const cur = this.chars[pos];
      if (cur && (cur.userData as CharData).ch !== chs[pos]) { this.scene.remove(cur); this.chars[pos] = null; this.parkChar(cur); }
      if (chs[pos] >= 0 && !this.chars[pos]) { const g = this.takeChar(chs[pos]); this.chars[pos] = g; this.scene.add(g); }
      const g = this.chars[pos];
      if (g) {
        const P = this.seatPoint(pos); g.position.set(P.x, 0, P.z); g.lookAt(0, 0, 0);
        const d = g.userData as CharData; d.seat = this.posToSeat[pos];
        d.active = !!v && v.phase === 'play' && v.turn === d.seat;
      }
    }
    this.rebuildBoard();
  }

  /** Domino: the footprint of the line of play, plus where the next tile at each end would go. */
  private measureChain(v: TableView) {
    const placed = layoutLine(v.line, X_MAX);
    if (placed.length) {
      const any = TILES.findIndex(t => t[0] !== t[1]);
      for (const e of ['L', 'R'] as End[]) placed.push(previewPlacement(v.line, any, e, X_MAX));
    }
    const r = (TILE_W / 2) * TS;
    const pts: THREE.Vector3[] = [];
    let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
    for (const p of placed) { x0 = Math.min(x0, p.x * TS - r); x1 = Math.max(x1, p.x * TS + r); z0 = Math.min(z0, p.y * TS - r); z1 = Math.max(z1, p.y * TS + r); }
    if (placed.length) for (const x of [x0, x1]) for (const z of [z0, z1]) pts.push(new THREE.Vector3(x, TILE_H, z));
    const key = placed.length ? [x0, x1, z0, z1].map(n => n.toFixed(1)).join(',') : '';
    if (key !== this.chainKey) { this.chainKey = key; this.chainPts = pts; this.frameKey = ''; }
  }

  private rebuildBoard() {
    this.boardG.clear(); this.propsG.clear(); this.ghosts = []; this.ring.visible = false;
    const av = this.view;
    for (const b of Object.values(this.boards)) b!.root.visible = false;
    this.board = null;
    this.glasses.visible = !(av && av.game === 'ludo' && av.phase !== 'lobby');
    if (!av || av.phase === 'lobby') { if (this.zoomed) this.resetZoom(); return; }
    if (av.game !== 'domino') {
      // chess, tawla or ludo: the game's own board sits on the table
      const g = av.game;
      this.board = this.boards[g] ??= (() => { const r = g === 'chess' ? new ChessBoard(CHESS_LOOKS[this.skins.chess]) : g === 'tawla' ? new TawlaBoard(TAWLA_LOOKS[this.skins.tawla]) : new LudoBoard(LUDO_LOOKS[this.skins.ludo]); this.scene.add(r.root); return r; })();
      this.board.root.visible = true;
      this.board.update(av, this.boardUi, this.clock);
      this.turnRing(av.turn);
      return;
    }
    const v: TableView = av;
    const now = this.clock;
    this.measureChain(v);
    for (const pl of layoutLine(v.line, X_MAX)) {
      const m = this.tile(pl.a, pl.b);
      m.position.set(pl.x, TILE_H / 2, pl.y); m.rotation.y = -pl.ang;
      if (!this.known.has(pl.id)) { this.known.add(pl.id); if (v.last?.t === 'play' && v.last.id === pl.id) this.dropAt.set(pl.id, now); }
      const d = this.dropAt.get(pl.id);
      if (d !== undefined && now - d < 0.6) m.userData.drop = d;
      this.boardG.add(m);
    }
    if (this.sel !== null && this.choose) for (const e of this.choose) {
      const g = previewPlacement(v.line, this.sel, e, X_MAX);
      const m = new THREE.Mesh(this.body, this.ghostMat);
      m.position.set(g.x, TILE_H / 2 + 0.02, g.y); m.rotation.y = -g.ang; m.scale.setScalar(1.06); m.userData.end = e;
      this.boardG.add(m); this.ghosts.push(m);
    }
    // opponents stand their tiles on edge facing themselves, like at a real table
    this.posToSeat.forEach((seat, pos) => {
      if (seat < 0 || !v.seats[seat] || (pos === 0 && v.you >= 0)) return;
      const n = v.seats[seat]!.count, P = this.seatPoint(pos);
      const len = Math.hypot(P.x, P.z), inw = { x: -P.x / len, z: -P.z / len }, perp = { x: -inw.z, z: inw.x };
      const edge = T - 0.75, base = { x: -inw.x * edge, z: -inw.z * edge };
      const yaw = Math.atan2(-inw.z, inw.x);
      for (let i = 0; i < n; i++) {
        const off = (i - (n - 1) / 2) * TS * 1.12;
        const holder = new THREE.Group();
        const t = this.tile(0, 0, false); t.rotation.z = Math.PI / 2; t.rotation.y = Math.PI / 2; t.scale.setScalar(TS);
        holder.add(t);
        holder.position.set(base.x + perp.x * off, TILE_W * TS / 2, base.z + perp.z * off);
        holder.rotation.y = yaw + Math.PI / 2 + (i - (n - 1) / 2) * 0.02;
        this.propsG.add(holder);
      }
    });
    // the boneyard: face-down tiles in a loose pile at the corner
    while (this.yardLayout.length < v.yard) {
      const k = this.yardLayout.length;
      this.yardLayout.push({ x: T - 1.4 + (Math.random() - 0.5) * 1.4, z: -(T - 1.8) + (Math.random() - 0.5) * 2.4, r: Math.random() * Math.PI, y: (k > 7 ? 1 : 0) });
    }
    for (let i = 0; i < v.yard; i++) {
      const p = this.yardLayout[i];
      const t = this.tile(0, 0, false); t.scale.setScalar(TS);
      t.position.set(p.x, TILE_H * TS / 2 + p.y * TILE_H * TS, p.z); t.rotation.y = p.r;
      this.propsG.add(t);
    }
    if (v.phase === 'play') this.turnRing(v.turn);
  }

  /** Glowing ring on the table edge in front of whoever's turn it is. */
  private turnRing(turn: number) {
    if (turn < 0) return;
    const pos = this.posToSeat.indexOf(turn);
    if (pos < 0) return;
    const P = this.seatPoint(pos), len = Math.hypot(P.x, P.z);
    this.ring.position.set((P.x / len) * (T - 0.1), 0.02, (P.z / len) * (T - 0.1));
    this.ring.visible = true;
  }

  /* ---------- camera & loop ---------- */

  private resize() {
    const w = this.canvas.clientWidth || window.innerWidth, h = this.canvas.clientHeight || window.innerHeight;
    // sharp on phones, but never more pixels than the level allows (a 4K or retina desktop would crawl otherwise)
    const budget = [1.1e6, 2.2e6, 4.6e6][this.gfx], cap = [1, 1.6, 2.6][this.gfx];
    const pr = Math.max(0.6, Math.min(cap, IS_PHONE ? 2 : 9, window.devicePixelRatio || 1, Math.sqrt(budget / Math.max(1, w * h))));
    if (Math.abs(this.renderer.getPixelRatio() - pr) > 0.01) this.renderer.setPixelRatio(pr);
    this.renderer.setSize(w, h, false);
    this.wake();
    if (this.post) { const b = this.renderer.getDrawingBufferSize(new THREE.Vector2()); this.post.setSize(b.x, b.y); }
    this.camera.aspect = w / h;
    this.frameKey = '';
    this.placeCamera(0);
  }

  /* ---------- player zoom ---------- */
  private static readonly MAG_MIN = 0.7;
  private static readonly MAG_MAX = 3;
  private size() { return { w: this.canvas.clientWidth || window.innerWidth, h: this.canvas.clientHeight || window.innerHeight }; }
  /** Keep the zoomed-in view inside the picture. */
  private clampPan(p: { x: number; y: number }, mag: number) {
    const { w, h } = this.size(), s = Math.max(1, mag);
    p.x = Math.max(0, Math.min(w * (s - 1), p.x)); p.y = Math.max(0, Math.min(h * (s - 1), p.y));
  }
  /** Zoom by `factor` keeping the screen point (x, y) where it is. `now` skips the easing (pinch and wheel). */
  zoomAt(factor: number, x: number, y: number, now = false) {
    const m0 = this.magT, m1 = Math.min(TableScene.MAG_MAX, Math.max(TableScene.MAG_MIN, m0 * factor));
    if (Math.abs(m1 - m0) < 1e-4) return;
    const s0 = Math.max(1, m0), s1 = Math.max(1, m1);
    // a point under the finger: its place in the unzoomed picture is (x + pan) / s; keep it under the finger
    this.panT = { x: (x + this.panT.x) * s1 / s0 - x, y: (y + this.panT.y) * s1 / s0 - y };
    this.magT = m1; this.clampPan(this.panT, m1);
    if (now) { this.mag = this.magT; this.pan = { ...this.panT }; }
    this.onZoom(this.zoomed);
  }
  private panBy(dx: number, dy: number) {
    if (this.magT <= 1) return;
    this.panT = { x: this.panT.x + dx, y: this.panT.y + dy }; this.clampPan(this.panT, this.magT);
    this.pan = { ...this.panT };
  }
  private startPan(e: PointerEvent) {
    if (this.pinch.size === 0 && this.magT > 1.01) this.panning = { id: e.pointerId, x: e.clientX, y: e.clientY, moved: false };
  }
  /** The zoom buttons: in and out around the middle of the table, and back to the whole table. */
  zoomIn() { const c = this.centreOnScreen(); this.zoomAt(1.4, c.x, c.y); }
  zoomOut() { const c = this.centreOnScreen(); this.zoomAt(1 / 1.4, c.x, c.y); }
  resetZoom() { this.magT = 1; this.panT = { x: 0, y: 0 }; this.onZoom(false); }
  get zoomed() { return Math.abs(this.magT - 1) > 0.01; }
  /** The middle of what is on screen now (zooming the buttons keeps it there). */
  private centreOnScreen() {
    const { w, h } = this.size(), ins = this.insets;
    return { x: (ins.left + w - ins.right) / 2, y: (ins.top + h - ins.clear) / 2 };
  }

  /** The HUD and controls cover these strips of the screen (CSS px); the table is framed in the rest. */
  setInsets(i: { top: number; right: number; bottom: number; left: number; clear?: number }) {
    const r = (n: number) => Math.max(0, Math.round(n));
    // clear: how much of the bottom the played domino tiles must stay above (the hand plus the turn line and timer)
    const n = { top: r(i.top), right: r(i.right), bottom: r(i.bottom), left: r(i.left), clear: r(Math.max(i.clear ?? 0, i.bottom)) };
    if (n.top === this.insets.top && n.right === this.insets.right && n.bottom === this.insets.bottom && n.left === this.insets.left && n.clear === this.insets.clear) return;
    this.insets = n; this.frameKey = '';
  }

  /** Lower the graphics a step (slow device). */
  private stepDownGfx() {
    if (this.gfx === 0) return;
    this.gfx = (this.gfx - 1) as 0 | 1;
    try { localStorage.setItem(GFX_KEY, String(this.gfx)); } catch { /* private mode */ }
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.post?.dispose(); this.post = null;
    const sh = this.spot.shadow;
    sh.mapSize.setScalar(1024); sh.map?.dispose(); (sh as { map: unknown }).map = null;
    this.resize();
  }

  private placeCamera(sway: number) {
    const w = this.canvas.clientWidth || window.innerWidth, h = this.canvas.clientHeight || window.innerHeight;
    const playing = !!this.view && this.view.phase !== 'lobby';
    if (!playing) {
      // the café, seen from the door: the menu card sits over the lower part
      const portrait = w / h < 0.9, el = 0.62, vf = (this.camera.fov * Math.PI) / 180;
      const hf = 2 * Math.atan(Math.tan(vf / 2) * Math.max(0.5, w / h));
      const d = Math.max((T + 4.5) / Math.tan(hf / 2), ((T + 1.5) * Math.sin(el) + 1.0) / Math.tan(vf / 2)) * (portrait ? 0.92 : 1.05);
      this.camera.position.set(Math.sin(sway) * d * 0.25, 1.5 + d * Math.sin(el), -1.5 + d * Math.cos(el));
      this.camera.lookAt(Math.sin(sway) * 2, 1.5, -1.5);
      this.camera.setViewOffset(w, h, 0, Math.round(h * -0.04), w, h);
      this.camera.updateProjectionMatrix();
      return;
    }
    const v = this.view!, g = v.game;
    const portrait = w / h < 0.9;
    const el = g === 'domino' ? (portrait ? 1.08 : 0.98) : (portrait ? 1.2 : 1.12);
    const key = [w, h, g, el, this.zoom.toFixed(3), this.insets.top, this.insets.right, this.insets.bottom, this.insets.left, this.insets.clear, g === 'domino' ? this.chainKey : '', this.chars.map(c => (c ? 1 : 0)).join('')].join('|');
    if (key !== this.frameKey) { this.frameKey = key; this.frame = this.fit(w, h, el, g); }
    const { d, ox, oy } = this.frame;
    this.camera.position.set(0, d * Math.sin(el), d * Math.cos(el));
    this.camera.lookAt(0, 0, 0);
    // the player's lens zoom: the same view drawn s times bigger, looking at the part `pan` points to
    const s = Math.max(1, this.mag);
    this.camera.setViewOffset(w * s, h * s, ox * s + this.pan.x, oy * s + this.pan.y, w, h);
    this.camera.updateProjectionMatrix();
  }

  /**
   * Frames the table in the part of the screen the HUD and controls leave free: the camera backs off just enough
   * for the points that matter (the table or board, and the faces across it) to fit, then the picture is shifted
   * so they sit in the middle of that free area. Works for any screen shape, from a small phone to a 4K monitor.
   */
  private fit(w: number, h: number, el: number, g: string) {
    const ins = this.insets;
    const pad = Math.max(6, Math.min(w, h) * 0.025);
    const fx0 = ins.left + pad, fx1 = w - ins.right - pad, fy0 = ins.top + pad;
    const fw = Math.max(80, fx1 - fx0);
    const V = (x: number, y: number, z: number) => new THREE.Vector3(x, y, z);
    let sets: { pts: THREE.Vector3[]; under: number }[];
    if (g === 'domino') {
      const e = T - 0.2;
      const far = [V(-e, 0, -e), V(e, 0, -e)];
      // the faces of the players across and (on a wide screen) beside you
      const wide = fw / Math.max(80, h - ins.top - ins.bottom) > 1.25;
      this.chars.forEach((c, pos) => {
        if (!c || pos === 0 || (!wide && pos !== 2)) return;
        const P = this.seatPoint(pos);
        far.push(V(P.x * (pos === 2 ? 1 : 0.86), HEAD_Y - 1.75 + 1.3, P.z));   // the top of the head, and a little room for the name
      });
      // best: the whole table, its empty near edge allowed under the hand. On a short screen that makes it too
      // small, so then only the far side and the middle (where the tiles are played) must clear the hand.
      sets = [{ pts: [...far, V(-e, 0, e), V(e, 0, e)], under: 0.45 }, { pts: [...far, V(-e, 0, 0.8), V(e, 0, 0.8)], under: 0 }];
    } else {
      const bw = g === 'chess' ? 4.9 : g === 'ludo' ? 5.7 : 6.0;
      const bd = g === 'tawla' ? 4.25 : bw;
      const pts: THREE.Vector3[] = [];
      for (const x of [-bw, bw]) for (const z of [-bd, bd]) for (const y of [0, 0.5]) pts.push(V(x, y, z));
      sets = [{ pts, under: 0 }];
    }
    const cam = this.camera;
    const box = (d: number, pts: THREE.Vector3[]) => {
      cam.position.set(0, d * Math.sin(el), d * Math.cos(el)); cam.lookAt(0, 0, 0);
      cam.clearViewOffset(); cam.updateProjectionMatrix(); cam.updateMatrixWorld();
      let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity;
      for (const p of pts) {
        const q = this.v3.copy(p).project(cam);
        const sx = (q.x + 1) / 2 * w, sy = (1 - q.y) / 2 * h;
        x0 = Math.min(x0, sx); x1 = Math.max(x1, sx); y0 = Math.min(y0, sy); y1 = Math.max(y1, sy);
      }
      return { x0, x1, y0, y1 };
    };
    // domino: every played tile and the places to play next must stay above the hand, the turn line and the timer
    const chain = g === 'domino' ? this.chainPts : [];
    const fyClear = h - ins.clear - pad;
    /** The vertical shift that keeps everything in its band at distance d, or null if nothing does. */
    const place = (d: number, set: { pts: THREE.Vector3[]; under: number }) => {
      const fy1 = h - ins.bottom * (1 - set.under) - pad;
      const b = box(d, set.pts), c = chain.length ? box(d, chain) : null;
      const x0 = Math.min(b.x0, c?.x0 ?? Infinity), x1 = Math.max(b.x1, c?.x1 ?? -Infinity);
      if (x1 - x0 > fw) return null;
      const lo = fy0 - Math.min(b.y0, c?.y0 ?? Infinity);
      const hi = Math.min(fy1 - b.y1, c ? fyClear - c.y1 : Infinity);
      if (lo > hi) return null;
      // centre in the free area when there is room to spare
      const want = (fy0 + fy1) / 2 - (b.y0 + b.y1) / 2;
      return { s: Math.max(lo, Math.min(hi, want)), ox: Math.round((x0 + x1) / 2 - (fx0 + fx1) / 2) };
    };
    const solve = (set: { pts: THREE.Vector3[]; under: number }) => {
      let lo = 4, hi = 400;
      for (let i = 0; i < 28; i++) { const mid = (lo + hi) / 2; if (place(mid, set)) hi = mid; else lo = mid; }
      return { d: hi, set };
    };
    const tries = sets.map(solve);
    const best = tries.length > 1 && tries[0].d > tries[1].d * 1.12 ? tries[1] : tries[0];
    const d = best.d * this.zoom;
    const p = place(d, best.set);
    if (p) return { d, ox: p.ox, oy: Math.round(-p.s) };
    // zoomed in by the player: keep the middle of the table (or of the line of play) in the middle
    const b = box(d, chain.length ? chain : best.set.pts), fy1 = h - ins.bottom * (1 - best.set.under) - pad;
    return { d, ox: Math.round((b.x0 + b.x1) / 2 - (fx0 + fx1) / 2), oy: Math.round((b.y0 + b.y1) / 2 - (fy0 + Math.min(fy1, fyClear)) / 2) };
  }

  private tick(now: number) {
    if (!this.portraitsDone) this.makePortraits();
    const raw = (now - this.lastT) / 1000;
    const dt = Math.min(0.05, raw); this.lastT = now; this.clock += dt;
    this.watchSpeed(raw);
    const amp = this.reduce ? 0 : 1, t = this.clock;
    // ease the zoom buttons' steps; pinch and wheel set it directly
    if (Math.abs(this.mag - this.magT) > 1e-3 || Math.abs(this.pan.x - this.panT.x) > 0.5 || Math.abs(this.pan.y - this.panT.y) > 0.5) {
      const k = this.reduce ? 1 : 1 - Math.exp(-Math.min(raw, 0.25) * 14);   // about a quarter of a second, at any frame rate
      this.mag += (this.magT - this.mag) * k; this.pan.x += (this.panT.x - this.pan.x) * k; this.pan.y += (this.panT.y - this.pan.y) * k;
      this.clampPan(this.pan, this.mag);
      this.wake(300);
    } else { this.mag = this.magT; this.pan = { ...this.panT }; }
    this.zoom = this.mag < 1 ? 1 / this.mag : 1;
    this.placeCamera(this.view || this.reduce ? 0 : Math.sin(t * 0.12) * 0.35);
    for (const c of this.chars) {
      if (!c) continue;
      const u = c.userData as CharData;
      u.piv.position.y = -1.75 + Math.sin(t * 1.6 + u.phase) * 0.03 * amp;
      u.piv.rotation.x += ((u.active ? 0.2 : 0) - u.piv.rotation.x) * 0.08;
      u.head.rotation.y = Math.sin(t * 0.5 + u.phase) * 0.2 * amp;
      u.head.rotation.x += ((u.active ? 0.3 : 0.06) - u.head.rotation.x) * 0.08;
      u.arms[0].rotation.x = -1.1; u.arms[1].rotation.x = -1.1; c.position.y = 0;
      u.elbows[0].rotation.x = u.elbows[1].rotation.x = -0.45;
      if (u.anim) {
        const k = (t - u.anim.t0) / u.anim.d;
        if (k >= 1) u.anim = null;
        else if (u.anim.type === 'play') u.arms[1].rotation.x = -1.1 - Math.sin(k * Math.PI) * 0.9;
        else if (u.anim.type === 'draw') u.arms[0].rotation.x = -1.1 - Math.sin(k * Math.PI) * 0.7;
        else if (u.anim.type === 'pass') u.head.rotation.y = Math.sin(k * Math.PI * 6) * 0.55 * (1 - k);
        else if (u.anim.type === 'emote') u.arms[1].rotation.x = -2.6 + Math.sin(k * Math.PI * 6) * 0.3;
        else if (u.anim.type === 'win') { c.position.y = Math.abs(Math.sin(k * Math.PI * 4)) * 0.8 * (1 - k * 0.5); u.arms[0].rotation.x = u.arms[1].rotation.x = -2.9; }
        for (let a = 0; a < 2; a++) if (u.arms[a].rotation.x < -1.8) u.elbows[a].rotation.x = -0.15;
      }
    }
    for (const r of this.regulars) {
      const u = r.userData as CharData;
      u.piv.position.y = -1.75 + Math.sin(t * 1.3 + u.phase) * 0.03 * amp;
      u.head.rotation.y = Math.sin(t * 0.35 + u.phase) * 0.5 * amp;
      const sip = Math.max(0, Math.sin(t * 0.4 + u.phase)) * amp;
      u.arms[1].rotation.x = -1.6 - sip * 0.5; u.elbows[1].rotation.x = -0.6 - sip * 1.1;
    }
    for (const m of this.boardG.children) {
      const d = m.userData.drop as number | undefined;
      if (d !== undefined) { const k = Math.min(1, (t - d) / 0.35); m.position.y = TILE_H / 2 + (1 - k) * (1 - k) * 4; }
    }
    if (!this.reduce) this.smoke.tick(dt);
    this.board?.tick(this.clock);
    this.ghostMat.opacity = 0.35 + Math.sin(t * 5) * 0.2;
    if (this.ring.visible) (this.ring.material as THREE.MeshBasicMaterial).opacity = 0.5 + Math.sin(t * 3) * 0.3;
    // shadows: redrawn every frame while things move, else ~12 times a second (the regulars breathing)
    const shadowNow = performance.now();
    if (shadowNow < this.busyUntil || shadowNow - this.shadowAt > 80) { this.renderer.shadowMap.needsUpdate = true; this.shadowAt = shadowNow; }
    this.renderer.info.reset();
    if (this.post) {
      // the whole picture stays sharp: only a soft glow on highlights, a warm grade and a light vignette
      try { this.post.render(this.scene, this.camera); }
      catch { this.post.dispose(); this.post = null; this.renderer.setRenderTarget(null); this.renderer.render(this.scene, this.camera); }
    } else this.renderer.render(this.scene, this.camera);
    this.onFrame();
  }

  /** Every ~2 s: if frames took over 45 ms on average twice running, lower the graphics a step. */
  private watchSpeed(raw: number) {
    if (raw <= 0 || raw > 0.5 || document.hidden) return;
    const p = this.perf; p.sum += raw; p.n++;
    if (p.sum < 2) return;
    const avg = p.sum / p.n; p.sum = 0; p.n = 0;
    p.slow = avg > 0.045 ? p.slow + 1 : 0;
    if (p.slow >= 2) { p.slow = 0; this.stepDownGfx(); }
  }

  /** Small 2D tile for the hand and result lists. */
  static tileEl(id: number, tag: 'button' | 'div' = 'button'): HTMLElement {
    const [a, b] = TILES[id];
    const el = document.createElement(tag);
    if (tag === 'button') (el as HTMLButtonElement).type = 'button';
    el.className = 'tile';
    for (const v of [a, b]) {
      const h = document.createElement('div'); h.className = 'half';
      for (let i = 0; i < 9; i++) { const d = document.createElement('span'); d.className = PIPS[v].includes(i) ? 'dot on' : 'dot'; h.appendChild(d); }
      el.appendChild(h);
    }
    el.append(Object.assign(document.createElement('i'), { className: 'pin' }));
    el.setAttribute('aria-label', `${a} | ${b}`);
    return el;
  }
}
