import * as THREE from 'three';
import { ELBOW_TO_PALM, UPPER_ARM } from './people';

/*
 * The people at the tables, alive: they breathe, blink, look at the board, at whoever is playing and now and
 * then at you; they think on their turn, fidget while they wait, react to wins and losses and act out emotes.
 *
 * Arms are posed by where the hands should be (on the table, at the chin, on the head, up in the air): a
 * two-bone solver bends the shoulder and the elbow like a real arm (elbows out and down), so every pose
 * stays natural. Pure posing: every frame `act` sets each joint from the character's state.
 */

export type ActKind = 'play' | 'draw' | 'pass' | 'win' | 'lose' | 'emote';
export interface Act { type: ActKind; t0: number; d: number; k?: string; /** test hook: hold the action at this point (0-1) */ fix?: number }
type Idle = 'tap' | 'lean' | 'scratch' | 'nod' | 'stretch' | 'chin' | 'sip'
  | 'glasses' | 'mustache' | 'watch' | 'knuckles' | 'behindhead' | 'drum' | 'groove' | 'hijab' | 'folded' | 'hair' | 'beads' | 'beard';
export interface Actor {
  piv: THREE.Group; head: THREE.Group; arms: THREE.Group[]; elbows: THREE.Group[]; eyes: THREE.Mesh[]; lids: THREE.Mesh[];
  phase: number; anim: Act | null; active: boolean;
  /** What they do with their hands while waiting, and until when. */
  idle: { kind: Idle; t0: number; d: number } | null; nextIdle: number;
  /** Blinking: next blink time; looking: the current point of interest and when to pick another. */
  blinkAt: number; blink2: boolean; lookAt: THREE.Vector3; nextLook: number; yaw: number; pitch: number;
  /** Regulars at the other tables hold a glass of tea and drink from it. */
  tea?: boolean;
  /** How far in front of them the table's edge is (body units). */
  edge: number;
  /** Who they are: their own habits, how quickly they move, how they sit (stooped or upright). */
  habits: Idle[]; tempo: number; hunch: number;
}

export function newActor(p: { piv: THREE.Group; head: THREE.Group; arms: THREE.Group[]; elbows: THREE.Group[]; eyes: THREE.Mesh[]; lids: THREE.Mesh[] }, tea = false, edge = 1.2,
  who: { habits?: string[]; tempo?: number; build?: { hunch?: number } } = {}): Actor {
  return { ...p, phase: Math.random() * 6, anim: null, active: false, idle: null, nextIdle: 2 + Math.random() * 5,
    blinkAt: Math.random() * 3, blink2: false, lookAt: new THREE.Vector3(), nextLook: 0, yaw: 0, pitch: 0.1, tea, edge,
    habits: (who.habits as Idle[] | undefined) ?? IDLES, tempo: who.tempo ?? 1, hunch: who.build?.hunch ?? 0 };
}

const IDLES: Idle[] = ['tap', 'tap', 'lean', 'scratch', 'nod', 'chin', 'stretch'];
/** Habits that take a while (counting beads, a song in the head). */
const LONG = new Set<Idle>(['beads', 'groove', 'behindhead', 'folded', 'watch']);
const ease = (k: number) => (k <= 0 ? 0 : k >= 1 ? 1 : k * k * (3 - 2 * k));
/** 0 → 1 → 0 over an action: quick in, hold, quick out. */
const hold = (k: number, edge = 0.18) => ease(Math.min(k / edge, (1 - k) / edge, 1));
const clamp = (v: number, a: number, b: number) => Math.max(a, Math.min(b, v));
const v3 = new THREE.Vector3(), m4 = new THREE.Matrix4();

/* ---------------- arms: reach for a point ---------------- */
const _t = new THREE.Vector3(), _e = new THREE.Vector3(), _u = new THREE.Vector3(), _x = new THREE.Vector3(), _y = new THREE.Vector3(), _z = new THREE.Vector3(), _m = new THREE.Matrix4(), _q = new THREE.Quaternion();
const _inv = new THREE.Quaternion();
/**
 * Point an arm so its palm reaches `target` (in the body's frame: facing +z, y up, origin at the seat).
 * `sx` is -1 for the left arm, +1 for the right. The elbow stays out to the side and down, like a person's.
 */
export function reach(piv: THREE.Object3D, arm: THREE.Object3D, elbow: THREE.Object3D, sx: number, target: THREE.Vector3, back = 0) {
  const sh = arm.parent;
  if (!sh) return;
  sh.rotation.set(0, 0, 0);
  // the target from the shoulder, in the torso's frame (the torso leans and bobs)
  _inv.copy(piv.quaternion).invert();
  const v = _t.copy(target).sub(piv.position).applyQuaternion(_inv).divideScalar(piv.scale.x || 1).sub(sh.position);   // (taller people: a scaled torso)
  const L1 = UPPER_ARM, L2 = ELBOW_TO_PALM;
  const d = clamp(v.length(), 0.35, L1 + L2 - 0.02);
  const dir = v.normalize();
  const a = Math.acos(clamp((L1 * L1 + d * d - L2 * L2) / (2 * L1 * d), -1, 1));         // shoulder: off the line to the hand
  const bend = Math.PI - Math.acos(clamp((L1 * L1 + L2 * L2 - d * d) / (2 * L1 * L2), -1, 1));   // elbow
  // which way the elbow points: out, down and a little back
  // (`back` 0-1: hanging or swinging arms point their elbows backward instead, close to the body)
  const hx = sx * (0.7 - 0.55 * back), hy = -0.75 + 0.6 * back, hz = -0.45 - 0.55 * back;
  _e.set(hx, hy, hz).addScaledVector(dir, -_e.set(hx, hy, hz).dot(dir));
  if (_e.lengthSq() < 1e-6) _e.set(sx, 0, 0);
  _e.normalize();
  const u = _u.copy(dir).multiplyScalar(Math.cos(a)).addScaledVector(_e, Math.sin(a)).normalize();   // upper arm
  // the arm's frame: its -y runs down the upper arm, the elbow bends toward its +z
  _y.copy(u).negate();
  _z.copy(dir).addScaledVector(u, -u.dot(dir));
  if (_z.lengthSq() < 1e-6) _z.copy(_e).negate().addScaledVector(u, -u.dot(_e)); // arm straight: any side
  _z.normalize();
  _x.crossVectors(_y, _z).normalize();
  _m.makeBasis(_x, _y, _z);
  arm.quaternion.copy(_q.setFromRotationMatrix(_m));
  elbow.rotation.set(-bend, 0, 0);
}

/** Something worth looking at: the board, a player's head, you (the camera). */
export interface Sights { board: THREE.Vector3; heads: THREE.Vector3[]; you: THREE.Vector3; activeHead: THREE.Vector3 | null }

const HEAD_Y = 1.87, CHIN_Y = 1.32, TOP_Y = 2.55;
const L = new THREE.Vector3(), R = new THREE.Vector3(), tmp = new THREE.Vector3();
/** Blend a hand target toward another by k. */
const mix = (out: THREE.Vector3, x: number, y: number, z: number, k: number) => out.lerp(tmp.set(x, y, z), k);

/**
 * Pose a seated person for this frame. `body` is their group (facing +z, the table top at y = 0), `t` the
 * scene clock, `amp` 0 when the player asked for reduced motion.
 */
export function act(body: THREE.Object3D, u: Actor, t: number, dt: number, amp: number, sights: Sights, self: THREE.Vector3 | null) {
  const E = u.edge;
  let yawTo = 0;
  let lean = (u.active ? 0.16 : 0.04) + u.hunch, headDown = 0, bob = 0, headTilt = 0, nod = 0, shake = 0;
  // hands at rest: forearms on the table, hands a little in from the edge
  const br = Math.sin(t * 1.6 * (0.8 + 0.2 * u.tempo) + u.phase) * amp;
  L.set(-0.85, 0.16, E + 0.75 + br * 0.02); R.set(0.85, 0.16, E + 0.75 - br * 0.02);
  if (u.tea) R.set(0.75, 0.55, E + 0.35);   // the regulars hold their tea
  u.piv.position.y = -1.75 + br * 0.03;

  /* waiting for others: a little fidget now and then */
  if (!u.active && !u.anim && amp > 0) {
    if (!u.idle && t > u.nextIdle) { const kind = u.tea ? 'sip' : u.habits[Math.floor(Math.random() * u.habits.length)]; u.idle = { kind, t0: t, d: (LONG.has(kind) ? 3.2 : 1.6 + Math.random() * 1.6) / u.tempo }; }
    if (u.idle) {
      const k = (t - u.idle.t0) / u.idle.d, h = hold(k);
      if (k >= 1) { u.idle = null; u.nextIdle = t + ((u.tea ? 2 : 2.5) + Math.random() * 4.5) / u.tempo; }
      else switch (u.idle.kind) {
        case 'tap': R.y += Math.abs(Math.sin(k * Math.PI * 9)) * 0.14 * h; break;                         // drumming fingers
        case 'lean': lean -= 0.14 * h; mix(L, -0.9, 0.12, E + 0.15, h); mix(R, 0.9, 0.12, E + 0.15, h); break;   // sit back, hands at the edge
        case 'scratch': mix(L, -0.4, TOP_Y - 0.15 + Math.sin(k * 40) * 0.04, 0.05, h); headTilt = 0.12 * h; break;
        case 'nod': nod = Math.sin(k * Math.PI * 4) * 0.14 * h; break;
        case 'chin': mix(R, 0.08, CHIN_Y - 0.05, 0.62, h); headTilt = -0.1 * h; lean += 0.06 * h; break;   // chin on the hand
        case 'stretch': mix(L, -0.75, 3.7, -0.1, h); mix(R, 0.75, 3.7, -0.1, h); lean -= 0.1 * h; headDown = -0.25 * h; break;
        case 'sip': mix(R, 0.12, CHIN_Y + 0.05, 0.55, h); headDown = -0.1 * h; break;
        // their own habits
        case 'glasses': mix(R, 0.04, HEAD_Y + 0.04, 0.74, h); headDown = 0.08 * h; break;                                   // pushes his glasses up
        case 'mustache': mix(R, Math.sin(k * Math.PI * 4) * 0.12, CHIN_Y + 0.16, 0.74, h); headTilt = 0.06 * h; break;          // strokes his moustache
        case 'watch': mix(L, 0.05, 1.05, 1.15, h); headDown = 0.35 * h; yawTo = 0.25 * h; break;                             // checks his watch
        case 'knuckles': mix(L, -0.12, 0.55, 1.3, h); mix(R, 0.12, 0.55, 1.3, h); lean += Math.sin(k * Math.PI * 6) * 0.03 * h; break;   // cracks his knuckles
        case 'behindhead': lean -= 0.2 * h; mix(L, -0.5, TOP_Y - 0.2, -0.25, h); mix(R, 0.5, TOP_Y - 0.2, -0.25, h); headDown = -0.12 * h; break;   // hands behind the head
        case 'drum': L.y += Math.abs(Math.sin(k * Math.PI * 12)) * 0.15 * h; R.y += Math.abs(Math.cos(k * Math.PI * 12)) * 0.15 * h; nod = Math.sin(k * Math.PI * 12) * 0.04 * h; break;   // drums on the table
        case 'groove': nod = Math.sin(k * Math.PI * 8) * 0.1 * h; headTilt = Math.sin(k * Math.PI * 4) * 0.08 * h; lean += Math.sin(k * Math.PI * 8) * 0.03 * h; break;   // a song in his head
        case 'hijab': mix(R, 0.5, HEAD_Y + 0.15, 0.25, h); headTilt = -0.1 * h; break;                                       // straightens her hijab
        case 'folded': mix(L, 0.25, 0.4, E + 0.35, h); mix(R, -0.25, 0.5, E + 0.4, h); lean -= 0.05 * h; break;             // arms folded on the table
        case 'hair': mix(R, 0.52, HEAD_Y + 0.08, 0.15, h); headTilt = -0.14 * h; break;                                      // tucks her hair behind the ear
        case 'beads': { const c = k * Math.PI * 10; mix(L, -0.3 + Math.sin(c) * 0.05, 0.75 + Math.cos(c) * 0.06, 0.95, h); headDown = 0.1 * h; break; }   // counts his prayer beads
        case 'beard': mix(R, 0.02, CHIN_Y - 0.15 - Math.sin(k * Math.PI * 3) * 0.12, 0.66, h); break;                       // strokes his beard
      }
    }
  } else u.idle = null;

  /* their turn: thinking, chin on the hand now and then, eyes on the board */
  if (u.active && !u.anim) {
    const th = clamp(0.5 + 0.8 * Math.sin(t * 0.6 + u.phase), 0, 1);
    mix(R, 0.08, CHIN_Y - 0.05, 0.62, th); headTilt = -0.07 * th;
  }

  /* things that happen: playing a piece, drawing, passing, winning, losing, emotes */
  if (u.anim) {
    const k = u.anim.fix ?? (t - u.anim.t0) / u.anim.d, h = hold(k, 0.22), s = Math.sin(clamp(k, 0, 1) * Math.PI);
    if (k >= 1) u.anim = null;
    else switch (u.anim.type) {
      case 'play': mix(R, 0.25, 0.25, E + 2.6, s); lean += 0.14 * s; break;          // reach into the middle
      case 'draw': mix(L, -1.2, 0.25, E + 2.2, s); lean += 0.1 * s; break;
      case 'pass': shake = Math.sin(k * Math.PI * 6) * 0.5 * (1 - k); mix(L, -1.0, 0.9, E + 0.4, h); mix(R, 1.0, 0.9, E + 0.4, h); lean -= 0.08 * h; break;   // shrug, palms up
      case 'win': bob = Math.abs(Math.sin(k * Math.PI * 4)) * 0.6 * (1 - k * 0.5); mix(L, -1.1, 3.9, 0.35, h); mix(R, 1.1, 3.9, 0.35, h); headDown = -0.3 * h; break;
      case 'lose': lean += 0.08 * h; headDown = 0.4 * h; shake = Math.sin(k * Math.PI * 3) * 0.2 * h; mix(L, -0.05, HEAD_Y + 0.05, 0.62, h); break;   // hand over the eyes
      case 'emote': {
        const w = Math.sin(k * Math.PI * 6);
        switch (u.anim.k) {
          case 'hi': mix(R, 1.15 + w * 0.3, 2.45, 1.05, h); break;                                                     // wave
          case 'clap': mix(L, -0.18 - Math.max(0, w) * 0.25, 1.0, 1.25, h); mix(R, 0.18 + Math.max(0, w) * 0.25, 1.0, 1.25, h); break;
          case 'lol': lean -= (0.12 + Math.abs(w) * 0.06) * h; headDown = -0.35 * h; bob = Math.abs(Math.sin(k * Math.PI * 10)) * 0.1 * h; mix(L, -0.5, 0.0, 0.9, h); break;   // laughing, hand on the belly
          case 'angry': { const up = Math.max(0, Math.sin(k * Math.PI * 4)); mix(R, 0.55, 0.15 + up * 1.0, E + 0.8 - up * 0.4, h); headDown = 0.15 * h; break; }   // fist on the table
          case 'think': mix(R, 0.08, CHIN_Y - 0.05, 0.62, h); headTilt = -0.18 * h; break;
          case 'wow': case 'oops': mix(L, -0.42, TOP_Y - 0.1, 0.2, h); mix(R, 0.42, TOP_Y - 0.1, 0.2, h); headDown = -0.12 * h; break;   // hands on the head
          case 'hurry': R.y += Math.abs(Math.sin(k * Math.PI * 14)) * 0.2 * h; nod = Math.sin(k * Math.PI * 14) * 0.05 * h; break;   // tapping on the table
          case 'love': mix(L, -0.25, 1.15, 0.75, h); mix(R, 0.25, 1.15, 0.75, h); nod = Math.sin(k * Math.PI * 3) * 0.08 * h; break;   // hands on the heart
          default: mix(R, 0.7, 1.3, E + 0.6, h); nod = Math.sin(k * Math.PI * 4) * 0.12 * h; break;            // gg, nice, fire: thumbs up and a nod
        }
        break;
      }
    }
  }

  /* where to look: the board, whoever is playing, someone else, or now and then you */
  if (t > u.nextLook) {
    const r = Math.random();
    let to: THREE.Vector3 = sights.board;
    if (u.active) to = r < 0.85 ? sights.board : sights.you;
    else if (sights.activeHead && sights.activeHead !== self && r < 0.45) to = sights.activeHead;
    else if (r < 0.62) to = sights.board;
    else if (r < 0.8) to = sights.you;
    else { const others = sights.heads.filter(hd => hd !== self); if (others.length) to = others[Math.floor(Math.random() * others.length)]; }
    u.lookAt.copy(to); u.nextLook = t + 1.2 + Math.random() * 3;
    if (Math.random() < 0.6) u.blinkAt = Math.min(u.blinkAt, t + 0.05);    // eyes blink when the gaze jumps
  }
  m4.copy(body.matrixWorld).invert();
  const local = v3.copy(u.lookAt).applyMatrix4(m4);
  const wantYaw = clamp(Math.atan2(local.x, local.z), -1.0, 1.0);
  const wantPitch = clamp(Math.atan2(HEAD_Y - local.y, Math.hypot(local.x, local.z)) * 0.8, -0.35, 0.6);
  const kk = 1 - Math.exp(-dt * 5);
  u.yaw += (wantYaw * 0.75 - u.yaw) * kk; u.pitch += (wantPitch * 0.7 - u.pitch) * kk;
  u.head.rotation.set(clamp(u.pitch + headDown + nod, -0.6, 0.8), u.yaw * (1 - Math.min(1, Math.abs(yawTo) * 4)) - yawTo + shake, headTilt, 'YXZ');
  for (const e of u.eyes) {
    const rest = e.userData.rest as THREE.Euler;
    e.rotation.set(rest.x + clamp((wantPitch - u.pitch) * 0.6, -0.25, 0.25), rest.y + clamp(wantYaw - u.yaw, -0.45, 0.45), rest.z);
  }
  let closed = 0;
  if (t > u.blinkAt) {
    const bk = (t - u.blinkAt) / 0.16;
    if (bk >= 1) { if (u.blink2) { u.blink2 = false; u.blinkAt = t + 0.08; } else { u.blink2 = Math.random() < 0.2; u.blinkAt = t + 2 + Math.random() * 3.5; } }
    else closed = Math.sin(bk * Math.PI);
  }
  for (const l of u.lids) l.rotation.x = (l.userData.open as number) + closed * 0.85;

  u.piv.rotation.x += (lean - u.piv.rotation.x) * (1 - Math.exp(-dt * 6));
  u.piv.updateMatrix();
  reach(u.piv, u.arms[0], u.elbows[0], -1, L);
  reach(u.piv, u.arms[1], u.elbows[1], 1, R);
  body.position.y = bob;
}
