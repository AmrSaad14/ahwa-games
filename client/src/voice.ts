import type { C2S, IceServer, RtcSignal, AnyView } from '../../shared/protocol';

interface Peer { pc: RTCPeerConnection; audio: HTMLAudioElement | null; initiator: boolean; retry: ReturnType<typeof setTimeout> | null }

/**
 * Table voice chat: every player who joins voice gets a direct audio connection (WebRTC) to every other
 * player in voice at the same table — at most three connections, so no media server is needed.
 * The game server only passes the connection offers along; the audio itself never goes through it
 * (except via the TURN relay when two phones can't reach each other directly).
 */
export class Voice {
  joined = false;
  micMuted = false;
  ice: IceServer[] = [];
  /** Seats whose audio you have muted on this table. */
  readonly muted = new Set<number>();
  private stream: MediaStream | null = null;
  private peers = new Map<number, Peer>();
  private tableId = '';
  private you = -1;
  private ctx: AudioContext | null = null;
  private meters = new Map<number, { an: AnalyserNode; buf: Uint8Array<ArrayBuffer> }>();
  private levels = new Map<number, number>();
  private meterTimer: ReturnType<typeof setInterval> | null = null;
  onChange: () => void = () => {};

  constructor(private send: (m: C2S) => boolean) {}

  static supported() { return typeof navigator !== 'undefined' && !!navigator.mediaDevices && 'getUserMedia' in navigator.mediaDevices && 'RTCPeerConnection' in window; }

  /** Asks for the microphone and joins the table's voice chat. */
  async join(): Promise<'ok' | 'denied' | 'unsupported'> {
    if (!Voice.supported()) return 'unsupported';
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }, video: false });
    } catch { return 'denied'; }
    this.joined = true; this.micMuted = false;
    this.meter(-1, this.stream);
    this.send({ t: 'voice', on: true });
    this.onChange();
    return 'ok';
  }

  leave() {
    if (this.joined) this.send({ t: 'voice', on: false });
    this.joined = false;
    this.closeAll();
    this.stream?.getTracks().forEach(t => t.stop());
    this.stream = null;
    this.meters.delete(-1);
    this.onChange();
  }

  setMicMuted(m: boolean) {
    this.micMuted = m;
    this.stream?.getAudioTracks().forEach(t => { t.enabled = !m; });
    this.onChange();
  }

  setMuted(seat: number, m: boolean) {
    if (m) this.muted.add(seat); else this.muted.delete(seat);
    const p = this.peers.get(seat); if (p?.audio) p.audio.muted = m;
    this.onChange();
  }

  /** Is this seat (or -1 for you) speaking right now? */
  talking(seat: number) { return (this.levels.get(seat) ?? 0) > 0.035; }
  connected(seat: number) { return this.peers.get(seat)?.pc.connectionState === 'connected'; }
  states() { return [...this.peers.entries()].map(([s, p]) => [s, p.pc.connectionState] as const); }

  /** Keep connections in step with who is in voice at the table. */
  sync(v: AnyView | null) {
    if (!v || v.phase === undefined) { this.closeAll(); return; }
    if (v.id !== this.tableId) { this.closeAll(); this.muted.clear(); this.tableId = v.id; }
    this.you = v.you;
    if (!this.joined || v.you < 0) return;
    const want = new Set<number>();
    v.seats.forEach((s, i) => { if (s && i !== v.you && s.mic && s.online && !s.bot) want.add(i); });
    for (const seat of [...this.peers.keys()]) if (!want.has(seat)) this.close(seat);
    // the lower seat number starts each connection, so two players never call each other at once
    for (const seat of want) if (!this.peers.has(seat) && v.you < seat) this.call(seat);
  }

  async onSignal(from: number, data: RtcSignal) {
    if (!this.joined || data.table !== this.tableId) return;
    if (data.type === 'offer') {
      this.close(from);
      const p = this.makePeer(from, false);
      await p.pc.setRemoteDescription({ type: 'offer', sdp: data.sdp });
      await p.pc.setLocalDescription(await p.pc.createAnswer());
      await gathered(p.pc);
      if (p.pc.localDescription) this.send({ t: 'rtc', to: from, data: { type: 'answer', sdp: p.pc.localDescription.sdp, table: this.tableId } });
    } else {
      const p = this.peers.get(from);
      if (p && p.pc.signalingState === 'have-local-offer') await p.pc.setRemoteDescription({ type: 'answer', sdp: data.sdp });
    }
  }

  private async call(seat: number) {
    const p = this.makePeer(seat, true);
    await p.pc.setLocalDescription(await p.pc.createOffer());
    await gathered(p.pc);
    if (this.peers.get(seat) === p && p.pc.localDescription) {
      this.send({ t: 'rtc', to: seat, data: { type: 'offer', sdp: p.pc.localDescription.sdp, table: this.tableId } });
    }
  }

  private makePeer(seat: number, initiator: boolean): Peer {
    const pc = new RTCPeerConnection({ iceServers: this.ice as RTCIceServer[] });
    const p: Peer = { pc, audio: null, initiator, retry: null };
    this.peers.set(seat, p);
    this.stream?.getTracks().forEach(t => pc.addTrack(t, this.stream!));
    pc.ontrack = e => {
      const remote = e.streams[0] ?? new MediaStream([e.track]);
      if (!p.audio) {
        p.audio = document.createElement('audio');
        p.audio.autoplay = true; p.audio.setAttribute('playsinline', ''); p.audio.hidden = true;
        document.body.appendChild(p.audio);
      }
      p.audio.srcObject = remote; p.audio.muted = this.muted.has(seat);
      p.audio.play().catch(() => {});
      this.meter(seat, remote);
      this.onChange();
    };
    pc.onconnectionstatechange = () => {
      this.onChange();
      if (pc.connectionState === 'failed' && p.initiator && this.peers.get(seat) === p) {
        p.retry = setTimeout(() => { if (this.peers.get(seat) === p) { this.close(seat); if (this.joined) this.call(seat); } }, 2000);
      }
    };
    return p;
  }

  private close(seat: number) {
    const p = this.peers.get(seat); if (!p) return;
    if (p.retry) clearTimeout(p.retry);
    p.pc.ontrack = null; p.pc.onconnectionstatechange = null; p.pc.close();
    if (p.audio) { p.audio.srcObject = null; p.audio.remove(); }
    this.peers.delete(seat); this.meters.delete(seat); this.levels.delete(seat);
  }

  private closeAll() { for (const s of [...this.peers.keys()]) this.close(s); }

  /** Volume meter for the "is talking" glow. */
  private meter(seat: number, stream: MediaStream) {
    try {
      this.ctx ??= new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)();
      const src = this.ctx.createMediaStreamSource(stream);
      const an = this.ctx.createAnalyser(); an.fftSize = 512;
      src.connect(an);
      this.meters.set(seat, { an, buf: new Uint8Array(new ArrayBuffer(an.fftSize)) });
      this.meterTimer ??= setInterval(() => {
        let changed = false;
        for (const [s, m] of this.meters) {
          m.an.getByteTimeDomainData(m.buf);
          let sum = 0; for (const x of m.buf) { const d = (x - 128) / 128; sum += d * d; }
          const lvl = Math.sqrt(sum / m.buf.length);
          const was = this.talking(s);
          this.levels.set(s, s === -1 && this.micMuted ? 0 : lvl);
          if (was !== this.talking(s)) changed = true;
        }
        if (changed) this.onChange();
      }, 150);
    } catch { /* no level meter on this device */ }
  }
}

/** Wait until the connection has gathered its network candidates (or 2.5 s), so one message carries everything. */
function gathered(pc: RTCPeerConnection) {
  if (pc.iceGatheringState === 'complete') return Promise.resolve();
  return new Promise<void>(resolve => {
    const done = () => { pc.removeEventListener('icegatheringstatechange', check); clearTimeout(timer); resolve(); };
    const check = () => { if (pc.iceGatheringState === 'complete') done(); };
    const timer = setTimeout(done, 2500);
    pc.addEventListener('icegatheringstatechange', check);
  });
}
