import * as THREE from 'three';

/*
 * The people at the tables, alive: they breathe, blink, look at the board, at whoever is playing and now and
 * then at you; they think on their turn, fidget while they wait, react to wins and losses and act out emotes.
 * Pure posing: every frame `act` sets each joint from the character's state, nothing accumulates.
 */

export type ActKind = 'play' | 'draw' | 'pass' | 'win' | 'lose' | 'emote';
export interface Act { type: ActKind; t0: number; d: number; k?: string; /** test hook: hold the action at this point (0-1) */ fix?: number }
type Idle = 'tap' | 'lean' | 'scratch' | 'nod' | 'stretch' | 'chin' | 'sip';
export interface Actor {
  piv: THREE.Group; head: THREE.Group; arms: THREE.Group[]; elbows: THREE.Group[]; eyes: THREE.Mesh[]; lids: THREE.Mesh[];
  phase: number; anim: Act | null; active: boolean;
  /** What they do with their hands while waiting, and until when. */
  idle: { kind: Idle; t0: number; d: number } | null; nextIdle: number;
  /** Blinking: next blink time; looking: the current point of interest and when to pick another. */
  blinkAt: number; blink2: boolean; lookAt: THREE.Vector3; nextLook: number; yaw: number; pitch: number;
  /** Regulars at the other tables hold a glass of tea and drink from it. */
  tea?: boolean;
}

export function newActor(p: { piv: THREE.Group; head: THREE.Group; arms: THREE.Group[]; elbows: THREE.Group[]; eyes: THREE.Mesh[]; lids: THREE.Mesh[] }, tea = false): Actor {
  return { ...p, phase: Math.random() * 6, anim: null, active: false, idle: null, nextIdle: 2 + Math.random() * 5,
    blinkAt: Math.random() * 3, blink2: false, lookAt: new THREE.Vector3(), nextLook: 0, yaw: 0, pitch: 0.1, tea };
}

const IDLES: Idle[] = ['tap', 'tap', 'lean', 'scratch', 'nod', 'chin', 'stretch'];
const ease = (k: number) => (k <= 0 ? 0 : k >= 1 ? 1 : k * k * (3 - 2 * k));
/** 0 → 1 → 0 over an action: quick in, hold, quick out. */
const hold = (k: number, edge = 0.18) => ease(Math.min(k / edge, (1 - k) / edge, 1));
const clamp = (v: number, a: number, b: number) => Math.max(a, Math.min(b, v));
const v3 = new THREE.Vector3(), m4 = new THREE.Matrix4();

/** Something worth looking at: the board, a player's head, you (the camera). */
export interface Sights { board: THREE.Vector3; heads: THREE.Vector3[]; you: THREE.Vector3; activeHead: THREE.Vector3 | null }

/**
 * Pose a seated person for this frame. `body` is their group (facing +z), `t` the scene clock, `amp` 0 when the
 * player asked for reduced motion.
 */
export function act(body: THREE.Object3D, u: Actor, t: number, dt: number, amp: number, sights: Sights, self: THREE.Vector3 | null) {
  const [A0, A1] = u.arms, [E0, E1] = u.elbows;
  // the resting pose: forearms on the table
  let lean = u.active ? 0.2 : 0.02, headDown = 0, bob = 0;
  let a0 = -1.1, a1 = -1.1, e0 = -0.45, e1 = -0.45, z0 = 0, z1 = 0, headTilt = 0, nod = 0, shake = 0;
  // breathing
  const br = Math.sin(t * 1.6 + u.phase) * amp;
  u.piv.position.y = -1.75 + br * 0.03;

  /* waiting for others: a little fidget now and then */
  if (!u.active && !u.anim && amp > 0) {
    if (!u.idle && t > u.nextIdle) { u.idle = { kind: u.tea ? 'sip' : IDLES[Math.floor(Math.random() * IDLES.length)], t0: t, d: 1.6 + Math.random() * 1.6 }; }
    if (u.idle) {
      const k = (t - u.idle.t0) / u.idle.d, h = hold(k);
      if (k >= 1) { u.idle = null; u.nextIdle = t + (u.tea ? 2 : 3) + Math.random() * 5; }
      else switch (u.idle.kind) {
        case 'tap': a1 -= 0.05 + Math.abs(Math.sin(k * Math.PI * 9)) * 0.12 * h; break;                 // drumming fingers
        case 'lean': lean -= 0.16 * h; a0 += 0.35 * h; a1 += 0.35 * h; e0 += 0.25 * h; e1 += 0.25 * h; break;   // sit back
        case 'scratch': a0 = -1.1 - 1.7 * h; e0 = -0.45 - 1.6 * h; z0 = -0.25 * h; headTilt = 0.15 * h; a0 += Math.sin(k * 40) * 0.05 * h; break;
        case 'nod': nod = Math.sin(k * Math.PI * 4) * 0.14 * h; break;
        case 'chin': a1 = -1.1 - 0.55 * h; e1 = -0.45 - 1.75 * h; z1 = 0.25 * h; headTilt = -0.12 * h; break;   // chin on the hand
        case 'stretch': a0 = a1 = -1.1 - 1.9 * h; e0 = e1 = -0.45 + 0.3 * h; lean -= 0.1 * h; headDown = -0.25 * h; break;
        case 'sip': a1 = -1.6 - 0.5 * h; e1 = -0.6 - 1.1 * h; headDown = -0.1 * h; break;
      }
    } else if (u.tea) { a1 = -1.6; e1 = -0.6; }
  } else { u.idle = null; if (u.tea) { a1 = -1.6; e1 = -0.6; } }

  /* their turn: thinking, chin on the hand, eyes on the board */
  if (u.active && !u.anim) {
    const th = 0.5 + 0.5 * Math.sin(t * 0.7 + u.phase);
    a1 = -1.1 - 0.5 * th; e1 = -0.45 - 1.7 * th; z1 = 0.22 * th; headTilt = -0.08 * th;
  }

  /* things that happen: playing a piece, drawing, passing, winning, losing, emotes */
  if (u.anim) {
    const k = u.anim.fix ?? (t - u.anim.t0) / u.anim.d, h = hold(k, 0.22);
    if (k >= 1) u.anim = null;
    else switch (u.anim.type) {
      case 'play': a1 = -1.1 - Math.sin(k * Math.PI) * 0.9; lean += 0.12 * Math.sin(k * Math.PI); break;
      case 'draw': a0 = -1.1 - Math.sin(k * Math.PI) * 0.7; break;
      case 'pass': shake = Math.sin(k * Math.PI * 6) * 0.5 * (1 - k); a0 += 0.3 * h; a1 += 0.3 * h; lean -= 0.1 * h; break;   // shrug, shake the head
      case 'win': bob = Math.abs(Math.sin(k * Math.PI * 4)) * 0.8 * (1 - k * 0.5); a0 = a1 = -1.1 - 1.8 * h; e0 = e1 = -0.15; headDown = -0.3 * h; break;
      case 'lose': lean -= 0.05 * h; headDown = 0.45 * h; shake = Math.sin(k * Math.PI * 3) * 0.25 * h; a0 = -1.1 - 1.35 * h; e0 = -0.45 - 1.85 * h; z0 = -0.15 * h; break;   // head down, hand over the eyes
      case 'emote': {
        const w = Math.sin(k * Math.PI * 6);
        switch (u.anim.k) {
          case 'hi': a1 = -1.1 - 1.7 * h; e1 = -0.3 - 0.4 * h; z1 = (-0.2 + w * 0.35) * h; break;                                      // wave
          case 'clap': a0 = a1 = -1.1 - 0.4 * h; e0 = e1 = -0.45 - 0.9 * h; z0 = (0.35 + w * 0.12) * h; z1 = -(0.35 + w * 0.12) * h; break;
          case 'lol': lean -= (0.12 + Math.abs(w) * 0.08) * h; headDown = -0.35 * h; bob = Math.abs(Math.sin(k * Math.PI * 10)) * 0.12 * h; break;   // laughing, head back
          case 'angry': a1 = -1.1 - 1.2 * Math.max(0, Math.sin(k * Math.PI * 4)) * h; e1 = -0.45 - 0.6 * h; headDown = 0.15 * h; break;   // fist on the table
          case 'think': a1 = -1.1 - 0.55 * h; e1 = -0.45 - 1.75 * h; z1 = 0.25 * h; headTilt = -0.2 * h; break;
          case 'wow': case 'oops': a0 = a1 = -1.1 - 1.55 * h; e0 = e1 = -0.45 - 1.6 * h; z0 = -0.3 * h; z1 = 0.3 * h; headDown = -0.15 * h; break;   // hands on the head
          case 'hurry': a1 -= Math.abs(Math.sin(k * Math.PI * 14)) * 0.18 * h; nod = Math.sin(k * Math.PI * 14) * 0.05 * h; break;   // tapping on the table
          case 'gg': case 'nice': case 'love': case 'fire': default: a1 = -1.1 - 1.25 * h; e1 = -0.45 - 0.9 * h; nod = Math.sin(k * Math.PI * 4) * 0.12 * h; break;   // thumbs up and a nod
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
  // the target in the head's own frame (the body faces +z)
  m4.copy(body.matrixWorld).invert();
  const local = v3.copy(u.lookAt).applyMatrix4(m4);
  const hy = 1.9;   // about head height in the body's frame
  const wantYaw = clamp(Math.atan2(local.x, local.z), -1.0, 1.0);
  const wantPitch = clamp(Math.atan2(hy - local.y, Math.hypot(local.x, local.z)) * 0.8, -0.35, 0.6);
  const k = 1 - Math.exp(-dt * 5);
  u.yaw += (wantYaw * 0.75 - u.yaw) * k; u.pitch += (wantPitch * 0.7 - u.pitch) * k;
  u.head.rotation.set(clamp(u.pitch + headDown + nod, -0.6, 0.8), u.yaw + shake, headTilt, 'YXZ');
  // the eyes do the rest of the turn
  for (const e of u.eyes) {
    const rest = e.userData.rest as THREE.Euler;
    e.rotation.set(rest.x + clamp((wantPitch - u.pitch) * 0.6, -0.25, 0.25), rest.y + clamp(wantYaw - u.yaw, -0.45, 0.45), rest.z);
  }
  // blink every few seconds (sometimes twice)
  let closed = 0;
  if (t > u.blinkAt) {
    const bk = (t - u.blinkAt) / 0.16;
    if (bk >= 1) { if (u.blink2) { u.blink2 = false; u.blinkAt = t + 0.08; } else { u.blink2 = Math.random() < 0.2; u.blinkAt = t + 2 + Math.random() * 3.5; } }
    else closed = Math.sin(bk * Math.PI);
  }
  for (const l of u.lids) l.rotation.x = (l.userData.open as number) + closed * 0.85;

  u.piv.rotation.x += (lean - u.piv.rotation.x) * (1 - Math.exp(-dt * 6));
  A0.rotation.set(a0, 0, z0); A1.rotation.set(a1, 0, z1); E0.rotation.x = e0; E1.rotation.x = e1;
  body.position.y = bob;
}
