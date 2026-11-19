# The street outside the café door (Resources/Audio/CafeStreet.ogg): the low hum of Cairo traffic, a car or a
# motorbike passing now and then, a short horn far off, footsteps going by. All synthesised, about 50 s, loops.
#   python3 tools/audio/street.py out.wav   (then: ffmpeg -i out.wav -c:a libvorbis -q:a 2 CafeStreet.ogg)
import sys, wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 32000
LEN = SR * 50
rng = np.random.default_rng(23)
out = np.zeros(LEN)

def lp(x, f): return sosfilt(butter(2, f, btype='low', fs=SR, output='sos'), x)
def bp(x, lo, hi): return sosfilt(butter(2, [lo, hi], btype='band', fs=SR, output='sos'), x)

# the hum: brown noise, low and slowly swelling
hum = np.cumsum(rng.standard_normal(LEN)); hum -= lp(hum, 0.5); hum = lp(hum, 260)
hum /= np.abs(hum).max(); swell = 0.75 + 0.25 * np.sin(np.linspace(0, 2 * np.pi * 3, LEN))
out += hum * swell * 0.35

def place(sig, at, gain):
    n = min(len(sig), LEN - at)
    if n > 0: out[at:at + n] += sig[:n] * gain

def passby(dur, f0, rough):
    """An engine going past: a buzzing tone rising then falling (Doppler), loudest in the middle."""
    n = int(dur * SR); t = np.arange(n) / SR
    pitch = f0 * (1.08 - 0.16 * t / dur)
    ph = np.cumsum(pitch / SR)
    s = np.sign(np.sin(2 * np.pi * ph)) * 0.5 + np.sin(2 * np.pi * ph * 2) * 0.3 + rough * rng.standard_normal(n)
    s = lp(s, 900)
    env = np.exp(-((t - dur / 2) / (dur / 4.5)) ** 2)
    return s * env

def horn(dur, f):
    n = int(dur * SR); t = np.arange(n) / SR
    s = np.sin(2 * np.pi * f * t) + 0.6 * np.sin(2 * np.pi * f * 1.26 * t) + 0.3 * np.sin(2 * np.pi * f * 2 * t)
    env = np.minimum(1, t / 0.02) * np.minimum(1, (dur - t) / 0.05)
    return bp(s * env, 300, 2500)

def steps(count):
    s = np.zeros(int(count * 0.55 * SR) + SR)
    for k in range(count):
        n = int(0.06 * SR); click = rng.standard_normal(n) * np.exp(-np.arange(n) / (0.012 * SR))
        a = int(k * 0.55 * SR + rng.uniform(-0.03, 0.03) * SR); s[a:a + n] += bp(click, 200, 1800)
    return s * np.linspace(0.3, 1, len(s)) * np.linspace(1, 0.3, len(s))

t = 1.0
while t < 47:
    kind = rng.random()
    at = int(t * SR)
    if kind < 0.45: place(passby(rng.uniform(3, 5), rng.uniform(45, 70), 0.25), at, 0.28)
    elif kind < 0.7: place(passby(rng.uniform(2, 3), rng.uniform(110, 150), 0.4), at, 0.2)   # a motorbike
    elif kind < 0.85:
        for k in range(rng.integers(1, 3)): place(horn(rng.uniform(0.15, 0.35), rng.uniform(380, 460)), at + int(k * 0.32 * SR), 0.08)
    else: place(steps(rng.integers(5, 9)), at, 0.12)
    t += rng.uniform(2.0, 4.5)

# far away and through the doorway: dull the top, then fade the loop's ends into each other
out = lp(out, 2200)
fade = SR * 2
out[:fade] = out[:fade] * np.linspace(0, 1, fade) + out[-fade:] * np.linspace(1, 0, fade)
out = out[:-fade]
out /= np.abs(out).max() * 1.12
w = wave.open(sys.argv[1], 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((out * 32767).astype(np.int16).tobytes()); w.close()
print('street', len(out) / SR, 's')
