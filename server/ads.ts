// Rewarded video ads: 5K coins for watching one, up to a daily limit.
//
// Safe mode (ADS_SSV=1, recommended for real money ads): AdMob itself calls GET /ads/ssv on this server
// after the player watched the ad, signed with Google's key; only then are coins added. The app can't fake it.
// Without ADS_SSV the server trusts the app's "watched" message, still limited to one reward per ticket
// and ADS_PER_DAY per player — fine for testing, easy to abuse with a modified app.
import crypto from 'node:crypto';

const KEYS_URL = 'https://www.gstatic.com/admob/reward/verifier-keys.json';
let keys: { at: number; byId: Map<string, crypto.KeyObject> } | null = null;

async function verifierKey(id: string): Promise<crypto.KeyObject | null> {
  if (!keys || Date.now() - keys.at > 12 * 3600_000 || !keys.byId.has(id)) {
    const r = await fetch(KEYS_URL, { signal: AbortSignal.timeout(10_000) });
    const body = await r.json() as { keys: { keyId: number | string; pem: string }[] };
    keys = { at: Date.now(), byId: new Map(body.keys.map(k => [String(k.keyId), crypto.createPublicKey(k.pem)])) };
  }
  return keys.byId.get(id) ?? null;
}

/**
 * Checks an AdMob server-side verification callback. `query` is the raw query string (without "?").
 * Returns the fields Google sent, or null if the signature is wrong.
 */
export async function verifySsv(query: string, key?: crypto.KeyObject): Promise<URLSearchParams | null> {
  const i = query.indexOf('&signature=');
  if (i < 0) return null;
  const message = query.slice(0, i);
  const rest = new URLSearchParams(query.slice(i + 1));
  const sig = rest.get('signature'), keyId = rest.get('key_id');
  if (!sig || !keyId) return null;
  try {
    const k = key ?? await verifierKey(keyId);
    if (!k) return null;
    const ok = crypto.verify('sha256', Buffer.from(message), k, Buffer.from(sig, 'base64url'));
    return ok ? new URLSearchParams(message) : null;
  } catch { return null; }
}

/** One-time tickets: the app asks for one before showing an ad and passes it to AdMob as custom data. */
export class AdTickets {
  private open = new Map<string, { uid: string; exp: number }>();
  issue(uid: string) {
    const t = crypto.randomBytes(16).toString('base64url');
    if (this.open.size > 20_000) for (const [k, v] of this.open) if (v.exp < Date.now()) this.open.delete(k);
    this.open.set(t, { uid, exp: Date.now() + 30 * 60_000 });
    return t;
  }
  /** Uses up a ticket; true if it was issued to this player and hasn't expired. */
  take(ticket: string, uid: string) {
    const t = this.open.get(ticket);
    if (!t) return false;
    this.open.delete(ticket);
    return t.uid === uid && t.exp > Date.now();
  }
}
