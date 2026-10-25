// Sign in with Apple, the web way (works from the Android app, iPhones and browsers alike, no Apple SDK):
// the app opens /auth/apple/start in the browser, Apple sends the player back to /auth/apple/callback (a form
// POST), and the server hands the app a one-time ticket through the ahwagames:// link, as with Google.
//
// Needs an Apple Developer account (developer.apple.com, $99 a year):
//   APPLE_SERVICE_ID   the Services ID set up for "Sign in with Apple" (e.g. com.ahwa.boardbattles.signin)
//   APPLE_TEAM_ID      your team id (10 characters, top right in the developer site)
//   APPLE_KEY_ID       the id of a key with "Sign in with Apple" enabled
//   APPLE_PRIVATE_KEY  that key's .p8 file contents (newlines may be written as \n)
// and in the Services ID: the domain of this server and the return URL https://YOUR-SERVER/auth/apple/callback.
import crypto from 'node:crypto';

export interface AppleConfig { serviceId: string; teamId: string; keyId: string; privateKey: string; publicUrl: string; appScheme: string }
export interface AppleProfile { sub: string; email: string | null; name: string | null }

export class AppleSignIn {
  private states = new Map<string, { to: 'app' | 'web'; exp: number }>();
  private tickets = new Map<string, AppleProfile & { exp: number }>();
  private secret: { jwt: string; exp: number } | null = null;
  constructor(private cfg: AppleConfig) {}

  get enabled() { return !!(this.cfg.serviceId && this.cfg.teamId && this.cfg.keyId && this.cfg.privateKey && this.cfg.publicUrl); }
  private get redirectUri() { return this.cfg.publicUrl.replace(/\/$/, '') + '/auth/apple/callback'; }

  startUrl(to: 'app' | 'web'): string {
    const state = crypto.randomBytes(18).toString('base64url');
    this.sweep();
    this.states.set(state, { to, exp: Date.now() + 10 * 60_000 });
    const q = new URLSearchParams({
      client_id: this.cfg.serviceId, redirect_uri: this.redirectUri, response_type: 'code', scope: 'name email',
      response_mode: 'form_post', state,
    });
    return 'https://appleid.apple.com/auth/authorize?' + q;
  }

  /** The client secret Apple wants: a short JWT signed with the .p8 key (ES256), reused for a day. */
  private clientSecret(): string {
    if (this.secret && this.secret.exp > Date.now() + 60_000) return this.secret.jwt;
    const now = Math.floor(Date.now() / 1000);
    const enc = (o: object) => Buffer.from(JSON.stringify(o)).toString('base64url');
    const data = enc({ alg: 'ES256', kid: this.cfg.keyId }) + '.' + enc({ iss: this.cfg.teamId, iat: now, exp: now + 86_400, aud: 'https://appleid.apple.com', sub: this.cfg.serviceId });
    const key = this.cfg.privateKey.replace(/\\n/g, '\n');
    const sig = crypto.sign('sha256', Buffer.from(data), { key, dsaEncoding: 'ieee-p1363' }).toString('base64url');
    this.secret = { jwt: data + '.' + sig, exp: (now + 86_400) * 1000 };
    return this.secret.jwt;
  }

  /** Apple sent the player back (form fields code, state, and the first time only, user with their name). */
  async callback(code: string, state: string, userJson: string): Promise<{ to: 'app' | 'web'; ticket: string }> {
    const st = this.states.get(state);
    this.states.delete(state);
    if (!st || st.exp < Date.now()) throw new Error('expired');
    const r = await fetch('https://appleid.apple.com/auth/token', {
      method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ client_id: this.cfg.serviceId, client_secret: this.clientSecret(), code, grant_type: 'authorization_code', redirect_uri: this.redirectUri }),
      signal: AbortSignal.timeout(10_000),
    });
    const body = await r.json().catch(() => ({})) as { id_token?: string; error?: string };
    if (!r.ok || !body.id_token) throw new Error('apple token: ' + (body.error || r.status));
    // straight from Apple over TLS, in exchange for our signed secret: the claims can be read directly
    const claims = JSON.parse(Buffer.from(body.id_token.split('.')[1], 'base64url').toString()) as Record<string, unknown>;
    if (claims.iss !== 'https://appleid.apple.com' || claims.aud !== this.cfg.serviceId) throw new Error('apple token: wrong issuer');
    if (typeof claims.sub !== 'string' || Number(claims.exp) * 1000 < Date.now()) throw new Error('apple token: expired');
    let name: string | null = null;
    try { const u = JSON.parse(userJson || '{}') as { name?: { firstName?: string } }; name = u.name?.firstName ?? null; } catch { /* no name */ }
    const verified = claims.email_verified === true || claims.email_verified === 'true';
    const ticket = crypto.randomBytes(24).toString('base64url');
    this.tickets.set(ticket, { sub: claims.sub, email: verified ? String(claims.email ?? '') || null : null, name, exp: Date.now() + 5 * 60_000 });
    return { to: st.to, ticket };
  }

  /** Tests: a ticket as if Apple had signed this person in. */
  testTicket(p: AppleProfile) { const t = crypto.randomBytes(24).toString('base64url'); this.tickets.set(t, { ...p, exp: Date.now() + 60_000 }); return t; }

  take(ticket: unknown): AppleProfile | null {
    const t = this.tickets.get(String(ticket ?? ''));
    if (!t) return null;
    this.tickets.delete(String(ticket));
    return t.exp > Date.now() ? { sub: t.sub, email: t.email, name: t.name } : null;
  }

  appLink(ticket: string) { return `${this.cfg.appScheme}://auth?ticket=${encodeURIComponent(ticket)}`; }
  webLink(ticket: string) { return this.cfg.publicUrl.replace(/\/$/, '') + '/?ticket=' + encodeURIComponent(ticket); }

  private sweep() {
    const now = Date.now();
    if (this.states.size > 5000) for (const [k, v] of this.states) if (v.exp < now) this.states.delete(k);
    if (this.tickets.size > 5000) for (const [k, v] of this.tickets) if (v.exp < now) this.tickets.delete(k);
  }
}
