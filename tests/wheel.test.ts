import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Store } from '../server/db';
import { WHEEL, wheelCost } from '../shared/gifts';

test('lucky wheel: one free spin a day, then 1, 3, 4, 5 … ads for the next ones', () => {
  const store = new Store(':memory:');
  const u = store.createGuest('A', 0);
  const now = Date.parse('2026-11-07T10:00:00Z');
  const st = () => store.wheelState(store.get(u.id)!, now);
  assert.deepEqual([wheelCost(1), wheelCost(2), wheelCost(3), wheelCost(4), wheelCost(5)], [0, 1, 3, 4, 5]);
  assert.equal(st().ready, true, 'the free spin');
  const before = store.coinsOf(u.id);
  const r = store.wheelSpin(u.id, now, () => 0)!;
  assert.equal(r.index, 0); assert.equal(r.coins, WHEEL.segments[0].coins);
  assert.equal(store.coinsOf(u.id), before + r.coins);
  assert.equal(st().ready, false); assert.equal(st().cost, 1);
  assert.equal(store.wheelSpin(u.id, now), null, 'no second free spin');
  assert.equal(store.wheelAd(u.id, now), true);
  assert.equal(store.wheelAd(u.id, now), false, 'no more ads than the spin needs');
  assert.equal(st().ready, true);
  store.wheelSpin(u.id, now, () => 0.999);
  assert.equal(st().cost, 3); assert.equal(st().ads, 0);
  for (let i = 0; i < 2; i++) store.wheelAd(u.id, now);
  assert.equal(st().ready, false);
  store.wheelAd(u.id, now);
  assert.equal(st().ready, true);
  // a new day: the free spin again
  const tomorrow = now + 86_400_000;
  assert.equal(store.wheelState(store.get(u.id)!, tomorrow).ready, true);
  assert.equal(store.wheelState(store.get(u.id)!, tomorrow).spins, 0);
});
