import crypto from 'node:crypto';

/** Signed session tokens: base64url(userId).issuedAt.signature (HMAC-SHA256). */
export class Tokens {
  constructor(private secret: string) {}

  issue(uid: string): string {
    const body = Buffer.from(uid).toString('base64url') + '.' + Date.now().toString(36);
    return body + '.' + this.sign(body);
  }

  verify(token: unknown): string | null {
    if (typeof token !== 'string' || token.length > 300) return null;
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const body = parts[0] + '.' + parts[1];
    const expected = this.sign(body);
    const a = Buffer.from(parts[2]), b = Buffer.from(expected);
    if (a.length !== b.length || !crypto.timingSafeEqual(a, b)) return null;
    try { return Buffer.from(parts[0], 'base64url').toString(); } catch { return null; }
  }

  private sign(body: string) { return crypto.createHmac('sha256', this.secret).update(body).digest('base64url'); }
}

export function hashPassword(pw: string): string {
  const salt = crypto.randomBytes(16);
  const hash = crypto.scryptSync(pw, salt, 64, { N: 16384, r: 8, p: 1 });
  return `scrypt$${salt.toString('base64url')}$${hash.toString('base64url')}`;
}

export function checkPassword(pw: string, stored: string | null): boolean {
  if (!stored) return false;
  const [kind, salt, hash] = stored.split('$');
  if (kind !== 'scrypt' || !salt || !hash) return false;
  const want = Buffer.from(hash, 'base64url');
  const got = crypto.scryptSync(pw, Buffer.from(salt, 'base64url'), want.length, { N: 16384, r: 8, p: 1 });
  return crypto.timingSafeEqual(want, got);
}

/** Simple sliding-window limiter keyed by string (IP, user id…). */
export class Limiter {
  private hits = new Map<string, number[]>();
  constructor(private max: number, private windowMs: number) {}
  allow(key: string): boolean {
    const now = Date.now();
    const arr = (this.hits.get(key) || []).filter(t => now - t < this.windowMs);
    if (arr.length >= this.max) { this.hits.set(key, arr); return false; }
    arr.push(now); this.hits.set(key, arr);
    if (this.hits.size > 50_000) this.hits.clear();
    return true;
  }
}
