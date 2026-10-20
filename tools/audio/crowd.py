# The café's crowd: dozens of people talking at once in a coffeehouse (the Arabic "walla" of a crowd),
# laughter, tea glasses and spoons, dominoes slapped down, dice, chairs, a waiter calling an order,
# all in a room with some echo; made as a seamless loop for the game's opening and home screens.
#
# Voices: text-to-speech models free for any use (Piper's CC0 Persian and Turkish voices, Kokoro under
# Apache 2.0), speaking Egyptian coffeehouse phrases written as sounds. In a crowd no single sentence is
# heard, only the sound of people talking.
#   TTS=/path/to/models python3 tools/audio/crowd.py out.wav
import json, os, sys, glob, wave, numpy as np, onnxruntime as ort

TTS = os.environ.get('TTS', '/tmp/claude-0/tts')
OUT = sys.argv[1] if len(sys.argv) > 1 else 'crowd.wav'
SR = 44100
rng = np.random.default_rng(7)

# coffeehouse talk, written as sounds (IPA)
PHRASES = [
    'ʔahlan ja ħaːg', 'ʃaːj bisukkar ʃwajja', 'jalla ja ʕamm ʔilʕab', 'duːmiːnu ja baːʃa', 'ʔeːh daː ja raːgil',
    'wallaːhi ʕaːli', 'ħutt ʔittawla hina', 'maːʃi maːʃi', 'ʔinta kasabt ʔilmarra di', 'xalaːs ja saːħbi',
    'ʔahwa saːda law samaħt', 'ʃiːʃa ja walad', 'ʔilmarra ʔilli gajja', 'sitta w xamsa', 'duʃ ja baːʃa',
    'ʔana ʔilli halʕab', 'ʔistanna ʃwajja', 'ʔinta bitʃajjil leːh', 'la la la mʃ kida', 'ʔitfaddal ʔuʔʕud',
    'ʃuft ʔilmatʃ ʔimbaːriħ', 'ʔilʔahli kasab', 'ʔizzamaːlik baʔa', 'kunt fein ja ragil', 'saba:ħ ʔilxeːr',
    'misaːʔ ʔilfull', 'ʃukran ja ħabiːbi', 'ʔeːh ʔilʔaxbaːr', 'ʔirmi ʔizzahr', 'jaː salaːm', 'ma ʕaleːʃ',
    'ʔinʃaːʔallah', 'ʔana ʔilli ʕaleːja ʔiddoːr', 'ʔitnein ʃaːj hina', 'waːħid ʔahwa maẓbuːtˤ', 'ħaːdir ja beːh',
]
LAUGHS = ['hahaha haha', 'haha', 'hehehe', 'ha ha ha ha']
CALLS = ['waːħid ʃaːj', 'ʔitnein ʔahwa', 'ħaːdir', 'ʃiːʃa tufaːħ']

# ---------- the voices ----------
class Piper:
    def __init__(self, d):
        onnx = glob.glob(os.path.join(TTS, d, '*.onnx'))[0]
        cfg = json.load(open(onnx + '.json'))
        self.idmap = cfg['phoneme_id_map']; self.sr = cfg['audio']['sample_rate']
        self.sess = ort.InferenceSession(onnx, providers=['CPUExecutionProvider'])
    def say(self, text, speed=1.0):
        ids = self.idmap['^'] + self.idmap['_']
        for ch in text:
            if ch in self.idmap: ids += self.idmap[ch] + self.idmap['_']
        ids += self.idmap['$']
        x = np.array([ids], dtype=np.int64)
        a = self.sess.run(None, {'input': x, 'input_lengths': np.array([x.shape[1]], dtype=np.int64),
                                 'scales': np.array([0.75, 1 / speed, 0.9], dtype=np.float32)})[0].squeeze()
        return resample(a.astype(np.float32), self.sr, SR)

class Kokoro:
    def __init__(self):
        d = os.path.join(TTS, 'kokoro-multi-lang-v1_0')
        self.sess = ort.InferenceSession(os.path.join(d, 'model.onnx'), providers=['CPUExecutionProvider'])
        self.names = self.sess.get_modelmeta().custom_metadata_map['speaker_names'].split(',')
        self.V = np.fromfile(os.path.join(d, 'voices.bin'), dtype=np.float32).reshape(len(self.names), 510, 256)
        self.tok = {}
        for line in open(os.path.join(d, 'tokens.txt'), encoding='utf8'):
            p = line.rstrip('\n').rsplit(' ', 1)
            if len(p) == 2: self.tok[p[0]] = int(p[1])
    # sounds Kokoro doesn't have, swapped for the nearest it does
    SWAP = {'ħ': 'h', 'ʕ': 'ʔ', 'ẓ': 'z', 'ˤ': '', 'g': 'ɡ'}
    def say(self, text, voice, speed=1.0):
        ids = [self.tok[c] for c in ''.join(self.SWAP.get(c, c) for c in text) if c in self.tok]
        x = np.array([[0] + ids + [0]], np.int64)
        style = self.V[self.names.index(voice), min(len(ids), 509)][None]
        a = self.sess.run(None, {'tokens': x, 'style': style, 'speed': np.array([speed], np.float32)})[0].squeeze()
        return resample(a.astype(np.float32), 24000, SR)

def resample(a, sr0, sr1):
    if sr0 == sr1: return a
    n = int(len(a) * sr1 / sr0)
    return np.interp(np.linspace(0, len(a) - 1, n), np.arange(len(a)), a).astype(np.float32)

def pitch(a, k):
    """Higher or lower voice (and a little faster or slower), by resampling."""
    n = int(len(a) / k)
    return np.interp(np.linspace(0, len(a) - 1, n), np.arange(len(a)), a).astype(np.float32)

def lowpass(a, cutoff):
    # one-pole, twice: a soft roll-off for distant voices
    from scipy.signal import lfilter
    k = np.exp(-2 * np.pi * cutoff / SR)
    for _ in range(2): a = lfilter([1 - k], [1, -k], a).astype(np.float32)
    return a

def norm(a, peak=0.9):
    m = np.abs(a).max()
    return a / m * peak if m > 0 else a

# ---------- making the clips ----------
print('voices…', flush=True)
pipers = {name: Piper(d) for name, d in [('amir', 'vits-piper-fa_IR-amir-medium'), ('ganji', 'vits-piper-fa_IR-ganji-medium'), ('fahrettin', 'vits-piper-tr_TR-fahrettin-medium')]}
kok = Kokoro()
MALE_K = ['am_adam', 'am_echo', 'am_eric', 'am_michael', 'am_onyx', 'bm_george', 'em_alex', 'hm_omega', 'im_nicola', 'pm_alex', 'am_liam', 'bm_lewis']
FEMALE_K = ['af_bella', 'af_sarah', 'ef_dora', 'if_sara', 'hf_alpha', 'pf_dora', 'af_nicole', 'bf_emma']

def speaker_clip(sp, text):
    kind, name, k, speed = sp
    a = pipers[name].say(text, speed) if kind == 'piper' else kok.say(text, name, speed)
    return pitch(norm(a), k)

# the people in the room: voice, pitch, pace, where they sit (pan, distance)
people = []
for i in range(22):
    r = rng.random()
    if r < 0.45: sp = ('piper', rng.choice(list(pipers)), rng.uniform(0.9, 1.12), rng.uniform(0.95, 1.2))
    elif r < 0.78: sp = ('kokoro', str(rng.choice(MALE_K)), rng.uniform(0.88, 1.08), rng.uniform(0.95, 1.25))
    else: sp = ('kokoro', str(rng.choice(FEMALE_K)), rng.uniform(0.95, 1.1), rng.uniform(1.0, 1.25))
    people.append({'sp': sp, 'pan': rng.uniform(-0.9, 0.9), 'dist': rng.uniform(0.25, 1.0)})

DUR = 70.0   # seconds of loop
N = int(DUR * SR)
mix = np.zeros((2, N + SR * 8), np.float32)

def place(clip, t, pan, gain, dist):
    clip = lowpass(clip, 7000 - 5000 * dist) * gain
    i = int(t * SR)
    if i >= mix.shape[1]: return
    clip = clip[: mix.shape[1] - i]
    l, r = np.sqrt((1 - pan) / 2), np.sqrt((1 + pan) / 2)
    mix[0, i:i + len(clip)] += clip * l
    mix[1, i:i + len(clip)] += clip * r

print('talking…', flush=True)
for n, p in enumerate(people):
    t = rng.uniform(0, 3)
    while t < DUR + 4:
        text = str(rng.choice(PHRASES))
        if rng.random() < 0.3: text += ' ' + str(rng.choice(PHRASES))
        clip = speaker_clip(p['sp'], text)
        gain = (0.85 - 0.6 * p['dist']) * rng.uniform(0.7, 1.1)
        place(clip, t, p['pan'], gain, p['dist'])
        t += len(clip) / SR + rng.uniform(0.25, 2.8)
        if rng.random() < 0.08:   # a laugh
            lc = speaker_clip(p['sp'], str(rng.choice(LAUGHS)))
            place(lc, t, p['pan'], gain * 0.9, p['dist']); t += len(lc) / SR + rng.uniform(0.3, 1.0)
    print(f'  {n + 1}/{len(people)}', flush=True)

# the waiter calling orders, now and then, from the counter
for t in np.arange(rng.uniform(4, 9), DUR, rng.uniform(12, 18)):
    place(speaker_clip(('piper', 'amir', 1.0, 1.05), str(rng.choice(CALLS))), t, -0.6, 0.55, 0.4)

# ---------- the sounds of the place ----------
def env(n, decay): return np.exp(-np.arange(n) / SR * decay).astype(np.float32)
def clink():   # a tea glass touched by a spoon
    n = int(0.6 * SR); t = np.arange(n) / SR; f0 = rng.uniform(2300, 3600)
    s = sum(np.sin(2 * np.pi * f0 * m * t) * a for m, a in [(1, 1), (2.71, 0.5), (5.2, 0.25)])
    return (s * env(n, rng.uniform(9, 14)) * 0.35).astype(np.float32)
def stir():    # a spoon going round a glass
    out = np.zeros(int(1.4 * SR), np.float32)
    for k in range(int(rng.integers(4, 8))):
        c = clink() * 0.45; i = int(k * rng.uniform(0.15, 0.2) * SR)
        out[i:i + len(c)] += c[: len(out) - i]
    return out
def slap():    # a domino slapped down on a wooden table
    n = int(0.25 * SR); noise = rng.standard_normal(n).astype(np.float32)
    t = np.arange(n) / SR
    return ((noise * env(n, 60) * 0.6 + np.sin(2 * np.pi * 180 * t) * env(n, 30) * 0.8)).astype(np.float32)
def dice():    # two dice rolled on a backgammon board
    out = np.zeros(int(0.7 * SR), np.float32)
    for k in range(int(rng.integers(5, 9))):
        n = int(0.06 * SR); i = int(k * rng.uniform(0.05, 0.09) * SR)
        c = rng.standard_normal(n).astype(np.float32) * env(n, 90) * (0.6 - k * 0.05)
        out[i:i + n] += c[: len(out) - i]
    return out
def chair():   # a chair dragged on tiles
    n = int(rng.uniform(0.4, 0.8) * SR); noise = rng.standard_normal(n).astype(np.float32)
    shape = np.sin(np.linspace(0, np.pi, n)) ** 0.5
    return (lowpass(noise, rng.uniform(500, 1200)) * shape * 0.5).astype(np.float32)

print('the café…', flush=True)
for maker, every, gain in [(clink, 3.5, 0.16), (stir, 16, 0.14), (slap, 5.0, 0.22), (dice, 12, 0.16), (chair, 22, 0.14)]:
    t = rng.uniform(0, every)
    while t < DUR + 4:
        d = rng.uniform(0.3, 1.0)
        place(maker(), t, rng.uniform(-0.9, 0.9), gain * (1 - 0.5 * d), d)
        t += rng.exponential(every)

# room tone: a low murmur under everything
bed = lowpass(rng.standard_normal(mix.shape[1]).astype(np.float32), 300) * 0.05
mix += bed[None, :]

# ---------- the room ----------
print('echo…', flush=True)
from scipy.signal import fftconvolve
irn = int(1.1 * SR)
ir = rng.standard_normal((2, irn)).astype(np.float32) * env(irn, 5.5)[None, :]
ir[:, 0] = 1.0
for c in range(2):
    ir[c] = lowpass(ir[c], 3200)
wet = np.stack([fftconvolve(mix[c], ir[c])[: mix.shape[1]] for c in range(2)])
out = mix * 0.72 + wet / np.abs(wet).max() * np.abs(mix).max() * 0.28

# a seamless loop: the tail fades into the beginning
fade = int(4 * SR)
loop = out[:, :N].copy()
ramp = np.linspace(0, 1, fade, dtype=np.float32)[None, :]
loop[:, :fade] = loop[:, :fade] * ramp + out[:, N:N + fade] * (1 - ramp)
loop = loop / np.sqrt((loop ** 2).mean()) * 0.07   # quiet and steady (about −23 dB RMS)
loop = np.clip(loop, -0.98, 0.98)

with wave.open(OUT, 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((loop.T * 32767).astype(np.int16).tobytes())
print('wrote', OUT, f'{loop.shape[1] / SR:.1f}s')
