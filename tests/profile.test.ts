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

test('leagues: points per game and season, the reward for the highest league once the season ends', async () => {
  const { leagueOf } = await import('../shared/leagues');
  const s = new Store(':memory:');
  const a = s.createGuest('A', 0);
  const nov = Date.parse('2026-11-10T10:00:00Z'), dec = Date.parse('2026-12-02T10:00:00Z');
  for (let i = 0; i < 12; i++) s.addLeague(a.id, 'tawla', true, nov);   // 360 LP: silver
  s.addLeague(a.id, 'tawla', false, nov);                                // 345: still silver
  assert.equal(s.leaguesOf(a.id, nov).tawla.league, 'silver');
  assert.equal(s.leaguesOf(a.id, nov).tawla.next, 700);
  assert.equal(leagueOf(1800), 4);
  assert.deepEqual(s.leagueRewards(a.id, nov), [], 'nothing before the season ends');
  const before = s.coinsOf(a.id);
  const r = s.leagueRewards(a.id, dec);
  assert.deepEqual(r.map(x => [x.game, x.league, x.coins]), [['tawla', 'silver', 15_000]]);
  assert.equal(s.coinsOf(a.id), before + 15_000);
  assert.deepEqual(s.leagueRewards(a.id, dec), [], 'paid once');
  assert.deepEqual(s.leaguesOf(a.id, dec), {}, 'a new season starts from zero');
});

test('governorates: the weekly board counts the best players, last week\'s winner pays its players once', () => {
  const s = new Store(':memory:');
  const a = s.createGuest('A', 0), b = s.createGuest('B', 1), c = s.createGuest('C', 2);
  s.setGov(a.id, 'alex'); s.setGov(b.id, 'alex'); s.setGov(c.id, 'cairo');
  const now = Date.parse('2026-11-12T10:00:00Z'), lastWeek = now - 7 * 86_400_000;
  const { weekKey } = require('../shared/gifts');
  for (let i = 0; i < 6; i++) { s.addWeekly(a.id, true, weekKey(lastWeek)); s.addWeekly(c.id, false, weekKey(lastWeek)); }
  s.addWeekly(b.id, true, weekKey(lastWeek));
  const board = s.govBoard(weekKey(lastWeek));
  assert.equal(board[0].gov, 'alex'); assert.equal(board[0].points, 21); assert.equal(board[0].players, 2);
  assert.equal(s.govPlayers('alex', a.id, weekKey(lastWeek))[0].me, true);
  const before = s.coinsOf(a.id);
  assert.equal(s.govPrize(a.id, now)?.coins, 10_000);
  assert.equal(s.coinsOf(a.id), before + 10_000);
  assert.equal(s.govPrize(a.id, now), null, 'once');
  assert.equal(s.govPrize(b.id, now), null, 'played too little');
  assert.equal(s.govPrize(c.id, now), null, 'another governorate');
});

test('the whole game leaderboard: most coins won in games, all games together', async () => {
  const st = new Store(':memory:');
  const a = st.createGuest('Aya', 0), b = st.createGuest('Bassem', 1), c = st.createGuest('Camal', 2);
  st.logCoins(a.id, 'win', 500, { mode: 'ffa2' }); st.logCoins(a.id, 'win', 700, { mode: 'basra2' });
  st.logCoins(b.id, 'win', 2000, { mode: 'chess' });
  st.logCoins(c.id, 'loss', -900, { mode: 'tarneeb' });
  const lb = st.coinsBoard(a.id);
  assert.deepEqual(lb.rows.map(r => [r.name, r.rating, r.wins]), [['Bassem', 2000, 1], ['Aya', 1200, 2]]);
  assert.equal(lb.me!.rank, 2); assert.equal(lb.me!.rating, 1200);
  assert.equal(st.coinsBoard(c.id).me, null);
});
