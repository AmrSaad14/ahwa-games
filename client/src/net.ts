import type { C2S, S2C } from '../../shared/protocol';

declare const __SERVER_URL__: string;

/** The game server's https address: the build-time SERVER_URL for store apps, otherwise the page's own host. */
export function httpBase(): string {
  return (typeof __SERVER_URL__ === 'string' && __SERVER_URL__ ? __SERVER_URL__ : location.origin).replace(/\/$/, '');
}
/** WebSocket URL of the game server. */
export function socketUrl(): string { return httpBase().replace(/^http/, 'ws') + '/ws'; }

type Listener = (m: S2C) => void;

/** Keeps one connection to the game server alive, reconnecting with backoff and re-sending the session token. */
export class Net {
  private ws: WebSocket | null = null;
  private listeners: Listener[] = [];
  private statusListeners: ((up: boolean) => void)[] = [];
  private retry = 0;
  private pingTimer: ReturnType<typeof setInterval> | null = null;
  private stopped = false;
  ready = false;          // true after the server welcomed us
  clockOffset = 0;        // server time minus local time

  constructor(private getToken: () => string | null) {}

  on(fn: Listener) { this.listeners.push(fn); }
  onStatus(fn: (up: boolean) => void) { this.statusListeners.push(fn); }
  serverNow() { return Date.now() + this.clockOffset; }

  connect() {
    this.stopped = false;
    if (this.ws && (this.ws.readyState === WebSocket.OPEN || this.ws.readyState === WebSocket.CONNECTING)) return;
    let ws: WebSocket;
    try { ws = new WebSocket(socketUrl()); } catch { return this.scheduleRetry(); }
    this.ws = ws;
    ws.onopen = () => {
      this.retry = 0;
      ws.send(JSON.stringify({ t: 'hello', token: this.getToken() ?? undefined, v: 1 } satisfies C2S));
      this.pingTimer = setInterval(() => this.send({ t: 'ping', c: Date.now() }), 20_000);
    };
    ws.onmessage = ev => {
      let m: S2C;
      try { m = JSON.parse(String(ev.data)); } catch { return; }
      if (m.t === 'welcome') { this.ready = true; this.clockOffset = m.now - Date.now(); this.statusListeners.forEach(f => f(true)); }
      if (m.t === 'pong') { const rtt = Date.now() - m.c; this.clockOffset = m.now + rtt / 2 - Date.now(); }
      this.listeners.forEach(f => f(m));
    };
    ws.onclose = ev => {
      if (this.pingTimer) clearInterval(this.pingTimer);
      const was = this.ready;
      this.ready = false; this.ws = null;
      if (was) this.statusListeners.forEach(f => f(false));
      if (ev.code === 4001) return; // replaced by another device/tab: don't fight it
      if (!this.stopped) this.scheduleRetry();
    };
    ws.onerror = () => { /* onclose follows */ };
  }

  private scheduleRetry() {
    const delay = Math.min(15_000, 600 * 2 ** this.retry) * (0.7 + Math.random() * 0.6);
    this.retry++;
    setTimeout(() => this.connect(), delay);
  }

  /** Drop the connection and open a new one (after logging out, for example). */
  reconnect() { this.ws?.close(); this.ws = null; this.ready = false; setTimeout(() => this.connect(), 50); }

  send(m: C2S): boolean {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) return false;
    this.ws.send(JSON.stringify(m));
    return true;
  }
}
