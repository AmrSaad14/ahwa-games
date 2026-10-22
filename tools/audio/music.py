# Light café music for the game (Resources/Audio/CafeMusic.ogg): an oud and a qanun playing a slow
# melody in maqam Hijaz over a soft frame drum (maqsum rhythm). Everything is synthesised here
# (plucked strings with Karplus-Strong), so there are no rights questions. Loops seamlessly.
#   python3 tools/audio/music.py out.wav
import sys, math, random
import numpy as np

SR = 44100
BPM = 84
BEAT = 60 / BPM
BARS = 24                      # 24 bars of 4 beats: about 68 s
LEN = int(SR * BEAT * 4 * BARS)
random.seed(7); np.random.seed(7)

def ks(freq, dur, bright=0.5, decay=0.996):
    """A plucked string (Karplus-Strong), bright 0..1."""
    n = int(SR * dur); p = max(2, int(SR / freq))
    buf = np.random.uniform(-1, 1, p)
    # a softer pluck for the oud: low-pass the initial noise
    for _ in range(int((1 - bright) * 4)): buf = 0.5 * (buf + np.roll(buf, 1))
    out = np.zeros(n); b = buf.copy(); i = 0
    for k in range(n):
        out[k] = b[i]
        nxt = (i + 1) % p
        b[i] = decay * 0.5 * (b[i] + b[nxt])
        i = nxt
    env = np.minimum(1, np.arange(n) / (0.004 * SR))
    return out * env

# maqam Hijaz on D: D Eb F# G A Bb C D (with the usual flat-ish E-flat)
D = 146.83
STEPS = [0, 1.1, 4, 5, 7, 8, 10, 12, 13.1, 16, 17]   # semitones above D
def note(deg, octave=0):
    o, d = divmod(deg, 7)
    return D * 2 ** ((STEPS[d] + 12 * (o + octave)) / 12)

mix = np.zeros(LEN + SR * 3)
def put(sig, t, gain):
    i = int(t * SR); mix[i:i + len(sig)] += sig[: len(mix) - i] * gain

# the melody: phrases of scale degrees and lengths (in beats), answered and varied
phrases = [
    [(4, 1), (5, 0.5), (4, 0.5), (3, 1), (2, 1)],
    [(3, 0.5), (2, 0.5), (1, 1), (2, 1), (0, 1)],
    [(0, 0.5), (1, 0.5), (2, 1), (3, 0.5), (4, 0.5), (5, 1)],
    [(4, 1), (3, 0.5), (2, 0.5), (1, 1), (0, 1)],
    [(7, 1), (6, 0.5), (5, 0.5), (4, 1), (5, 1)],
    [(4, 0.5), (3, 0.5), (2, 0.5), (1, 0.5), (0, 2)],
]
order = [0, 1, 2, 3, 0, 1, 4, 5, 2, 3, 4, 5]
t = 0.0
for bar_pair, pi in enumerate(order):
    ph = phrases[pi]
    start = bar_pair * 8 * BEAT          # each phrase gets two bars
    t = start
    for deg, beats in ph:
        dur = beats * BEAT
        # the oud plays the phrase; a little tremolo (risha) on the long notes
        if beats >= 1 and random.random() < 0.5:
            k = int(beats * 4)
            for j in range(k): put(ks(note(deg), dur / k + 0.6, 0.35, 0.995), t + j * dur / k, 0.16)
        else:
            put(ks(note(deg), dur + 1.2, 0.35, 0.9965), t, 0.24)
        t += dur
    # the qanun answers the phrase an octave up, softly, in the second bar
    t2 = start + 4 * BEAT
    for deg, beats in ph:
        dur = beats * BEAT * 0.9
        put(ks(note(deg, 1), dur + 0.9, 0.9, 0.9955), t2, 0.11)
        put(ks(note(deg, 1) * 1.003, dur + 0.9, 0.9, 0.9955), t2 + 0.012, 0.06)   # the doubled courses
        t2 += beats * BEAT * 0.9
# a low drone on D under it all
drone_t = np.arange(len(mix)) / SR
mix += 0.03 * np.sin(2 * math.pi * D / 2 * drone_t) * (0.8 + 0.2 * np.sin(2 * math.pi * 0.1 * drone_t))

# the frame drum: maqsum (dum tek - tek dum - tek -)
def dum():
    n = int(0.35 * SR); tt = np.arange(n) / SR
    return np.sin(2 * math.pi * (70 + 40 * np.exp(-tt * 30)) * tt) * np.exp(-tt * 9)
def tek():
    n = int(0.12 * SR); tt = np.arange(n) / SR
    noise = np.convolve(np.random.uniform(-1, 1, n), np.ones(6) / 6, 'same')   # a skin, not a hiss
    return noise * np.exp(-tt * 55) * 0.5 + np.sin(2 * math.pi * 520 * tt) * np.exp(-tt * 40) * 0.35
pattern = [(0, dum, 0.16), (0.5, tek, 0.05), (1.5, tek, 0.05), (2, dum, 0.14), (3, tek, 0.06)]
for bar in range(BARS):
    for off, f, g in pattern:
        put(f(), (bar * 4 + off) * BEAT, g)

# loop it: fold the tail back onto the start
tail = mix[LEN:LEN + SR * 3]
mix[:len(tail)] += tail
out = mix[:LEN]
# a touch of room: a few short echoes
for d, g in [(0.031, 0.25), (0.067, 0.18), (0.113, 0.12), (0.19, 0.07)]:
    k = int(d * SR); out[k:] += out[:-k] * g
out = out / np.percentile(np.abs(out), 99.7) * 0.55
out = np.tanh(out * 1.2) / np.tanh(1.2) * 0.7   # gently even out the peaks
import wave
w = wave.open(sys.argv[1], 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((out * 32767).astype(np.int16).tobytes()); w.close()
print('ok', len(out) / SR, 's')
