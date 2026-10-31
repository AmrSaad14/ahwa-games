// Unity Ads' server-to-server callback (/ads/unity): the signature check and what it hands back.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { verifyUnityS2S } from '../server/ads';

const SECRET = 'unity-test-secret';
const sign = (p: Record<string, string>) => {
  const msg = Object.keys(p).sort().map(k => `${k}=${p[k]}`).join(',');
  return new URLSearchParams({ ...p, hmac: crypto.createHmac('md5', SECRET).update(msg).digest('hex') });
};

test('a signed callback gives the player and the ticket back', () => {
  const v = verifyUnityS2S(sign({ sid: 'u_abc:tick-1', oid: '987', productid: '1234' }), SECRET);
  assert.deepEqual(v, { uid: 'u_abc', ticket: 'tick-1', oid: '987' });
});

test('a changed value, a wrong secret or no secret is refused', () => {
  const p = sign({ sid: 'u_abc:tick-1', oid: '987' });
  p.set('sid', 'u_evil:tick-1');
  assert.equal(verifyUnityS2S(p, SECRET), null);
  assert.equal(verifyUnityS2S(sign({ sid: 'u_abc:t', oid: '1' }), 'other'), null);
  assert.equal(verifyUnityS2S(sign({ sid: 'u_abc:t', oid: '1' }), ''), null);
  assert.equal(verifyUnityS2S(sign({ sid: 'no-ticket', oid: '1' }), SECRET), null);
});
