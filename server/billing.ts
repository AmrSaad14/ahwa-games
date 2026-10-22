// Checks Google Play purchases with Google before any coins or ad-free days are given.
//
// The app buys through Google Play Billing and sends the purchase token here; this asks the Google Play
// Developer API whether it is real and paid, then the hub credits it once (tokens are remembered).
//
//   GOOGLE_PLAY_SERVICE_ACCOUNT   the service account key (JSON, or the same JSON base64-encoded) of a Google
//                                 Cloud service account invited to the Play Console with "View financial data"
//                                 and "Manage orders and subscriptions"
//   ANDROID_PACKAGE               the app ID (default com.ahwa.domino)
//   PURCHASES_TEST=1              development only: accept tokens starting with "test:" without asking Google
import crypto from 'node:crypto';
import { NO_ADS, packById } from '../shared/gifts';
import { SEASON_PASS } from '../shared/season';

export interface Verified { ok: true; coins: number; until: number; orderId: string | null }
export interface Refused { ok: false; code: string }

interface ServiceAccount { client_email: string; private_key: string }

const b64url = (b: Buffer | string) => Buffer.from(b).toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
/** The tag the app attaches to a purchase so it can only be credited to the account that bought it. */
export const accountTags = (uid: string) => [uid, crypto.createHash('md5').update(uid).digest('hex'), crypto.createHash('sha256').update(uid).digest('hex')];

export class PlayBilling {
  private sa: ServiceAccount | null = null;
  private token: { value: string; until: number } | null = null;

  constructor(private cfg: { serviceAccount: string; packageName: string; testMode: boolean }) {
    const raw = cfg.serviceAccount.trim();
    if (raw) {
      try { this.sa = JSON.parse(raw.startsWith('{') ? raw : Buffer.from(raw, 'base64').toString('utf8')) as ServiceAccount; }
      catch { console.error('[billing] GOOGLE_PLAY_SERVICE_ACCOUNT is not valid JSON'); }
    }
  }
  get enabled() { return !!this.sa || this.cfg.testMode; }

  /** An OAuth access token for the Play Developer API (a signed JWT traded with Google, cached for an hour). */
  private async accessToken(): Promise<string> {
    if (this.token && this.token.until > Date.now() + 60_000) return this.token.value;
    const sa = this.sa!, now = Math.floor(Date.now() / 1000);
    const head = b64url(JSON.stringify({ alg: 'RS256', typ: 'JWT' }));
    const body = b64url(JSON.stringify({ iss: sa.client_email, scope: 'https://www.googleapis.com/auth/androidpublisher', aud: 'https://oauth2.googleapis.com/token', iat: now, exp: now + 3600 }));
    const sig = b64url(crypto.createSign('RSA-SHA256').update(`${head}.${body}`).sign(sa.private_key));
    const r = await fetch('https://oauth2.googleapis.com/token', {
      method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer', assertion: `${head}.${body}.${sig}` }),
    });
    const j = await r.json() as { access_token?: string; expires_in?: number; error?: string };
    if (!j.access_token) throw new Error('Google token: ' + (j.error || r.status));
    this.token = { value: j.access_token, until: Date.now() + (j.expires_in ?? 3600) * 1000 };
    return j.access_token;
  }
  private async play(path: string, method = 'GET') {
    const base = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${encodeURIComponent(this.cfg.packageName)}`;
    const r = await fetch(base + path, { method, headers: { Authorization: `Bearer ${await this.accessToken()}` } });
    if (r.status === 404 || r.status === 400 || r.status === 410) return null;
    if (!r.ok) throw new Error(`Play API ${method} ${path.split('/tokens/')[0]}: ${r.status}`);
    return r.status === 204 ? {} : await r.json() as Record<string, unknown>;
  }

  /** Is this a real, paid purchase of `product` by player `uid`? */
  async verify(uid: string, product: string, token: string): Promise<Verified | Refused> {
    const pack = packById(product) ?? (product === SEASON_PASS.id ? { id: product, coins: 0, egp: SEASON_PASS.egp } : undefined), sub = product === NO_ADS.id;
    if (!pack && !sub) return { ok: false, code: 'bad_product' };
    if (!token || token.length > 4096) return { ok: false, code: 'bad_purchase' };
    if (this.cfg.testMode && token.startsWith('test:')) {
      return { ok: true, coins: pack?.coins ?? 0, until: sub ? Date.now() + NO_ADS.days * 86_400_000 : 0, orderId: null };
    }
    if (!this.sa) return { ok: false, code: 'purchases_off' };
    const t = encodeURIComponent(token);
    const mine = (tag: unknown) => !tag || accountTags(uid).includes(String(tag));
    if (pack) {
      const p = await this.play(`/purchases/products/${encodeURIComponent(product)}/tokens/${t}`);
      if (!p) return { ok: false, code: 'bad_purchase' };
      if (p.purchaseState !== 0) return { ok: false, code: p.purchaseState === 2 ? 'purchase_pending' : 'bad_purchase' };
      if (!mine(p.obfuscatedExternalAccountId)) return { ok: false, code: 'wrong_account' };
      // consumed here as well as in the app, so a crash between paying and crediting can't lose or repeat it
      if (p.consumptionState === 0) await this.play(`/purchases/products/${encodeURIComponent(product)}/tokens/${t}:consume`, 'POST').catch(() => null);
      return { ok: true, coins: pack.coins, until: 0, orderId: (p.orderId as string) ?? null };
    }
    const s = await this.play(`/purchases/subscriptionsv2/tokens/${t}`);
    if (!s) return { ok: false, code: 'bad_purchase' };
    const items = (s.lineItems as { productId: string; expiryTime?: string }[] | undefined) ?? [];
    const until = Math.max(0, ...items.filter(i => i.productId === NO_ADS.id).map(i => Date.parse(i.expiryTime || '') || 0));
    const ids = s.externalAccountIdentifiers as { obfuscatedExternalAccountId?: string } | undefined;
    if (!mine(ids?.obfuscatedExternalAccountId)) return { ok: false, code: 'wrong_account' };
    if (s.subscriptionState === 'SUBSCRIPTION_STATE_PENDING') return { ok: false, code: 'purchase_pending' };
    if (until <= Date.now()) return { ok: false, code: 'expired' };
    if (s.acknowledgementState === 'ACKNOWLEDGEMENT_STATE_PENDING') {
      await this.play(`/purchases/subscriptions/${encodeURIComponent(NO_ADS.id)}/tokens/${t}:acknowledge`, 'POST').catch(() => null);
    }
    return { ok: true, coins: 0, until, orderId: (s.latestOrderId as string) ?? null };
  }
}
