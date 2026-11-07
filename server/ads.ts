// Rewarded video ads: coins for watching one, up to a daily limit.
//
// Unity Ads (the game's network): with UNITY_ADS_SECRET set, Unity itself calls GET /ads/unity after the player
// watched the video (signed with that secret, from Unity support); only then are coins added.
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

/**
 * Checks a Unity Ads server-to-server redeem callback (GET /ads/unity?sid=…&oid=…&hmac=…). Unity signs every
 * parameter but hmac, sorted by name, as "k=v" joined by commas, with HMAC-MD5 and the secret its support
 * gives you (UNITY_ADS_SECRET). Returns the player id and ticket we put in sid ("uid:ticket"), or null.
 */
export function verifyUnityS2S(params: URLSearchParams, secret: string): { uid: string; ticket: string; oid: string } | null {
  const hmac = params.get('hmac'), sid = params.get('sid'), oid = params.get('oid');
  if (!secret || !hmac || !sid || !oid) return null;
  const msg = [...params.entries()].filter(([k]) => k !== 'hmac').sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0).map(([k, v]) => `${k}=${v}`).join(',');
  const want = crypto.createHmac('md5', secret).update(msg).digest('hex');
  const a = Buffer.from(want), b = Buffer.from(hmac.toLowerCase());
  if (a.length !== b.length || !crypto.timingSafeEqual(a, b)) return null;
  const i = sid.lastIndexOf(':');
  if (i <= 0) return null;
  return { uid: sid.slice(0, i), ticket: sid.slice(i + 1), oid };
}

export interface AdFor { skin?: string; wheel?: boolean; double?: string }

/** One-time tickets: the app asks for one before showing an ad and passes it to AdMob as custom data. */
export class AdTickets {
  private open = new Map<string, { uid: string; exp: number } & AdFor>();
  /** What the ad is for: a skin (one step towards it), the lucky wheel (towards the next spin), doubling a
   *  match's winnings; nothing: coins. A bare string is a skin (as before). */
  issue(uid: string, what?: string | AdFor) {
    const t = crypto.randomBytes(16).toString('base64url');
    if (this.open.size > 20_000) for (const [k, v] of this.open) if (v.exp < Date.now()) this.open.delete(k);
    const f: AdFor = typeof what === 'string' ? { skin: what } : (what ?? {});
    this.open.set(t, { uid, exp: Date.now() + 30 * 60_000, ...f });
    return t;
  }
  /** Uses up a ticket: what it was for if it was issued to this player and hasn't expired, otherwise null. */
  take(ticket: string, uid: string): AdFor | null {
    const t = this.open.get(ticket);
    if (!t) return null;
    this.open.delete(ticket);
    return t.uid === uid && t.exp > Date.now() ? { skin: t.skin, wheel: t.wheel, double: t.double } : null;
  }
}
