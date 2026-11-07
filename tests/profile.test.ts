import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Store } from '../server/db';

test('profile: win streaks per game, head to head, and badges', () => {
  const s = new Store(':memory:');
  const a = s.createGuest('A', 0), b = s.createGuest('B', 1);
  for (const won of [true, true, true, false, true, true]) s.recordResult(a.id, 0, won, 'tawla', false);
  const st = s.playerStats(a.id)!;
  assert.equal(st.perGame.tawla.streak, 2);
  assert.equal(st.perGame.tawla.best, 3);
  const first = st.achievements.find(x => x.id === 'first')!;
  assert.equal(first.done, true);
  assert.equal(st.achievements.find(x => x.id === 'tawla10')!.have, 3);
  // a beat b twice, b beat a once; partners in a team game don't count
  const save = (players: (string | null)[], team: number, mode = 'ffa2') => s.saveMatch({ id: Math.random().toString(), mode, private: false, players: players.map(u => ({ uid: u, bot: !u, rating: 1000 })), scores: [], team });
  save([a.id, b.id], 0); save([b.id, a.id], 1); save([a.id, b.id], 1);
  save([a.id, null, b.id, null], 0, 'team');
  assert.deepEqual(s.headToHead(a.id, b.id), { games: 3, wins: 2, losses: 1 });
  assert.deepEqual(s.headToHead(b.id, a.id), { games: 3, wins: 1, losses: 2 });
});
