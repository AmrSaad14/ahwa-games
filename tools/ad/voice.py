# The advert's voice: Am Fathi (and one line from Hajj Mahmoud) in Egyptian Arabic, from lines.json.
# With AZURE_SPEECH_KEY and AZURE_SPEECH_REGION set, it uses Azure's licensed neural voice ar-EG-ShakirNeural
# (fine for ads). Without them it makes a DRAFT with the same voice through edge-tts, for review only.
# Writes: voice.wav (all lines with their pauses), timeline.json (when each line starts and ends) and
# mouth.json (how open the mouth is, 30 times a second, for the lip sync).
#   python3 voice.py <out dir> [lines.json]
# lines.json: a list of lines (one voice, STYLE below), or {"voices": {who: {voice, rate, pitch}}, "lines": [...]};
# a line may wait "before" seconds and leave "after" seconds of quiet.
import json, os, subprocess, sys, wave, asyncio, urllib.request
import numpy as np

here = os.path.dirname(os.path.abspath(__file__))
out = sys.argv[1]; os.makedirs(out, exist_ok=True)
spec = json.load(open(sys.argv[2] if len(sys.argv) > 2 else os.path.join(here, 'lines.json'), encoding='utf8'))
VOICE = 'ar-EG-ShakirNeural'
STYLE = {'fathi': {'rate': '+6%', 'pitch': '+0Hz'}, 'hajj': {'rate': '-6%', 'pitch': '-18Hz'}}
if isinstance(spec, dict): STYLE = spec['voices']; lines = spec['lines']
else: lines = spec
def voice_of(who): return STYLE[who].get('voice', VOICE)
key, region = os.environ.get('AZURE_SPEECH_KEY', ''), os.environ.get('AZURE_SPEECH_REGION', '')
licensed = bool(key and region)

def azure(text, who, path):
    s = STYLE[who]
    ssml = f"""<speak version="1.0" xml:lang="ar-EG" xmlns="http://www.w3.org/2001/10/synthesis"><voice name="{voice_of(who)}">
<prosody rate="{s['rate']}" pitch="{s['pitch']}">{text}</prosody></voice></speak>"""
    req = urllib.request.Request(f'https://{region}.tts.speech.microsoft.com/cognitiveservices/v1', data=ssml.encode('utf8'), headers={
        'Ocp-Apim-Subscription-Key': key, 'Content-Type': 'application/ssml+xml', 'X-Microsoft-OutputFormat': 'riff-24khz-16bit-mono-pcm', 'User-Agent': 'board-battles-ad'})
    open(path, 'wb').write(urllib.request.urlopen(req, timeout=60).read())

async def edge(text, who, path):
    import edge_tts
    s = STYLE[who]
    await edge_tts.Communicate(text, voice_of(who), rate=s['rate'], pitch=s['pitch']).save(path + '.mp3')
    subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-i', path + '.mp3', '-ac', '1', '-ar', '24000', path], check=True)

SR = 24000
def read(path):
    raw = subprocess.run(['ffmpeg', '-loglevel', 'error', '-i', path, '-f', 's16le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True, check=True).stdout
    a = np.frombuffer(raw, np.int16).astype(np.float32) / 32768
    # trim the silence around the line
    idx = np.where(np.abs(a) > 0.01)[0]
    a = a[max(0, idx[0] - 600): idx[-1] + 2400] if len(idx) else a
    # pauses inside a line no longer than a quarter of a second (keeps the pace up)
    win = SR // 50; loud = np.array([np.abs(a[i:i + win]).max() > 0.012 for i in range(0, len(a), win)])
    keep, run = [], 0
    for i, l in enumerate(loud):
        run = 0 if l else run + 1
        if run * win <= 0.22 * SR: keep.append(a[i * win:(i + 1) * win])
    return np.concatenate(keep) if keep else a

parts, timeline, t = [], [], 0.0
for l in lines:
    if l.get('before'): parts.append(np.zeros(int(l['before'] * SR), np.float32)); t += l['before']
    p = os.path.join(out, l['id'] + '.wav')
    if licensed: azure(l['text'], l['who'], p)
    else: asyncio.run(edge(l['text'], l['who'], p))
    a = read(p)
    timeline.append({'id': l['id'], 'who': l['who'], 'text': l['text'], 'start': round(t, 3), 'end': round(t + len(a) / SR, 3)})
    parts.append(a); t += len(a) / SR
    gap = np.zeros(int(l.get('after', 0) * SR), np.float32); parts.append(gap); t += l.get('after', 0)
voice = np.concatenate(parts)
w = wave.open(os.path.join(out, 'voice.wav'), 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
w.writeframes((np.clip(voice, -1, 1) * 32767).astype(np.int16).tobytes()); w.close()
# the mouth: loudness in the speech band, 30 times a second, smoothed and scaled 0..1
fps, hop = 30, SR // 30
env = np.array([np.sqrt(np.mean(voice[i:i + hop] ** 2)) for i in range(0, len(voice), hop)])
env = np.convolve(env, [0.25, 0.5, 0.25], mode='same')
env = np.clip((env - 0.01) / (np.percentile(env[env > 0.01], 90) if (env > 0.01).any() else 1), 0, 1)
who = []
for i in range(len(env)):
    tt = i / fps; who.append(next((x['who'] for x in timeline if x['start'] <= tt < x['end']), ''))
json.dump({'fps': fps, 'open': [round(float(x), 3) for x in env], 'who': who}, open(os.path.join(out, 'mouth.json'), 'w'))
json.dump({'licensed': licensed, 'voice': sorted({voice_of(l['who']) for l in lines}), 'duration': round(t, 3), 'lines': timeline}, open(os.path.join(out, 'timeline.json'), 'w'), ensure_ascii=False, indent=1)
print('licensed' if licensed else 'DRAFT (edge-tts)', round(t, 2), 's')
