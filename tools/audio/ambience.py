# The café in the background (Resources/Audio/CafeAmbience.ogg): people talking at the other tables (a murmur:
# voices made from glottal pulses through vowel formants, too far away to make out words), spoons stirring tea
# glasses, cups set down, the clack of a backgammon game somewhere. All synthesised, about 60 s, loops.
#   python3 tools/audio/ambience.py out.wav   (then: ffmpeg -i out.wav -c:a libvorbis -q:a 3 CafeAmbience.ogg)
import sys, wave
import numpy as np
from scipy.signal import butter, lfilter, sosfilt

SR = 32000
LEN = SR * 60
rng = np.random.default_rng(11)
out = np.zeros(LEN)

def bandpass(x, f, q):
    lo, hi = max(50, f / (1 + 0.5 / q)), min(SR / 2 - 100, f * (1 + 0.5 / q))
    return sosfilt(butter(2, [lo, hi], btype='band', fs=SR, output='sos'), x)

VOWELS = [(730, 1090, 2440), (530, 1840, 2480), (270, 2290, 3010), (570, 840, 2410), (300, 870, 2240), (660, 1720, 2410)]

def voice(dur, f0, gain):
    """One person talking: syllables of a pulse train through changing vowel formants, with pauses."""
    n = int(dur * SR); sig = np.zeros(n); t = 0
    while t < n:
        if rng.random() < 0.18: t += int(rng.uniform(0.3, 1.2) * SR); continue   # a pause
        syl = int(rng.uniform(0.09, 0.26) * SR)
        if t + syl >= n: break
        pitch = f0 * (1 + 0.12 * rng.standard_normal()) * np.linspace(1.05, 0.95, syl)
        phase = np.cumsum(pitch / SR)
        src = (phase % 1.0) - 0.5 + 0.15 * rng.standard_normal(syl)       # a buzz with breath
        v = VOWELS[rng.integers(len(VOWELS))]
        s = sum(bandpass(src, f, 6 + 4 * k) / (1 + k) for k, f in enumerate(v))
        env = np.sin(np.linspace(0, np.pi, syl)) ** 0.7
        sig[t:t + syl] += s * env
        t += syl + int(rng.uniform(0.0, 0.07) * SR)
    return sig * gain

# a dozen people at the other tables, men and women, near and far
for i in range(12):
    f0 = rng.choice([105, 120, 135, 190, 215])
    v = voice(60, f0, rng.uniform(0.25, 0.6))
    out += np.roll(v, rng.integers(LEN))
# far away: soften the top, then a room around it
out = sosfilt(butter(4, 1800, btype='low', fs=SR, output='sos'), out)
out = sosfilt(butter(2, 140, btype='high', fs=SR, output='sos'), out)
ir_len = int(0.9 * SR)
ir = rng.standard_normal(ir_len) * np.exp(-np.arange(ir_len) / (0.22 * SR))
ir = sosfilt(butter(2, 2500, btype='low', fs=SR, output='sos'), ir)
wet = np.fft.irfft(np.fft.rfft(out, LEN + ir_len) * np.fft.rfft(ir, LEN + ir_len))[:LEN]
wet[:ir_len] += np.fft.irfft(np.fft.rfft(out, LEN + ir_len) * np.fft.rfft(ir, LEN + ir_len))[LEN:LEN + ir_len]   # wrap the tail round (loops)
out = out / (np.abs(out).max() + 1e-9) * 0.5 + wet / (np.abs(wet).max() + 1e-9) * 0.5

def add(at, snd, gain):
    i = int(at * SR) % LEN; n = len(snd)
    end = min(LEN, i + n); out[i:end] += snd[:end - i] * gain
    if i + n > LEN: out[:i + n - LEN] += snd[end - i:] * gain

def clink():   # a spoon against a tea glass: a few bright, inharmonic partials ringing out
    n = int(0.6 * SR); t = np.arange(n) / SR
    base = rng.uniform(2600, 3600)
    return sum(np.sin(2 * np.pi * base * r * t) * np.exp(-t * d) * a for r, d, a in [(1, 9, 1), (1.53, 14, 0.5), (2.71, 22, 0.3), (3.9, 30, 0.15)])

def cup():     # a cup or glass put down on a table
    n = int(0.25 * SR); t = np.arange(n) / SR
    return (np.sin(2 * np.pi * 180 * t) * np.exp(-t * 28) + rng.standard_normal(n) * np.exp(-t * 90) * 0.4)

def checkers():  # a backgammon move somewhere: two or three quick clacks
    n = int(0.5 * SR); s = np.zeros(n)
    for k in range(rng.integers(2, 4)):
        at = int(k * rng.uniform(0.08, 0.16) * SR); m = int(0.05 * SR); t = np.arange(m) / SR
        c = np.sin(2 * np.pi * 900 * t) * np.exp(-t * 120) + rng.standard_normal(m) * np.exp(-t * 300) * 0.5
        s[at:at + m] += c[:max(0, min(m, n - at))]
    return s

for _ in range(26): add(rng.uniform(0, 60), clink() + np.roll(clink(), int(0.13 * SR)) * 0.7, rng.uniform(0.05, 0.12))   # stirring: two clinks
for _ in range(14): add(rng.uniform(0, 60), cup(), rng.uniform(0.08, 0.16))
for _ in range(10): add(rng.uniform(0, 60), checkers(), rng.uniform(0.04, 0.08))

out = np.tanh(out / (np.abs(out).max() + 1e-9) * 1.4) * 0.7
w = wave.open(sys.argv[1] if len(sys.argv) > 1 else 'ambience.wav', 'wb')
w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((out * 32767).astype(np.int16).tobytes()); w.close()
