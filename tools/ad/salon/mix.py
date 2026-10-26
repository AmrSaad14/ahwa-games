# The engagement-visit advert's sound: the four voices on top; the café music under the evening (dipping under
# every line), cut dead at 2 a.m. for crickets, the clock and a snore, back for the end card; and the hits:
# the case slammed on the glass table, tea glasses rattling, the knee slap, dice, a tile, ludo captures, the
# checkmate, a record scratch on the father's frozen face.
#   python3 mix.py <voice dir> <music.ogg> <ambience.ogg> <duration s> <out.wav>
import sys, json, os, subprocess, wave
import numpy as np

SR = 48000
vdir, music_p, amb_p, D, out_p = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4]), sys.argv[5]
N = int(SR * D)
rng = np.random.default_rng(11)

def load(path):
    raw = subprocess.run(['ffmpeg', '-loglevel', 'error', '-i', path, '-f', 's16le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.int16).astype(np.float32) / 32768
def fit(x): return np.resize(x, N) if len(x) < N else x[:N]

tl = json.load(open(os.path.join(vdir, 'timeline.json')))
L = {l['id']: l for l in tl['lines']}
S = lambda i: L[i]['start']; E = lambda i: L[i]['end']
voice = np.zeros(N, np.float32)
v = load(os.path.join(vdir, 'voice.wav'))[:N]; voice[:len(v)] = v
voice *= 0.92 / max(1e-4, np.percentile(np.abs(v), 99.9))
t = np.arange(N) / SR

# music: low under the opening, groove from the offer, out at 2 a.m., full for the end card
talk = np.zeros(N, np.float32)
for l in tl['lines']: talk[int(l['start'] * SR):int(l['end'] * SR)] = 1
k = int(0.25 * SR); talk = np.convolve(talk, np.ones(k) / k, mode='same')
night0, night1 = E('f4') + 0.35, S('vo') - 0.35
music = fit(load(music_p))
env = np.interp(t, [0, 0.55, 0.7, S('g2'), S('g2') + 0.6, night0 - 0.05, night0, night1, night1 + 0.15, D - 1.0, D], [0, 0, 0.5, 0.5, 1, 1, 0, 0, 1.25, 1.25, 0])
mus = music * env * (0.4 - 0.27 * talk)
amb = fit(load(amb_p)) * 0.0   # (the café murmur doesn't belong in a living room)

fx = np.zeros(N, np.float32)
def add(at, snd, g=1.0):
    i = int(at * SR)
    if i < 0 or i >= N: return
    n = min(len(snd), N - i); fx[i:i + n] += snd[:n] * g
def tt(d): return np.arange(int(d * SR)) / SR
def noise(d): return rng.standard_normal(int(d * SR)).astype(np.float32)
def lowpass(x, n): return np.convolve(x, np.ones(n) / n, mode='same')
def thud(f=70, d=0.5): x = tt(d); return (np.sin(2 * np.pi * f * x * (1 - x * 0.6)) * np.exp(-x * 9) + lowpass(noise(d), 30) * np.exp(-x * 30) * 2.5)
def clink(f): x = tt(0.25); return np.sin(2 * np.pi * f * x) * np.exp(-x * 26) * 0.5
def slap(): x = tt(0.18); return lowpass(noise(0.18), 6) * np.exp(-x * 45) * 1.6
def rattle():
    o = np.zeros(int(0.5 * SR), np.float32)
    for i in range(9):
        c = clink(rng.uniform(1800, 3200)) * 0.6; j = int(rng.uniform(0, 0.42) * SR); o[j:j + len(c)] += c[:len(o) - j]
    return o
def tile(): x = tt(0.2); return (lowpass(noise(0.2), 4) * np.exp(-x * 60) * 1.4 + np.sin(2 * np.pi * 900 * x) * np.exp(-x * 40) * 0.3)
def pop(f=900): x = tt(0.12); return np.sin(2 * np.pi * f * x * (1 + x * 4)) * np.exp(-x * 30) * 0.6
def clack(): x = tt(0.3); return (np.sin(2 * np.pi * 520 * x) * np.exp(-x * 35) + lowpass(noise(0.3), 5) * np.exp(-x * 70)) * 0.9
def scratch():
    x = tt(0.45); f = np.linspace(1400, 180, len(x)); ph = np.cumsum(2 * np.pi * f / SR)
    return (np.sign(np.sin(ph)) * 0.25 + lowpass(noise(0.45), 3) * 0.4) * np.exp(-x * 3)
def whoosh(d=0.5):
    x = tt(d); f = np.exp(np.linspace(np.log(400), np.log(2200), len(x))); ph = np.cumsum(2 * np.pi * f / SR)
    return lowpass(noise(d) * (0.5 + 0.5 * np.sin(ph)), 20) * np.sin(np.pi * x / d) ** 2 * 0.6
def cricket(d):
    x = tt(d); return np.sin(2 * np.pi * 4600 * x) * ((np.sin(2 * np.pi * 28 * x) > 0.3) * (np.sin(2 * np.pi * 1.6 * x) > 0)) * 0.05
def tick(): x = tt(0.03); return lowpass(noise(0.03), 2) * np.exp(-x * 200) * 0.35
def snore():
    x = tt(1.6); return lowpass(noise(1.6), 90) * np.sin(np.pi * np.clip(x / 1.2, 0, 1)) ** 2 * 1.8 * (0.6 + 0.4 * np.sin(2 * np.pi * 34 * x))
def hit():
    x = tt(1.6); return (np.sin(2 * np.pi * 55 * x) * 0.8 + np.sin(2 * np.pi * 110 * x) * 0.3) * np.exp(-x * 3.2)
def chime():
    x = tt(2.2); return sum(np.sin(2 * np.pi * f * x) * a for f, a in [(784, .35), (1175, .25), (1568, .15)]) * np.exp(-x * 2.2)

# 1. the slam (the hand comes down 0.6 s in), the glasses jumping
add(0.6, thud(60, 0.7), 0.9)
for i in range(5): add(0.62 + i * 0.05, clink(2400 + i * 330), 0.7)
# 2.–3. the snatch, the tawla cut-in, the knee slap
add(S('f2') + 0.15, whoosh(0.3), 0.35)
ti0 = E('f2') + 0.15
add(ti0, rattle(), 0.9); add(ti0 + 0.5, thud(150, 0.2), 0.5); add(ti0 + 0.55, thud(170, 0.2), 0.5)
add(S('f3') + 0.65, slap(), 0.9)
# 4. dominoes: the tile, then the record scratch on his face
di0 = E('g3') + 0.15
add(di0 + 0.3, tile(), 1.0); add(di0 + 0.9, tile(), 0.8)
add(S('g4') + 0.5, tile(), 0.6)
add(E('g4') + 0.05, scratch(), 0.8)
# 5. ludo captures, the heads turning
li0 = E('g4') + 1.0
add(li0 + 0.2, pop(900), 0.9); add(li0 + 0.55, pop(1200), 0.9)
add(E('m1') + 0.2, whoosh(0.5), 0.5)
# 6. checkmate
add(E('b1') + 0.4, clack(), 1.0)
# 7. 2 a.m.: crickets, the clock, a snore
nl = night1 - night0
add(night0, cricket(nl), 1.0)
for i in range(int(nl) + 1): add(night0 + 0.1 + i, tick(), 1.0)
add(night0 + 0.25, snore(), 0.45)
add(S('f5') - 0.3, slap() * 0.5, 0.6); add(S('f5') + 0.45, slap() * 0.5, 0.5)
# end card
add(night1 + 0.1, hit(), 0.6); add(night1 + 0.7, chime(), 0.4)

mix = voice + mus + amb + fx
mix = np.tanh(mix * 0.95) * 0.95
side = np.roll(mus, int(0.012 * SR)) - mus
st = np.stack([mix + side * 0.18, mix - side * 0.18], 1).clip(-1, 1)
w = wave.open(out_p, 'wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((st * 32767).astype(np.int16).tobytes()); w.close()
print('wrote', out_p, round(D, 2), 's')
