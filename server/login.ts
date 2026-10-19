// Sign-in with a mobile number (a 6-digit code by SMS) and with Google.
//
// Google uses the standard web sign-in: the app opens /auth/google/start in the phone's browser, Google sends
// the player back to /auth/google/callback on this server, and the server hands the app a one-time ticket
// (through the ahwagames:// link on phones, or ?ticket= on the web) that it trades for the account over the socket.
// No Google SDK in the app, so it works the same on Android, iOS and the web.
import crypto from 'node:crypto';
import { Limiter } from './auth';

export interface LoginConfig {
  publicUrl: string;                 // https address of this server, e.g. https://games.example.com
  appScheme: string;                 // the app's link scheme (ahwagames)
  googleClientId: string; googleClientSecret: string;
  sms: 'twilio' | 'http' | 'console' | '';
  twilioSid: string; twilioToken: string; twilioVerifySid: string;
  smsHttpUrl: string; smsHttpToken: string;
  defaultCountry: string;            // calling code used for numbers typed without one (20: Egypt)
  appName: string;
}

/* ---------------- phone numbers ---------------- */

/** Turns what a player typed into +<country><number>, or null. "01012345678" becomes "+201012345678" in Egypt. */
export function normalizePhone(raw: unknown, defaultCountry = '20'): string | null {
  let s = String(raw ?? '').replace(/[\s\-().]/g, '')
    .replace(/[٠-٩]/g, d => String(d.charCodeAt(0) - 0x660)).replace(/[۰-۹]/g, d => String(d.charCodeAt(0) - 0x6f0));
  if (s.startsWith('00')) s = '+' + s.slice(2);
  if (!s.startsWith('+')) s = '+' + defaultCountry + s.replace(/^0/, '');
  if (!/^\+[1-9]\d{7,14}$/.test(s)) return null;
  if (s.startsWith('+20') && !/^\+201[0125]\d{8}$/.test(s)) return null;   // Egyptian mobiles: 010, 011, 012, 015
  return s;
}
/** +201012345678 → +20 10•••••678, for showing which number an account uses. */
export const maskPhone = (p: string) => p.slice(0, 5) + '•'.repeat(Math.max(0, p.length - 8)) + p.slice(-3);

interface Pending { hash: string; exp: number; tries: number }

/** Sends and checks the sign-in codes. */
export class PhoneCodes {
  private pending = new Map<string, Pending>();
  private perPhone = new Limiter(5, 60 * 60_000);
  private perPhoneMinute = new Limiter(1, 55_000);
  private perIp = new Limiter(10, 60 * 60_000);
  /** Codes sent in "console" mode, for tests and local development. */
  lastCode = new Map<string, string>();

  constructor(private cfg: LoginConfig) {}

  get enabled() {
    const c = this.cfg;
    return c.sms === 'console' || (c.sms === 'twilio' && !!(c.twilioSid && c.twilioToken && c.twilioVerifySid)) || (c.sms === 'http' && !!c.smsHttpUrl);
  }

  async send(phone: string, ip: string): Promise<string | null> {
    if (!this.enabled) return 'phone_off';
    if (!this.perIp.allow(ip) || !this.perPhoneMinute.allow(phone) || !this.perPhone.allow(phone)) return 'slow_down';
    try {
      if (this.cfg.sms === 'twilio') {
        await this.twilio(`Services/${this.cfg.twilioVerifySid}/Verifications`, { To: phone, Channel: 'sms' });
        return null;
      }
      const code = String(crypto.randomInt(0, 1_000_000)).padStart(6, '0');
      this.pending.set(phone, { hash: sha(phone + code), exp: Date.now() + 10 * 60_000, tries: 0 });
      if (this.pending.size > 20_000) for (const [k, v] of this.pending) if (v.exp < Date.now()) this.pending.delete(k);
      const text = `${code} is your ${this.cfg.appName} code. كود الدخول: ${code}`;
      if (this.cfg.sms === 'http') {
        const r = await fetch(this.cfg.smsHttpUrl, {
          method: 'POST', headers: { 'Content-Type': 'application/json', ...(this.cfg.smsHttpToken ? { Authorization: `Bearer ${this.cfg.smsHttpToken}` } : {}) },
          body: JSON.stringify({ to: phone, text, code }), signal: AbortSignal.timeout(10_000),
        });
        if (!r.ok) throw new Error('sms gateway ' + r.status);
      } else {
        this.lastCode.set(phone, code);
        console.log(`[sms] ${phone}: ${text}`);
      }
      return null;
    } catch (e) {
      console.error('[sms] sending failed:', (e as Error).message);
      return 'sms_failed';
    }
  }

  async check(phone: string, code: unknown): Promise<boolean> {
    const c = String(code ?? '').replace(/\D/g, '');
    if (c.length !== 6) return false;
    if (this.cfg.sms === 'twilio') {
      try {
        const r = await this.twilio(`Services/${this.cfg.twilioVerifySid}/VerificationCheck`, { To: phone, Code: c }) as { status?: string };
        return r.status === 'approved';
      } catch { return false; }
    }
    const p = this.pending.get(phone);
    if (!p || p.exp < Date.now() || p.tries >= 5) { this.pending.delete(phone); return false; }
    p.tries++;
    const a = Buffer.from(p.hash), b = Buffer.from(sha(phone + c));
    if (a.length !== b.length || !crypto.timingSafeEqual(a, b)) return false;
    this.pending.delete(phone);
    return true;
  }

  private async twilio(pathname: string, form: Record<string, string>) {
    const auth = Buffer.from(`${this.cfg.twilioSid}:${this.cfg.twilioToken}`).toString('base64');
    const r = await fetch(`https://verify.twilio.com/v2/${pathname}`, {
      method: 'POST', headers: { Authorization: `Basic ${auth}`, 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(form), signal: AbortSignal.timeout(10_000),
    });
    const body = await r.json().catch(() => ({}));
    if (!r.ok && r.status !== 404) throw new Error('twilio ' + r.status + ' ' + JSON.stringify(body).slice(0, 200));
    return body;
  }
}

const sha = (s: string) => crypto.createHash('sha256').update(s).digest('base64url');

/* ---------------- Google ---------------- */

export interface GoogleProfile { sub: string; email: string | null; name: string | null }

export class GoogleSignIn {
  private states = new Map<string, { to: 'app' | 'web'; exp: number }>();
  private tickets = new Map<string, GoogleProfile & { exp: number }>();
  constructor(private cfg: LoginConfig) {}

  get enabled() { return !!(this.cfg.googleClientId && this.cfg.googleClientSecret && this.cfg.publicUrl); }
  private get redirectUri() { return this.cfg.publicUrl.replace(/\/$/, '') + '/auth/google/callback'; }

  /** Where to send the player to pick their Google account. */
  startUrl(to: 'app' | 'web'): string {
    const state = crypto.randomBytes(18).toString('base64url');
    this.sweep();
    this.states.set(state, { to, exp: Date.now() + 10 * 60_000 });
    const q = new URLSearchParams({
      client_id: this.cfg.googleClientId, redirect_uri: this.redirectUri, response_type: 'code',
      scope: 'openid email profile', state, prompt: 'select_account',
    });
    return 'https://accounts.google.com/o/oauth2/v2/auth?' + q;
  }

  /** Google sent the player back: trade the code for their Google id and leave a one-time ticket for the app. */
  async callback(code: string, state: string): Promise<{ to: 'app' | 'web'; ticket: string }> {
    const st = this.states.get(state);
    this.states.delete(state);
    if (!st || st.exp < Date.now()) throw new Error('expired');
    const r = await fetch('https://oauth2.googleapis.com/token', {
      method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ code, client_id: this.cfg.googleClientId, client_secret: this.cfg.googleClientSecret, redirect_uri: this.redirectUri, grant_type: 'authorization_code' }),
      signal: AbortSignal.timeout(10_000),
    });
    const body = await r.json() as { id_token?: string; error?: string };
    if (!r.ok || !body.id_token) throw new Error('google token: ' + (body.error || r.status));
    // the token came straight from Google over TLS in exchange for our secret, so its claims can be read directly
    const claims = JSON.parse(Buffer.from(body.id_token.split('.')[1], 'base64url').toString()) as Record<string, unknown>;
    if (claims.aud !== this.cfg.googleClientId || !['accounts.google.com', 'https://accounts.google.com'].includes(String(claims.iss))) throw new Error('google token: wrong audience');
    if (typeof claims.sub !== 'string' || Number(claims.exp) * 1000 < Date.now()) throw new Error('google token: expired');
    const ticket = crypto.randomBytes(24).toString('base64url');
    this.tickets.set(ticket, {
      sub: claims.sub, email: claims.email_verified ? String(claims.email ?? '') || null : null,
      name: typeof claims.given_name === 'string' ? claims.given_name : typeof claims.name === 'string' ? claims.name : null,
      exp: Date.now() + 5 * 60_000,
    });
    return { to: st.to, ticket };
  }

  /** Tests: make a ticket as if Google had signed this person in. */
  testTicket(p: GoogleProfile) { const t = crypto.randomBytes(24).toString('base64url'); this.tickets.set(t, { ...p, exp: Date.now() + 60_000 }); return t; }

  /** A ticket works once, for five minutes. */
  take(ticket: unknown): GoogleProfile | null {
    const t = this.tickets.get(String(ticket ?? ''));
    if (!t) return null;
    this.tickets.delete(String(ticket));
    return t.exp > Date.now() ? { sub: t.sub, email: t.email, name: t.name } : null;
  }

  /** Google Play Games works with the same Google project (its web client id and secret); no web address needed. */
  get playGamesEnabled() { return !!this.testPlayGames || !!(this.cfg.googleClientId && this.cfg.googleClientSecret); }
  /** The client id the app asks Play Games for a server code with (public). */
  get clientId() { return this.cfg.googleClientId; }
  /** Tests: a fake Play Games exchange. */
  testPlayGames: ((code: string) => GoogleProfile | null) | null = null;

  /**
   * Google Play Games: the app signed the player in with Play Games and got a one-time server code for us.
   * Trading it (with our secret) gives an access token, and the Games API says which player that is.
   */
  async playGames(code: string): Promise<GoogleProfile> {
    if (this.testPlayGames) { const p = this.testPlayGames(code); if (!p) throw new Error('play games: bad code'); return p; }
    const r = await fetch('https://oauth2.googleapis.com/token', {
      method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ code, client_id: this.cfg.googleClientId, client_secret: this.cfg.googleClientSecret, redirect_uri: '', grant_type: 'authorization_code' }),
    });
    const tok = await r.json().catch(() => ({})) as { access_token?: string; error?: string };
    if (!r.ok || !tok.access_token) throw new Error('play games token: ' + (tok.error || r.status));
    const me = await fetch('https://games.googleapis.com/games/v1/players/me', { headers: { Authorization: 'Bearer ' + tok.access_token } });
    const p = await me.json().catch(() => ({})) as { playerId?: string; displayName?: string };
    if (!me.ok || !p.playerId) throw new Error('play games player: ' + me.status);
    return { sub: p.playerId, email: null, name: p.displayName ?? null };
  }

  appLink(ticket: string) { return `${this.cfg.appScheme}://auth?ticket=${encodeURIComponent(ticket)}`; }
  webLink(ticket: string) { return this.cfg.publicUrl.replace(/\/$/, '') + '/?ticket=' + encodeURIComponent(ticket); }

  private sweep() {
    const now = Date.now();
    if (this.states.size > 5000) for (const [k, v] of this.states) if (v.exp < now) this.states.delete(k);
    if (this.tickets.size > 5000) for (const [k, v] of this.tickets) if (v.exp < now) this.tickets.delete(k);
  }
}

/** The page shown in the phone's browser after Google, which opens the game again. */
export function backToAppPage(link: string, appName: string, ok: boolean): string {
  const esc = (s: string) => s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
  return `<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>${esc(appName)}</title><style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#1b120c;color:#f3e9d4;font:16px system-ui,sans-serif;text-align:center}
a{display:inline-block;margin-top:16px;padding:14px 26px;border-radius:12px;background:#d9a84e;color:#1b120c;font-weight:700;text-decoration:none}</style></head>
<body><main><p>${ok ? 'تم تسجيل الدخول · Signed in' : 'لم يتم تسجيل الدخول · Sign-in failed'}</p>
${ok ? `<a href="${esc(link)}">ارجع للعبة · Back to the game</a><script>location.href=${JSON.stringify(link)}</script>` : `<p>حاول تاني · Please try again</p>`}</main></body></html>`;
}
