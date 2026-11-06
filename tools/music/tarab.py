# An original café tune in the spirit of 1950s–60s Egyptian light song (oud, qanun, accordion, a violin section,
# darbuka and riq, maqam hijaz on D, the maqsum rhythm). Everything is synthesized here (plucked strings by
# Karplus–Strong, bowed and reed sounds additively), the melody is our own, and the piece loops seamlessly.
#   python3 tarab.py <out.wav> [seed]
import sys
import numpy as np
from scipy.signal import lfilter, butter, sosfilt, fftconvolve

SR = 44100
BPM = 104
E8 = 60 / BPM / 2                    # one eighth note, seconds
rng = np.random.default_rng(int(sys.argv[2]) if len(sys.argv) > 2 else 3)
out_f = sys.argv[1]

NOTE = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}
def hz(n):
    """'D4', 'Eb4', 'F#4', 'Eq4' (q = quarter flat)."""
    name, octv = n[:-1], int(n[-1])
    s = NOTE[name[0]] + (-1 if 'b' in name[1:] else 0) + (1 if '#' in name[1:] else 0) + (-0.5 if 'q' in name[1:] else 0)
    return 440 * 2 ** ((s + 12 * (octv + 1) - 69) / 12)

def env(n, a, d, s=1.0, r=0.05):
    e = np.ones(n) * s
    na, nr = int(a * SR), int(r * SR)
    if na: e[:na] = np.linspace(0, 1, na) if na < n else np.linspace(0, 1, n)[:n]
    if nr and nr < n: e[-nr:] *= np.linspace(1, 0, nr)
    if d: e *= np.exp(-np.arange(n) / SR / d)
    return e

def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype='band', fs=SR, output='sos'), x)
def lowpass(x, f, order=2):
    return sosfilt(butter(order, f, btype='low', fs=SR, output='sos'), x)
def highpass(x, f, order=2):
    return sosfilt(butter(order, f, btype='high', fs=SR, output='sos'), x)

# ---------- instruments ----------
def pluck(f, dur, bright=0.5, decay=0.995, ring=1.6):
    """Karplus–Strong string: a noise burst through a tuned delay line that loses a little each pass."""
    n = int((dur + ring) * SR)
    N = SR / f; Ni = int(N); frac = N - Ni
    burst = rng.uniform(-1, 1, Ni + 2)
    burst = lowpass(burst, 1200 + bright * 7000)
    x = np.zeros(n); x[:len(burst)] = burst
    # y[n] = x[n] + g*((1-frac)*y[n-Ni] + frac*y[n-Ni-1]) averaged (the loss filter)
    a = np.zeros(Ni + 3); a[0] = 1
    g = decay
    a[Ni] -= g * (1 - frac) * 0.5; a[Ni + 1] -= g * 0.5; a[Ni + 2] -= g * frac * 0.5
    y = lfilter([1], a, x)
    # the note stops (a finger damps it) a little after its length
    stop = int(dur * SR)
    if stop < n: y[stop:] *= np.exp(-np.arange(n - stop) / SR / 0.25)
    return y / (np.max(np.abs(y)) + 1e-9)

def oud(f, dur):
    s = pluck(f, dur, bright=0.35, decay=0.993, ring=1.2)
    body = bandpass(s, 90, 2400) + 0.6 * bandpass(s, 180, 320) + 0.3 * bandpass(s, 700, 1100)
    return body * 0.6

def qanun(f, dur):
    # three strings per note, very slightly apart
    s = sum(pluck(f * c, dur, bright=0.8, decay=0.996, ring=1.8) for c in (1, 1.0018, 0.9984)) / 3
    return highpass(s, 180) * 0.55

def bowed(f, dur, voices=6, vib=5.2, depth=0.006, bright=3200):
    """A section of violins in unison (the big Egyptian string sound)."""
    n = int((dur + 0.25) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for v in range(voices):
        det = 1 + rng.normal(0, 0.0025)
        ph0 = rng.uniform(0, 2 * np.pi)
        vibr = np.sin(2 * np.pi * (vib + rng.normal(0, .3)) * t + ph0) * depth * np.clip(t / 0.25, 0, 1)
        phase = 2 * np.pi * np.cumsum(f * det * (1 + vibr)) / SR
        saw = sum(np.sin(k * phase) / k for k in range(1, 14))
        y += saw
    y = lowpass(y, bright, 2) + 0.4 * bandpass(y, 2400, 3600)
    return y * env(n, 0.09, 0, 1, 0.22) / voices * 0.5

def accordion(f, dur):
    n = int((dur + 0.08) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for det in (1.0, 1.0035, 0.9968):   # musette: three reeds, a little apart
        ph = 2 * np.pi * f * det * t
        y += np.sign(np.sin(ph)) * 0.35 + np.sin(2 * ph) * 0.3 + np.sin(ph) * 0.5
    y = lowpass(y, 2600) * (1 + 0.06 * np.sin(2 * np.pi * 6 * t))
    return y * env(n, 0.03, 0, 1, 0.08) * 0.18

def doum(vel=1.0):
    n = int(0.5 * SR); t = np.arange(n) / SR
    f = 62 + 55 * np.exp(-t * 28)
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 7)
    y += lowpass(rng.uniform(-1, 1, n), 900) * np.exp(-t * 90) * 0.4
    return y * vel * 0.9

def tek(vel=1.0, soft=False):
    n = int(0.18 * SR); t = np.arange(n) / SR
    y = bandpass(rng.uniform(-1, 1, n), 1800, 6500) * np.exp(-t * (75 if soft else 55))
    y += np.sin(2 * np.pi * 720 * t) * np.exp(-t * 40) * 0.25
    return y * vel * (0.35 if soft else 0.7)

def riq(vel=1.0):
    n = int(0.25 * SR); t = np.arange(n) / SR
    y = highpass(rng.uniform(-1, 1, n), 5500) * np.exp(-t * 22)
    for f in (5200, 7400, 9100): y += np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) * np.exp(-t * 18) * 0.12
    return y * vel * 0.16

def bass(f, dur):
    s = pluck(f, dur, bright=0.15, decay=0.997, ring=0.6)
    return lowpass(s, 600) * 0.7

# ---------- the score (eighth notes; '-' is a rest) ----------
def bar(*items):
    """('D4', 2), ('F#4', 1) …: notes and their lengths in eighths (a bar has 8)."""
    return list(items)
A = [
    bar(('D4', 2), ('F#4', 1), ('G4', 1), ('A4', 3), ('Bb4', 1)),
    bar(('A4', 2), ('G4', 1), ('F#4', 1), ('G4', 2), ('A4', 2)),
    bar(('Bb4', 1), ('C5', 1), ('D5', 2), ('C5', 1), ('Bb4', 1), ('A4', 2)),
    bar(('G4', 1), ('A4', 1), ('F#4', 2), ('Eb4', 2), ('D4', 2)),
    bar(('D5', 2), ('C5', 1), ('Bb4', 1), ('A4', 2), ('Bb4', 1), ('C5', 1)),
    bar(('D5', 3), ('Eb5', 1), ('D5', 2), ('C5', 2)),
    bar(('Bb4', 1), ('A4', 1), ('G4', 1), ('F#4', 1), ('G4', 2), ('Eb4', 2)),
    bar(('D4', 6), ('-', 2)),
]
B = [
    bar(('G4', 2), ('A4', 1), ('Bb4', 1), ('C5', 2), ('Bb4', 2)),
    bar(('A4', 2), ('G4', 2), ('-', 4)),
    bar(('C5', 2), ('D5', 1), ('Eb5', 1), ('D5', 2), ('C5', 2)),
    bar(('Bb4', 2), ('A4', 2), ('-', 4)),
    bar(('D5', 1), ('Eb5', 1), ('F#5', 2), ('G5', 2), ('F#5', 1), ('Eb5', 1)),
    bar(('D5', 2), ('C5', 1), ('Bb4', 1), ('A4', 2), ('G4', 2)),
    bar(('F#4', 1), ('G4', 1), ('A4', 1), ('Bb4', 1), ('A4', 2), ('F#4', 1), ('Eb4', 1)),
    bar(('D4', 6), ('-', 2)),
]
# the qanun answers the accordion's open bars (B2, B4) — the call and response of the old songs
ANSWER = {1: [('D5', 1), ('C5', 1), ('Bb4', 1), ('A4', 1)], 3: [('Eb5', 1), ('D5', 1), ('C5', 1), ('Bb4', 1)]}
ROOT_A = ['D2', 'D2', 'G2', 'D2', 'G2', 'C2', 'G2', 'D2']
ROOT_B = ['G2', 'G2', 'C2', 'G2', 'D2', 'G2', 'D2', 'D2']

# sections: intro (4), A oud (8), B accordion (8), A strings + oud (8), turnaround (2)
plan = [('intro', None, None)] * 4 + [('A', i, 'oud') for i in range(8)] + [('B', i, 'acc') for i in range(8)] + \
       [('A', i, 'str') for i in range(8)] + [('turn', 0, None), ('turn', 1, None)]
BAR = 8 * E8
total = int((len(plan) * BAR) * SR)
TAIL = int(3.0 * SR)
mix = {k: np.zeros(total + TAIL) for k in ('mel', 'qanun', 'str', 'drum', 'bass', 'riq')}
def put(track, at, snd, g=1.0):
    i = int(at * SR); n = min(len(snd), len(mix[track]) - i)
    if n > 0: mix[track][i:i + n] += snd[:n] * g

def play_line(track, notes, t0, inst, g=1.0, octave=0):
    t = t0
    for n, l in notes:
        if n != '-':
            f = hz(n) * (2 ** octave)
            put(track, t + rng.normal(0, 0.006), inst(f, l * E8), g * (1 + rng.normal(0, 0.05)))
        t += l * E8

def tremolo(track, n, t0, beats8, g=0.5):
    """The qanun's rolled note: fast repeated plucks."""
    f = hz(n); k = int(beats8 * E8 / 0.07)
    for i in range(k): put(track, t0 + i * 0.07, qanun(f, 0.12), g * (0.7 + 0.3 * np.sin(i / k * np.pi)))

for b, (sec, i, who) in enumerate(plan):
    t0 = b * BAR
    # ---- drums: maqsum  D T - T D - T -  (and riq on the eighths)
    intensity = {'intro': 0.6 if b >= 2 else 0, 'A': 0.85 if who == 'oud' else 1.0, 'B': 0.9, 'turn': 1.0}[sec]
    if intensity:
        for pos, kind in [(0, 'D'), (1, 'T'), (3, 'T'), (4, 'D'), (6, 'T')]:
            at = t0 + pos * E8
            put('drum', at, doum(intensity) if kind == 'D' else tek(intensity))
        for pos in (2, 5, 7): put('drum', t0 + pos * E8, tek(intensity * 0.8, soft=True))
        for pos in range(8): put('riq', t0 + pos * E8 + (0.012 if pos % 2 else 0), riq(intensity * (1.0 if pos % 2 == 0 else 0.6)))
    if sec == 'turn' and i == 1:
        # a darbuka roll into the top (the loop comes back to the intro)
        for k in range(12): put('drum', t0 + 4 * E8 + k * E8 / 3, tek(0.5 + k / 24, soft=k % 2 == 1))
    # ---- bass on the doums
    root = {'intro': 'D2', 'A': ROOT_A[i] if i is not None else 'D2', 'B': ROOT_B[i] if i is not None else 'D2', 'turn': 'D2' if i == 0 else 'A1'}[sec]
    if sec != 'intro' or b >= 2:
        put('bass', t0, bass(hz(root), 3 * E8), 0.9)
        put('bass', t0 + 4 * E8, bass(hz(root) * (1.5 if sec == 'turn' else 1), 3 * E8), 0.7)
    # ---- melody
    if sec == 'intro':
        tremolo('qanun', ['A4', 'A4', 'D5', 'A4'][b], t0, 4, 0.45)
        tremolo('qanun', ['D4', 'F#4', 'G4', 'Eb4'][b], t0 + 4 * E8, 4, 0.4)
    elif sec == 'A' and who == 'oud':
        play_line('mel', A[i], t0, oud, 1.0)
        if i in (3, 7): tremolo('qanun', 'D5', t0 + 6 * E8, 2, 0.3)
    elif sec == 'B':
        play_line('mel', B[i], t0, accordion, 1.0)
        if i in ANSWER: play_line('qanun', ANSWER[i], t0 + 4 * E8, qanun, 0.7)
    elif sec == 'A' and who == 'str':
        play_line('str', A[i], t0, bowed, 1.0, octave=1)   # the violins an octave up
        play_line('mel', A[i], t0, oud, 0.7)               # with the oud underneath
    elif sec == 'turn':
        play_line('qanun', [('A4', 1), ('Bb4', 1), ('A4', 1), ('G4', 1), ('F#4', 1), ('G4', 1), ('Eb4', 1), ('D4', 1)] if i == 0 else [('D4', 4), ('-', 4)], t0, qanun, 0.6)

# ---------- the room (a small hall), the mix, and the loop ----------
def reverb(x, secs=1.6, wet=0.22):
    n = int(secs * SR); t = np.arange(n) / SR
    ir = rng.normal(0, 1, n) * np.exp(-t * 6.9 / secs)
    ir = lowpass(ir, 5000); ir[0] = 0
    w = fftconvolve(x, ir)[:len(x)]
    w *= np.max(np.abs(x)) / (np.max(np.abs(w)) + 1e-9)
    return x * (1 - wet) + w * wet

# levels by loudness (RMS) so the tune leads and the drums sit under it, as background music should
gains = {'mel': 1.0, 'qanun': 0.6, 'str': 0.85, 'drum': 0.42, 'bass': 0.38, 'riq': 0.16}
pans = {'mel': -0.1, 'qanun': 0.35, 'str': -0.25, 'drum': 0.0, 'bass': 0.0, 'riq': 0.3}
L = np.zeros(total + TAIL); R = np.zeros(total + TAIL)
for k, x in mix.items():
    on = np.abs(x) > 1e-4
    rms = np.sqrt(np.mean(x[on] ** 2)) if on.any() else 1
    x = reverb(x / (rms + 1e-9) * 0.1, wet=0.3 if k in ('str', 'qanun') else 0.18) * gains[k]
    p = pans[k]; L += x * np.sqrt((1 - p) / 2); R += x * np.sqrt((1 + p) / 2)
# the loop: what rings past the end is laid over the start, so it repeats without a seam
for ch in (L, R): ch[:TAIL] += ch[total:total + TAIL]
L, R = L[:total], R[:total]
# a little old-record warmth: gentle saturation and a softer top
L, R = np.tanh(L * 1.3) / 1.3, np.tanh(R * 1.3) / 1.3
L, R = lowpass(L, 9500), lowpass(R, 9500)
peak = max(np.max(np.abs(L)), np.max(np.abs(R)))
st = (np.stack([L, R], 1) / peak * 0.89 * 32767).astype(np.int16)
import wave
w = wave.open(out_f, 'wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(st.tobytes()); w.close()
print('wrote', out_f, round(total / SR, 1), 's')
