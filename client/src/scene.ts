import * as THREE from 'three';
import { End, TILES } from '../../shared/engine';
import { layoutLine, previewPlacement } from '../../shared/layout';
import type { AnyView, TableView } from '../../shared/protocol';
import type { BoardGameId } from '../../shared/games/defs';
import { BoardRenderer, BoardUi, ChessBoard, LudoBoard, Pick, TawlaBoard, initBoardEnvironment } from './boards';
import { HEAD_Y, PEOPLE, makePerson } from './people';
import { FLOOR_Y, Smoke, buildAhwa, canvasTex, makeChair, makeShisha, makeTable, makeTeaGlass } from './ahwa';

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
  private zoom = 1;                 // pinch / mouse-wheel zoom, 1 = default framing
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
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.05;
    initBoardEnvironment(this.renderer);
    const s = this.scene;
    s.background = new THREE.Color(0x24170e);
    s.fog = new THREE.Fog(0x2a1b10, 30, 75);
    s.add(new THREE.HemisphereLight(0xffe2b8, 0x3a2416, 0.9));
    // the bulb over our table
    const spot = new THREE.SpotLight(0xffd59a, 200, 45, 0.78, 0.55, 1.4);
    spot.position.set(0.5, 15, 1.5); spot.target.position.set(0, 0, 0);
    spot.castShadow = true; spot.shadow.mapSize.set(2048, 2048); spot.shadow.bias = -0.0004; spot.shadow.radius = 3;
    s.add(spot, spot.target);
    const fill = new THREE.DirectionalLight(0xffe9cc, 0.35); fill.position.set(-10, 12, 18); s.add(fill);

    const room = buildAhwa(s);
    this.smoke = new Smoke(s, room.smoke);
    // a couple of regulars at the next tables
    const r1 = this.makeChar(0, true); r1.position.set(-12.5 + 3.6 * Math.sin(0.1), 0, -8.5 + 3.6); r1.lookAt(-12.5, 0, -8.5); s.add(r1);
    const r2 = this.makeChar(3, true); r2.position.set(12.5, 0, -9 - 3.6); r2.lookAt(12.5, 0, -9); s.add(r2);
    const r3 = this.makeChar(1, true); r3.position.set(12.5, 0, -9 + 3.6); r3.lookAt(12.5, 0, -9); s.add(r3);
    this.regulars.push(r1, r2, r3);

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
        return;
      }
      if (!this.ghosts.length) return;
      const hit = this.ray.intersectObjects(this.ghosts, true)[0];
      if (hit) { let o: THREE.Object3D | null = hit.object; while (o && !o.userData.end) o = o.parent; if (o) this.onGhost(o.userData.end as End); }
    });
    // zoom: mouse wheel on desktop, two-finger pinch on phones
    const clampZoom = (z: number) => Math.min(1.5, Math.max(0.55, z));
    canvas.addEventListener('wheel', e => { e.preventDefault(); this.zoom = clampZoom(this.zoom * Math.exp(e.deltaY * 0.0012)); }, { passive: false });
    canvas.addEventListener('pointerdown', e => {
      this.pinch.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (this.pinch.size === 2) { const [a, b] = [...this.pinch.values()]; this.pinchStart = Math.hypot(a.x - b.x, a.y - b.y); this.zoomStart = this.zoom; }
    });
    canvas.addEventListener('pointermove', e => {
      if (!this.pinch.has(e.pointerId)) return;
      this.pinch.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (this.pinch.size === 2 && this.pinchStart > 0) { const [a, b] = [...this.pinch.values()]; this.zoom = clampZoom(this.zoomStart * this.pinchStart / Math.max(1, Math.hypot(a.x - b.x, a.y - b.y))); }
    });
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
    this.resize();
    const loop = (now: number) => { this.tick(now); requestAnimationFrame(loop); };
    requestAnimationFrame(loop);
  }

  /* ---------- public API ---------- */

  showHome(ch: number) { this.view = null; this.homeCh = ch; this.sel = null; this.choose = null; this.refresh(); }

  showTable(view: AnyView, sel: number | null, choose: End[] | null, boardUi: BoardUi = { sel: null, targets: [] }) {
    const newRound = !this.view || this.view.round !== view.round || this.view.id !== view.id;
    if (newRound) { this.known.clear(); this.dropAt.clear(); this.yardLayout = []; }
    this.view = view; this.sel = sel; this.choose = choose; this.boardUi = boardUi;
    this.refresh();
  }

  animate(seat: number, type: Anim['type']) {
    const pos = this.posToSeat.indexOf(seat);
    const c = pos >= 0 ? this.chars[pos] : null;
    if (c) (c.userData as CharData).anim = { type, t0: this.clock, d: type === 'win' ? 2.2 : 0.9 };
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

  /* ---------- tiles ---------- */

  private faceMat(a: number, b: number) {
    const k = a + '-' + b;
    let m = this.faceMats.get(k);
    if (!m) {
      const tx = canvasTex(512, 256, g => {
        // ivory face with a soft vignette
        const bg = g.createRadialGradient(256, 128, 40, 256, 128, 300);
        bg.addColorStop(0, '#fbf5e6'); bg.addColorStop(1, '#ebdfc4');
        g.fillStyle = bg; g.fillRect(0, 0, 512, 256);
        // centre groove
        g.fillStyle = '#9c8d70'; g.fillRect(253, 22, 5, 212);
        g.fillStyle = 'rgba(255,255,255,.7)'; g.fillRect(258, 22, 2, 212);
        // engraved pips
        [a, b].forEach((v, h) => PIPS[v].forEach(i => {
          // the pattern runs along the tile's length, as on a real domino (a 6 is two rows of three)
          const cx = h * 256 + 52 + Math.floor(i / 3) * 76, cy = 52 + (i % 3) * 76, r = 21;
          const pg = g.createRadialGradient(cx - 5, cy - 5, 2, cx, cy, r);
          pg.addColorStop(0, '#000'); pg.addColorStop(0.8, '#1e140d'); pg.addColorStop(1, '#4a3a2a');
          g.fillStyle = pg; g.beginPath(); g.arc(cx, cy, r, 0, Math.PI * 2); g.fill();
          g.strokeStyle = 'rgba(255,255,255,.55)'; g.lineWidth = 2; g.beginPath(); g.arc(cx, cy, r + 1, 0.1 * Math.PI, 0.6 * Math.PI); g.stroke();
        }));
        // brass spinner pin
        const pin = g.createRadialGradient(252, 124, 1, 256, 128, 13);
        pin.addColorStop(0, '#fff2c4'); pin.addColorStop(0.5, '#d4a347'); pin.addColorStop(1, '#7a5520');
        g.fillStyle = pin; g.beginPath(); g.arc(256, 128, 12, 0, Math.PI * 2); g.fill();
      });
      m = new THREE.MeshStandardMaterial({ map: tx, roughness: 0.25 });
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
      if (cur && (cur.userData as CharData).ch !== chs[pos]) { this.scene.remove(cur); this.chars[pos] = null; }
      if (chs[pos] >= 0 && !this.chars[pos]) { const g = this.makeChar(chs[pos]); this.chars[pos] = g; this.scene.add(g); }
      const g = this.chars[pos];
      if (g) {
        const P = this.seatPoint(pos); g.position.set(P.x, 0, P.z); g.lookAt(0, 0, 0);
        const d = g.userData as CharData; d.seat = this.posToSeat[pos];
        d.active = !!v && v.phase === 'play' && v.turn === d.seat;
      }
    }
    this.rebuildBoard();
  }

  private rebuildBoard() {
    this.boardG.clear(); this.propsG.clear(); this.ghosts = []; this.ring.visible = false;
    const av = this.view;
    for (const b of Object.values(this.boards)) b!.root.visible = false;
    this.board = null;
    this.glasses.visible = !(av && av.game === 'ludo' && av.phase !== 'lobby');
    if (!av || av.phase === 'lobby') return;
    if (av.game !== 'domino') {
      // chess, tawla or ludo: the game's own board sits on the table
      const g = av.game;
      this.board = this.boards[g] ??= (() => { const r = g === 'chess' ? new ChessBoard() : g === 'tawla' ? new TawlaBoard() : new LudoBoard(); this.scene.add(r.root); return r; })();
      this.board.root.visible = true;
      this.board.update(av, this.boardUi, this.clock);
      this.turnRing(av.turn);
      return;
    }
    const v: TableView = av;
    const now = this.clock;
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
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.placeCamera(0);
  }

  private placeCamera(sway: number) {
    const w = this.canvas.clientWidth || window.innerWidth, h = this.canvas.clientHeight || window.innerHeight;
    const portrait = w / h < 0.9;
    const playing = !!this.view && this.view.phase !== 'lobby';
    // board games are framed on their own board, from a little higher so it reads clearly
    const g = playing ? this.view!.game : 'domino';
    const boardHalf = g === 'chess' ? 4.9 : g === 'ludo' ? 5.7 : g === 'tawla' ? 6.0 : 0;
    const boardDepth = g === 'tawla' ? 4.25 : boardHalf;            // tawla's box is wider than it is deep
    // a landscape phone keeps the controls in a 200px panel on the right of board games
    const phoneLand = w > h && h <= 600;
    const side = playing && boardHalf && phoneLand ? 200 : 0;
    const el = !playing ? 0.62 : boardHalf ? (portrait ? 1.2 : 1.12) : (portrait ? 1.08 : 0.98);
    const vf = (this.camera.fov * Math.PI) / 180;
    const effAspect = Math.max(0.5, (w - side) / h);
    const hf = 2 * Math.atan(Math.tan(vf / 2) * effAspect);
    const hw = !playing ? T + 4.5 : boardHalf ? boardHalf + (side ? 0.3 : 0) : T + 0.4;
    // domino on a landscape phone leaves room at the top for the face of the player across the table
    const hd = !playing ? T + 1.5 : boardHalf ? (phoneLand ? boardDepth + 0.8 : Math.min(boardHalf, 5)) : phoneLand ? T + 1.9 : T + 0.3;
    const d = Math.max(hw / Math.tan(hf / 2), (hd * Math.sin(el) + 1.0) / Math.tan(vf / 2)) * (playing ? this.zoom : 1.05);
    const tz = playing ? (portrait ? 0.4 : phoneLand && !boardHalf ? -1.4 : 0.1) : -1.5;
    const ty = playing ? 0 : 1.5;
    this.camera.position.set(Math.sin(sway) * d * 0.25, ty + d * Math.sin(el), tz + d * Math.cos(el));
    this.camera.lookAt(Math.sin(sway) * 2, ty, tz);
    // shift the picture: up a little under the top bar, and left of the control panel
    const yOff = playing ? (portrait ? 0.07 : phoneLand ? (boardHalf ? -0.035 : -0.07) : 0.08) : -0.04;
    this.camera.setViewOffset(w, h, side / 2, Math.round(h * yOff), w, h);
    this.camera.updateProjectionMatrix();
  }

  private tick(now: number) {
    if (!this.portraitsDone) this.makePortraits();
    const dt = Math.min(0.05, (now - this.lastT) / 1000); this.lastT = now; this.clock += dt;
    const amp = this.reduce ? 0 : 1, t = this.clock;
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
    this.renderer.render(this.scene, this.camera);
    this.onFrame();
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
