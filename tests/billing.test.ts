// Google Play purchase checks, with Google's token and Play Developer API answered by a stand-in fetch.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { PlayBilling } from '../server/billing';

const { privateKey, publicKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const sa = { client_email: 'shop@game.iam.gserviceaccount.com', private_key: privateKey.export({ type: 'pkcs8', format: 'pem' }).toString() };

function fakeGoogle(answers: Record<string, unknown>) {
  const calls: string[] = [];
  const real = globalThis.fetch;
  globalThis.fetch = (async (url: string, init: RequestInit = {}) => {
    const u = String(url); calls.push(`${init.method || 'GET'} ${u.replace(/^https:\/\/[^/]+/, '')}`);
    if (u === 'https://oauth2.googleapis.com/token') {
      // the JWT must be signed by the service account's key
      const jwt = new URLSearchParams(String(init.body)).get('assertion')!.split('.');
      const ok = crypto.createVerify('RSA-SHA256').update(`${jwt[0]}.${jwt[1]}`).verify(publicKey, Buffer.from(jwt[2], 'base64url'));
      const claims = JSON.parse(Buffer.from(jwt[1], 'base64url').toString());
      assert.ok(ok, 'JWT signature'); assert.equal(claims.iss, sa.client_email); assert.match(claims.scope, /androidpublisher/);
      return new Response(JSON.stringify({ access_token: 'ya29.test', expires_in: 3600 }));
    }
    assert.equal((init.headers as Record<string, string>).Authorization, 'Bearer ya29.test');
    const key = Object.keys(answers).find(k => u.includes(k));
    return key ? new Response(JSON.stringify(answers[key]), { status: 200 }) : new Response('{}', { status: 404 });
  }) as typeof fetch;
  return { calls, restore: () => { globalThis.fetch = real; } };
}

test('billing: a paid coin pack is accepted and consumed; pending and other-account purchases are not', async () => {
  const g = fakeGoogle({
    '/products/coins_100k/tokens/good': { purchaseState: 0, consumptionState: 0, orderId: 'GPA.1', obfuscatedExternalAccountId: crypto.createHash('md5').update('u1').digest('hex') },
    '/products/coins_100k/tokens/pending': { purchaseState: 2 },
    '/products/coins_100k/tokens/theirs': { purchaseState: 0, obfuscatedExternalAccountId: 'someone-else' },
    ':consume': {},
  });
  try {
    const b = new PlayBilling({ serviceAccount: Buffer.from(JSON.stringify(sa)).toString('base64'), packageName: 'com.ahwa.domino', testMode: false });
    assert.deepEqual(await b.verify('u1', 'coins_100k', 'good'), { ok: true, coins: 100_000, until: 0, orderId: 'GPA.1' });
    assert.ok(g.calls.some(c => c.startsWith('POST') && c.includes('/tokens/good:consume')), 'consumed on the server too');
    assert.deepEqual(await b.verify('u1', 'coins_100k', 'pending'), { ok: false, code: 'purchase_pending' });
    assert.deepEqual(await b.verify('u1', 'coins_100k', 'theirs'), { ok: false, code: 'wrong_account' });
    assert.deepEqual(await b.verify('u1', 'coins_100k', 'unknown'), { ok: false, code: 'bad_purchase' });
    assert.equal(g.calls.filter(c => c.includes('oauth2') || c.startsWith('POST /token')).length, 1, 'one access token, reused');
  } finally { g.restore(); }
});

test('billing: an active ad-free subscription returns its paid-until date and gets acknowledged', async () => {
  const until = new Date(Date.now() + 30 * 86_400_000).toISOString();
  const g = fakeGoogle({
    '/subscriptionsv2/tokens/sub': { subscriptionState: 'SUBSCRIPTION_STATE_ACTIVE', acknowledgementState: 'ACKNOWLEDGEMENT_STATE_PENDING', latestOrderId: 'GPA.2', lineItems: [{ productId: 'no_ads_monthly', expiryTime: until }] },
    '/subscriptionsv2/tokens/old': { subscriptionState: 'SUBSCRIPTION_STATE_EXPIRED', lineItems: [{ productId: 'no_ads_monthly', expiryTime: '2020-01-01T00:00:00Z' }] },
    ':acknowledge': {},
  });
  try {
    const b = new PlayBilling({ serviceAccount: JSON.stringify(sa), packageName: 'com.ahwa.domino', testMode: false });
    const r = await b.verify('u1', 'no_ads_monthly', 'sub');
    assert.ok(r.ok && r.until === Date.parse(until));
    assert.ok(g.calls.some(c => c.includes('/subscriptions/no_ads_monthly/tokens/sub:acknowledge')));
    assert.deepEqual(await b.verify('u1', 'no_ads_monthly', 'old'), { ok: false, code: 'expired' });
  } finally { g.restore(); }
});
