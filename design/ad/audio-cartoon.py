# The cartoon advert's sound: the dialogue, the game's café music under it (lower while someone talks),
# the café murmur at the start, and the hits: the tile slam, whooshes between scenes, pops, coins, the end sting.
#   python3 audio-cartoon.py <voice dir> <music.ogg> <ambience.ogg> <out.wav>
import sys, json, subprocess, wave
import numpy as np
SR = 44100
vdir, music_f, amb_f, out_f = sys.argv[1:5]
TL = json.load(open(vdir + '/timeline.json', encoding='utf8'))
S = {l['id']: l['start'] for l in TL['lines']}; E = {l['id']: l['end'] for l in TL['lines']}
D = TL['duration'] + 0.4; N = int(D * SR)
rng = np.random.default_rng(7)
def load(path):
    raw = subprocess.run(['ffmpeg', '-loglevel', 'error', '-i', path, '-f', 's16le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.int16).astype(np.float32) / 32768
voice = load(vdir + '/voice.wav'); voice = np.pad(voice, (0, max(0, N - len(voice))))[:N]
music, amb = load(music_f), load(amb_f)
t = np.arange(N) / SR
out = np.zeros(N, np.float32)
# music: ducked under speech (smoothed), up for the end card
speech = np.convolve((np.abs(voice) > 0.02).astype(np.float32), np.ones(int(SR * .25)) / int(SR * .25), mode='same')
duck = 1 - 0.55 * np.clip(speech * 3, 0, 1)
m = np.resize(music, N)
menv = np.interp(t, [0, 0.6, S['z4'] - 0.3, S['z4'], D - 1.0, D], [0, 0.42, 0.42, 0.6, 0.6, 0])
out += m * menv * duck
out += np.resize(amb, N) * np.interp(t, [0, .4, S['z2'] - .4, S['z2'], D], [0, .5, .5, 0, 0])
def add(at, snd, g=1.0):
    i = int(at * SR); n = min(len(snd), N - i)
    if n > 0 and i >= 0: out[i:i + n] += snd[:n] * g
def tt(d): return np.arange(int(d * SR)) / SR
def lowpass(x, f):
    a = np.exp(-2 * np.pi * f / SR); y = np.zeros_like(x); acc = 0.0
    for k in range(len(x)): acc = (1 - a) * x[k] + a * acc; y[k] = acc
    return y
def slam():
    x = tt(.45); body = np.sin(2 * np.pi * (90 - 50 * x) * x) * np.exp(-x * 14)
    click = lowpass(rng.standard_normal(len(x)), 2500) * np.exp(-x * 60) * 3
    return (body + click) * .9
def whoosh(d=.32):
    x = tt(d); n = rng.standard_normal(len(x)); f = np.linspace(500, 5000, len(x))
    y = np.zeros(len(x)); acc = 0
    for k in range(len(x)):
        a = np.exp(-2 * np.pi * f[k] / SR); acc = (1 - a) * n[k] + a * acc; y[k] = acc
    return y * np.sin(np.pi * x / d) * 1.1
def pop(f=700):
    x = tt(.12); return np.sin(2 * np.pi * (f + 900 * x) * x) * np.exp(-x * 40) * .5
def coin(f=1900):
    x = tt(.35); return (np.sin(2 * np.pi * f * x) + .6 * np.sin(2 * np.pi * f * 1.5 * x)) * np.exp(-x * 11) * .22
def chime():
    x = tt(1.2); s = np.zeros(len(x))
    for i, f in enumerate([784, 988, 1175, 1568]):
        y = np.sin(2 * np.pi * f * x) * np.exp(-np.maximum(x - i * .09, 0) * 4) * (x >= i * .09); s += y
    return s * .18
# the hits
add(S['f1'] + .05, slam(), 1.0)
z = S['z2']
for at in [S['z2'] - .15, S['n1'] - .25, S['z3'] - .15, S['f4'] - .15, S['z4'] - .1]: add(at - .05, whoosh(), .5)
for at in [z + 1.19, z + 1.94, z + 2.74, z + 3.57]: add(at, pop(650), .8)
for i in range(6): add(z + 4.4 + i * .14, pop(900 + i * 60), .5)
add(S['n1'] - .15, pop(500), .7)
win = S['f3'] + 1.26
add(win, chime(), 1.0)
for i in range(12): add(win + .05 + i * .07, coin(1700 + (i % 4) * 160), .8)
got = S['z3'] + 1.57
add(got, chime(), .8)
for i in range(8): add(got + .05 + i * .09, coin(1800 + (i % 3) * 200), .7)
for i in range(10): add(S['f4'] + i * .22, coin(1600 + (i % 5) * 140), .4)
end = S['z4'] - .1
for i, at in enumerate([end + .5, end + .62, end + .74, end + .86]): add(at, pop(800 + i * 120), .6)
add(end + .1, chime(), .9)
# the voice on top, then a gentle limiter
out += voice * 1.0
peak = np.max(np.abs(out)); out = np.tanh(out / max(peak, 1e-6) * 1.6) * .92 if peak > .95 else out
w = wave.open(out_f, 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((np.clip(out, -1, 1) * 32767).astype(np.int16).tobytes()); w.close()
print('wrote', out_f, round(D, 2), 's')
