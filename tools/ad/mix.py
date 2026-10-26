# The premium advert's sound: Am Fathi's voice on top, the game's café music under it (dipping while anyone talks),
# the café murmur, a soft whoosh on each cut and a warm hit when the crest lands on the end card.
#   python3 mix.py <voice dir> <music.ogg> <ambience.ogg> <duration s> <out.wav>
import sys, json, os, subprocess, wave
import numpy as np

SR = 48000
vdir, music_p, amb_p, D, out_p = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4]), sys.argv[5]
N = int(SR * D)
rng = np.random.default_rng(7)

def load(path):
    raw = subprocess.run(['ffmpeg', '-loglevel', 'error', '-i', path, '-f', 's16le', '-ac', '1', '-ar', str(SR), '-'],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.int16).astype(np.float32) / 32768

def fit(x): return np.resize(x, N) if len(x) < N else x[:N]

tl = json.load(open(os.path.join(vdir, 'timeline.json')))
L = {l['id']: l for l in tl['lines']}
voice = np.zeros(N, np.float32)
v = load(os.path.join(vdir, 'voice.wav'))[:N]
voice[:len(v)] = v
voice *= 0.9 / max(1e-4, np.percentile(np.abs(v), 99.9))     # the voice to a steady loudness

t = np.arange(N) / SR
# ducking: the music sits low while anyone talks and comes up in the gaps and at the end
talk = np.zeros(N, np.float32)
for l in tl['lines']:
    talk[int(l['start'] * SR):int(l['end'] * SR)] = 1
k = int(0.25 * SR); talk = np.convolve(talk, np.ones(k) / k, mode='same')
music = fit(load(music_p))
mus_env = np.interp(t, [0, 0.8, D - 2.2, D], [0, 1, 1, 0]) * (0.42 - 0.28 * talk)
end_card = L['l7']['end'] + 0.2 if 'l7' in L else D - 3
mus_env *= np.where(t > end_card - 0.5, 1.45, 1.0)            # up for the end card
amb = fit(load(amb_p)) * np.interp(t, [0, 0.6, D - 1, D], [0, 0.22, 0.22, 0])

fx = np.zeros(N, np.float32)
def add(at, snd, g=1.0):
    i = int(at * SR)
    if i >= N: return
    n = min(len(snd), N - i); fx[i:i + n] += snd[:n] * g
def tt(d): return np.arange(int(d * SR)) / SR
def whoosh(d=0.55):
    x = tt(d); noise = rng.standard_normal(len(x)).astype(np.float32)
    # band-passed noise sweeping up, in and out smoothly
    f = np.exp(np.linspace(np.log(300), np.log(2400), len(x)))
    ph = np.cumsum(2 * np.pi * f / SR); y = noise * (0.5 + 0.5 * np.sin(ph))
    y = np.convolve(y, np.ones(24) / 24, mode='same')
    return y * np.sin(np.pi * x / d) ** 2 * 0.5
def hit():
    x = tt(1.6); return (np.sin(2 * np.pi * 55 * x) * 0.8 + np.sin(2 * np.pi * 110 * x) * 0.3) * np.exp(-x * 3.2)
def chime():
    x = tt(2.2); return sum(np.sin(2 * np.pi * f * x) * a for f, a in [(784, .35), (1175, .25), (1568, .15)]) * np.exp(-x * 2.2)

cuts = [L[i]['start'] for i in ('l2', 'l3', 'l5') if i in L]
if 'l6' in L: cuts.append(L['l6']['start'] - 0.4)
if 'l7' in L: cuts.append(L['l7']['start'] - 0.2)
for c in cuts: add(c - 0.3, whoosh(), 0.35)
add(end_card + 0.05, hit(), 0.55); add(end_card + 0.6, chime(), 0.4)

mix = voice + music * mus_env + amb + fx
mix = np.tanh(mix * 0.95) * 0.95
# a little width: music and murmur slightly apart in the two channels, the voice in the middle
side = np.roll(music * mus_env + amb, int(0.012 * SR)) - (music * mus_env + amb)
left, right = mix + side * 0.18, mix - side * 0.18
st = np.stack([left, right], 1).clip(-1, 1)
w = wave.open(out_p, 'wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((st * 32767).astype(np.int16).tobytes()); w.close()
print('wrote', out_p, round(D, 2), 's')
