import { Haptics, ImpactStyle } from '@capacitor/haptics';

/** Small synthesized sound set: no audio files to ship or load. */
let ctx: AudioContext | null = null;
let idle: ReturnType<typeof setTimeout> | undefined;
if (typeof document !== 'undefined') document.addEventListener('visibilitychange', () => { if (document.hidden && ctx?.state === 'running') ctx.suspend().catch(() => {}); });
export const prefs = { sound: true, vibrate: true };

function ac(): AudioContext | null {
  if (!prefs.sound) return null;
  try {
    if (!ctx) ctx = new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)();
    if (ctx.state === 'suspended') ctx.resume();
    // an open audio output keeps the phone's sound chip awake: let it sleep a few seconds after the last sound
    clearTimeout(idle); idle = setTimeout(() => { if (ctx?.state === 'running') ctx.suspend().catch(() => {}); }, 4000);
    return ctx;
  } catch { return null; }
}

/** Call from the first tap so browsers allow audio. */
export function unlockAudio() { ac(); }

function noiseBurst(dur: number, freq: number, gain: number, when = 0) {
  const c = ac(); if (!c) return;
  const len = Math.floor(c.sampleRate * dur);
  const buf = c.createBuffer(1, len, c.sampleRate);
  const d = buf.getChannelData(0);
  for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 3);
  const src = c.createBufferSource(); src.buffer = buf;
  const f = c.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = freq; f.Q.value = 1.4;
  const g = c.createGain(); g.gain.value = gain;
  src.connect(f).connect(g).connect(c.destination);
  src.start(c.currentTime + when);
}

function tone(freq: number, dur: number, gain: number, when = 0, type: OscillatorType = 'sine') {
  const c = ac(); if (!c) return;
  const o = c.createOscillator(), g = c.createGain();
  o.type = type; o.frequency.value = freq;
  const t0 = c.currentTime + when;
  g.gain.setValueAtTime(0, t0);
  g.gain.linearRampToValueAtTime(gain, t0 + 0.01);
  g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
  o.connect(g).connect(c.destination);
  o.start(t0); o.stop(t0 + dur + 0.05);
}

function buzz(style: ImpactStyle) {
  if (!prefs.vibrate) return;
  // browsers only allow vibration after the player has touched the screen
  if ((navigator as Navigator & { userActivation?: { hasBeenActive: boolean } }).userActivation?.hasBeenActive === false) return;
  Haptics.impact({ style }).catch(() => { try { navigator.vibrate?.(style === ImpactStyle.Heavy ? 40 : 15); } catch { /* not supported */ } });
}

export const sfx = {
  place() { noiseBurst(0.09, 2400, 0.9); noiseBurst(0.05, 1200, 0.5, 0.025); buzz(ImpactStyle.Light); },
  draw() { noiseBurst(0.16, 900, 0.35); },
  pass() { tone(330, 0.18, 0.12, 0, 'triangle'); tone(247, 0.22, 0.1, 0.12, 'triangle'); },
  yourTurn() { tone(660, 0.16, 0.12); tone(880, 0.22, 0.12, 0.12); buzz(ImpactStyle.Medium); },
  tick() { tone(1200, 0.05, 0.05, 0, 'square'); },
  roundWin() { [523, 659, 784].forEach((f, i) => tone(f, 0.3, 0.12, i * 0.1, 'triangle')); buzz(ImpactStyle.Heavy); },
  roundLose() { [392, 330, 262].forEach((f, i) => tone(f, 0.3, 0.1, i * 0.12, 'triangle')); },
  matchWin() { [523, 659, 784, 1047, 784, 1047].forEach((f, i) => tone(f, 0.35, 0.13, i * 0.12, 'triangle')); buzz(ImpactStyle.Heavy); },
  dice() { for (let i = 0; i < 4; i++) noiseBurst(0.04, 1800 + i * 300, 0.5, i * 0.07); buzz(ImpactStyle.Light); },
  capture() { noiseBurst(0.12, 1600, 1); tone(220, 0.12, 0.08, 0.02, 'triangle'); buzz(ImpactStyle.Medium); },
  pop() { tone(900, 0.07, 0.08); },
  gift() { [1047, 1319, 1568, 2093].forEach((f, i) => tone(f, 0.25, 0.08, i * 0.07)); buzz(ImpactStyle.Light); },
};
