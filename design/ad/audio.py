# The advert's sound: the game's café music, the café murmur under the opening, and a hit for each beat
# (the tile slam, the dice, the knight, the pawns, the coins, the counter, the reveal, the logo).
#   python3 audio.py <music.ogg> <ambience.ogg> <out.wav>
import sys, subprocess, wave
import numpy as np
SR = 44100; D = 30.0; N = int(SR * D)
rng = np.random.default_rng(3)
def load(path):
    raw = subprocess.run(['ffmpeg', '-loglevel', 'error', '-i', path, '-f', 's16le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.int16).astype(np.float32) / 32768
music, amb = load(sys.argv[1]), load(sys.argv[2])
out = np.zeros(N, np.float32)
t = np.arange(N) / SR
# the music: quiet under the night scene, full from the regulars on, a little lift for the reveal, out at the end
m = np.resize(music, N) if len(music) < N else music[:N]
env = np.interp(t, [0, 3.6, 4.2, 18.8, 19.4, 28.5, 30], [0.25, 0.3, 0.85, 0.85, 1.0, 1.0, 0.0])
out += m * env * 0.8
a = np.resize(amb, N)
out += a * np.interp(t, [0, 0.5, 3.8, 4.4, 30], [0, 0.55, 0.55, 0.0, 0.0])
def add(at, snd, g=1.0):
    i = int(at * SR); n = min(len(snd), N - i)
    if n > 0: out[i:i + n] += snd[:n] * g
def tt(d): return np.arange(int(d * SR)) / SR
def whoosh(d=0.5, up=True):
    x = tt(d); n = rng.standard_normal(len(x))
    # a sweep: noise through a moving one-pole low-pass
    f = np.linspace(400, 6000, len(x)) if up else np.linspace(6000, 400, len(x))
    y = np.zeros(len(x)); acc = 0
    for k in range(len(x)):
        a_ = np.exp(-2 * np.pi * f[k] / SR); acc = (1 - a_) * n[k] + a_ * acc; y[k] = acc
    return y * np.sin(np.pi * x / d) ** 2 * 1.6
def slam():
    x = tt(0.9); return (rng.standard_normal(len(x)) * np.exp(-x * 50) * 0.9 + np.sin(2 * np.pi * (110 - 40 * x) * x) * np.exp(-x * 7) * 1.1 + np.sin(2 * np.pi * 240 * x) * np.exp(-x * 16) * 0.5)
def click(f=2400):
    x = tt(0.06); return np.sin(2 * np.pi * f * x) * np.exp(-x * 220) + rng.standard_normal(len(x)) * np.exp(-x * 500) * 0.4
def rattle(d=0.45):
    y = np.zeros(int(d * SR))
    for k in range(10): c = click(2000 + k * 160); i = int(k * 0.042 * SR); y[i:i + len(c)] += c[:len(y) - i] * 0.5
    return y
def thud(f=150):
    x = tt(0.4); return np.sin(2 * np.pi * f * x) * np.exp(-x * 14) + rng.standard_normal(len(x)) * np.exp(-x * 80) * 0.3
def pop(f=900):
    x = tt(0.18); return np.sin(2 * np.pi * (f + 900 * np.exp(-x * 30)) * x) * np.exp(-x * 25) * 0.8
def coin(f=3200):
    x = tt(0.35); return sum(np.sin(2 * np.pi * f * r * x) * np.exp(-x * d) * g for r, d, g in [(1, 12, 1), (1.5, 18, .5), (2.7, 26, .3)]) * 0.5
def chime():
    x = tt(1.6); return sum(np.sin(2 * np.pi * f * x) * np.exp(-x * 2.5) * g for f, g in [(784, .6), (988, .5), (1175, .45), (1568, .3)])
def boom():
    x = tt(1.2); return np.sin(2 * np.pi * (70 - 20 * x) * x) * np.exp(-x * 3.5) * 1.2 + rng.standard_normal(len(x)) * np.exp(-x * 30) * 0.4
add(4.0, whoosh(0.6), 0.5)
for i in range(3): add(4.5 + i * 0.3, pop(700 + i * 150), 0.5)
for i in range(3): add(5.35 + i * 0.35, pop(1300), 0.35)
add(6.85, pop(1000), 0.6)
add(8.55, whoosh(0.45), 0.6); add(9.0, slam(), 1.0); add(9.0, boom(), 0.6)
add(10.0, whoosh(0.4), 0.4); add(10.1, rattle(), 0.8); add(10.6, thud(320), 0.7); add(10.62, click(1800), 0.6); add(10.6, boom(), 0.4)
add(11.5, whoosh(0.4), 0.4); add(12.15, thud(170), 1.0); add(12.15, boom(), 0.5)
add(13.0, whoosh(0.4), 0.4)
for i in range(6): add(13.15 + i * 0.09, click(1200 + i * 90), 0.4)
add(13.7, pop(1400), 0.6); add(13.7, boom(), 0.4)
for i in range(26): add(14.6 + rng.uniform(0, 1.6), coin(rng.uniform(2600, 3800)), 0.28)
add(16.4, boom(), 0.5); add(16.45, chime(), 0.35)
for k in range(24): add(16.9 + 1.3 * (k / 24) ** 0.6, click(2600), 0.25)
add(18.2, coin(3000), 0.6); add(18.25, coin(3600), 0.5)
add(19.05, whoosh(0.7), 0.6); add(19.9, thud(200), 0.6)
for i in range(4): add(21 + i * 0.9, pop(1100 + i * 100), 0.45)
for i in range(3): add(22.2 + i * 0.35, pop(800), 0.35)
add(25.4, whoosh(0.6), 0.55); add(25.9, thud(140), 0.7); add(26.35, slam(), 0.35); add(26.6, chime(), 0.5); add(27.0, pop(900), 0.5)
out = np.tanh(out * 0.9) * 0.92
w = wave.open(sys.argv[3], 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((out * 32767).astype(np.int16).tobytes()); w.close()
