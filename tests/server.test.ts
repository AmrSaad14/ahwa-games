import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import WebSocket from 'ws';
import { createServer } from '../server/index';
import { config } from '../server/config';
import { chooseMove } from '../shared/bot';
import type { S2C, C2S, TableView, BoardView } from '../shared/protocol';

let app: ReturnType<typeof createServer>;
let port = 0;

before(async () => {
  app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent',
    turnMs: 3000, botFillMs: 300, nextRoundMs: 30, botDelay: [5, 10], lobbyAfterMatchMs: 200, msgsPer5s: 10000, chessClockMs: 15_000, autoMoveMs: 30 } as typeof config);
  port = await app.listen();
});
after(async () => { await app.close(); });

/** A scripted player that keeps every message and can auto-play its turns. */
class Client {
  ws!: WebSocket; msgs: S2C[] = []; view: TableView | null = null; board: BoardView | null = null; token = ''; uid = ''; autoplay = false; waiters: (() => void)[] = [];
  async open(token?: string) {
    this.ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
    await new Promise<void>((r, j) => { this.ws.once('open', () => r()); this.ws.once('error', j); });
    this.ws.on('message', d => {
      const m = JSON.parse(d.toString()) as S2C; this.msgs.push(m);
      if (m.t === 'welcome') { this.token = m.token; this.uid = m.user.id; }
      if (m.t === 'table') { if (m.view.game === 'domino') this.view = m.view; else this.board = m.view; if (this.autoplay) this.play(); }
      if (m.t === 'left') this.view = null;
      if (m.t === 'err' && process.env.DEBUG) console.log('err', m.code);
      this.waiters.splice(0).forEach(w => w());
    });
    this.send({ t: 'hello', token });
    await this.until(() => !!this.uid && this.msgs.some(m => m.t === 'welcome'));
    return this;
  }
  send(m: C2S) { this.ws.send(JSON.stringify(m)); }
  async until(fn: () => boolean, ms = 15000) {
    const end = Date.now() + ms;
    while (!fn()) {
      if (Date.now() > end) throw new Error('timed out waiting\n' + this.dump());
      await new Promise<void>(r => { this.waiters.push(r); setTimeout(r, 50); });
    }
  }
  dump() { return this.msgs.filter(m => m.t !== "table").map(m => JSON.stringify(m).slice(0, 120)).slice(-8).join("\n") + "\nVIEW " + JSON.stringify(this.view && { phase: this.view.phase, turn: this.view.turn, you: this.view.you, hand: this.view.hand, yard: this.view.yard, last: this.view.last }); }
  last<T extends S2C['t']>(t: T) { return [...this.msgs].reverse().find(m => m.t === t) as Extract<S2C, { t: T }> | undefined; }
  play() {
    const b = this.board;
    if (b && b.phase === 'play' && b.turn === b.you) {
      // play the first legal option, like an impatient player
      if (b.chess && b.chess.legal.length) { const [from, to] = b.chess.legal[0]; return this.send({ t: 'act', a: { t: 'move', from, to } }); }
      if (b.tawla) { if (!b.tawla.rolled) return this.send({ t: 'act', a: { t: 'roll' } }); const st = b.tawla.steps[0]; if (st) return this.send({ t: 'act', a: { t: 'step', from: st[0], to: st[1] } }); return; }
      if (b.ludo) { if (b.ludo.die === null) return this.send({ t: 'act', a: { t: 'roll' } }); if (b.ludo.movable.length) return this.send({ t: 'act', a: { t: 'pawn', i: b.ludo.movable[0] } }); return; }
    }
    const v = this.view; if (!v || v.phase !== 'play' || v.turn !== v.you) return;
    if (v.open !== null && v.hand.includes(v.open)) return this.send({ t: 'act', a: { t: 'play', id: v.open, e: 'C' } });   // the heaviest double opens
    const m = chooseMove(v.line, v.hand, v.yard);
    this.send({ t: 'act', a: m.t === 'play' ? { t: 'play', id: m.id, e: m.e } : m });
  }
  close() { this.ws.close(); }
}

test('guests get a token and come back as the same player', async () => {
  const a = await new Client().open();
  const uid = a.uid, token = a.token; a.close();
  const b = await new Client().open(token);
  assert.equal(b.uid, uid);
  b.close();
});

test('register, then log in from another device', async () => {
  const a = await new Client().open();
  a.send({ t: 'profile', name: 'Amr <b>', ch: 3 });
  a.send({ t: 'register', email: 'Amr@Example.com', pw: 'secret123' });
  await a.until(() => a.last('user')?.user.email === 'amr@example.com');
  assert.equal(a.last('user')!.user.name, 'Amr b');
  const b = await new Client().open();
  b.send({ t: 'login', email: 'amr@example.com', pw: 'wrong' });
  await b.until(() => b.last('err')?.code === 'bad_login');
  b.send({ t: 'login', email: 'amr@example.com', pw: 'secret123' });
  await b.until(() => b.uid === a.uid);
  a.close(); b.close();
});

test('a player can delete their account', async () => {
  const a = await new Client().open();
  a.send({ t: 'register', email: 'gone@example.com', pw: 'secret123' });
  await a.until(() => a.last('user')?.user.email === 'gone@example.com');
  const old = a.uid;
  a.send({ t: 'delete' });
  await a.until(() => a.uid !== old);
  const b = await new Client().open();
  b.send({ t: 'login', email: 'gone@example.com', pw: 'secret123' });
  await b.until(() => b.last('err')?.code === 'bad_login');
  a.close(); b.close();
});

test('private room: create, join by code, play a full match with hidden hands', async () => {
  const host = await new Client().open(), guest = await new Client().open();
  host.send({ t: 'create', mode: 'ffa2', target: 51 });
  await host.until(() => !!host.view?.code);
  const code = host.view!.code!;
  guest.send({ t: 'join', code: code.toUpperCase() });
  await guest.until(() => guest.view?.seats.filter(Boolean).length === 2);
  guest.send({ t: 'start' });
  await guest.until(() => guest.last('err')?.code === 'not_host');
  host.autoplay = guest.autoplay = true;
  host.send({ t: 'start' });
  await host.until(() => host.view?.phase === 'play');
  // each player only ever sees their own tiles
  const hv = host.view!, gv = guest.view!;
  assert.equal(hv.hand.length + gv.hand.length + hv.yard + (hv.line.f === null ? 0 : 1 + hv.line.L.length + hv.line.R.length) <= 28, true);
  assert.ok(!hv.hand.some(id => gv.hand.includes(id)), 'hands must not overlap');
  host.play(); guest.play();
  await host.until(() => host.view?.phase === 'mend', 60000);
  assert.ok(Math.max(...host.view!.match!.scores) >= 51);
  // private rooms return to the room afterwards
  await host.until(() => host.view?.phase === 'lobby', 5000);
  host.close(); guest.close();
});

test('quick match fills empty seats with computer players and updates rating', async () => {
  const p = await new Client().open();
  const before = p.last('welcome')!.user.rating;
  p.autoplay = true;
  p.send({ t: 'quick', mode: 'ffa4' });
  await p.until(() => p.last('queue')?.mode === 'ffa4');
  await p.until(() => p.view?.phase === 'play');
  assert.equal(p.view!.seats.filter(s => s?.bot).length, 3);
  p.play();
  await p.until(() => p.view?.phase === 'mend', 90000);
  const delta = p.view!.match!.deltas[p.view!.you];
  assert.equal(typeof delta, 'number');
  await p.until(() => p.last('user')?.user.games === 1);
  assert.equal(p.last('user')!.user.rating, Math.max(100, before + delta!));
  p.close();
});

test('two people searching together are matched against each other', async () => {
  const a = await new Client().open(), b = await new Client().open();
  a.send({ t: 'quick', mode: 'ffa2' }); b.send({ t: 'quick', mode: 'ffa2' });
  await a.until(() => a.view?.phase === 'play'); await b.until(() => b.view?.phase === 'play');
  assert.equal(a.view!.id, b.view!.id);
  assert.equal(a.view!.seats.filter(s => s?.bot).length, 0);
  a.send({ t: 'emote', k: 'gg' });
  await b.until(() => b.last('emote')?.k === 'gg');
  // b drops and comes back: still at the same table
  const token = b.token; b.close();
  await new Promise(r => setTimeout(r, 100));
  const b2 = await new Client().open(token);
  await b2.until(() => b2.view?.id === a.view!.id);
  b2.send({ t: 'leave' }); a.send({ t: 'leave' });
  await a.until(() => a.last('left') !== undefined);
  a.close(); b2.close();
});

test('team game (2 vs 2) runs to the end', async () => {
  const p = await new Client().open(); p.autoplay = true;
  p.send({ t: 'quick', mode: 'team' });
  await p.until(() => p.view?.phase === 'play');
  assert.equal(p.view!.scores.length, 2);
  p.play();
  await p.until(() => p.view?.phase === 'mend', 90000);
  p.close();
});

test('daily bonus, gifts between players, and coins', async () => {
  const a = await new Client().open(), b = await new Client().open();
  assert.equal(a.last('bonus')?.coins, 100, 'daily bonus on first visit');
  const start = a.last('welcome')!.user.coins;
  assert.equal(start, 600);
  // a second visit the same day gives no bonus
  const a2 = await new Client().open(a.token);
  assert.equal(a2.last('bonus'), undefined);
  a2.close();
  await new Promise(r => setTimeout(r, 100));
  const a3 = await new Client().open(a.token);
  a.close();
  a3.send({ t: 'create', mode: 'ffa3', target: 51 });
  await a3.until(() => !!a3.view?.code);
  b.send({ t: 'join', code: a3.view!.code! });
  await b.until(() => b.view?.seats.filter(Boolean).length === 2);
  a3.send({ t: 'bot', seat: 2 });
  await a3.until(() => !!a3.view?.seats[2]);
  const bSeat = b.view!.you;
  a3.send({ t: 'gift', to: bSeat, k: 'rose' });
  await b.until(() => b.last('gift')?.k === 'rose');
  assert.equal(b.last('gift')!.from, a3.view!.you);
  await a3.until(() => a3.last('user')?.user.coins === start - 30);
  await b.until(() => b.last('user')?.user.gifts === 1);
  // gifts to computer players are refused, and so are gifts you can't afford
  await new Promise(r => setTimeout(r, 1300));
  a3.send({ t: 'gift', to: 2, k: 'tea' });
  await a3.until(() => a3.last('err')?.code === 'gift_bot');
  a3.send({ t: 'gift', to: bSeat, k: 'diamond' });
  await a3.until(() => a3.last('user')?.user.coins === start - 530);
  await new Promise(r => setTimeout(r, 1300));
  a3.send({ t: 'gift', to: bSeat, k: 'diamond' });
  await a3.until(() => a3.last('err')?.code === 'not_enough_coins');
  // reporting another player is accepted
  a3.send({ t: 'report', seat: bSeat, reason: 'abuse' });
  await new Promise(r => setTimeout(r, 200));
  assert.equal((app.store.reports() as unknown[]).length, 1);
  a3.close(); b.close();
});

test('voice: offers only pass between players who joined voice at the same table', async () => {
  const a = await new Client().open(), b = await new Client().open();
  a.send({ t: 'create', mode: 'ffa2', target: 51 });
  await a.until(() => !!a.view?.code);
  b.send({ t: 'join', code: a.view!.code! });
  await a.until(() => a.view?.seats.filter(Boolean).length === 2);
  await b.until(() => !!b.view);
  const sig = { type: 'offer' as const, sdp: 'v=0 fake', table: '' };
  a.send({ t: 'rtc', to: b.view!.you, data: sig });
  await new Promise(r => setTimeout(r, 300));
  assert.equal(b.last('rtc'), undefined, 'not relayed before both joined voice');
  a.send({ t: 'voice', on: true }); b.send({ t: 'voice', on: true });
  await a.until(() => !!a.view?.seats.every(s => s?.mic));
  a.send({ t: 'rtc', to: b.view!.you, data: sig });
  await b.until(() => b.last('rtc')?.data.sdp === 'v=0 fake');
  assert.equal(b.last('rtc')!.from, a.view!.you);
  assert.ok(Array.isArray(a.last('welcome')!.ice) && a.last('welcome')!.ice.length >= 1, 'ICE servers are provided');
  // leaving voice is visible to the other player
  b.send({ t: 'voice', on: false });
  await a.until(() => a.view?.seats[b.view!.you]?.mic === false);
  a.close(); b.close();
});

test('tawla, ludo and chess play to the end online, with ratings kept per game', async () => {
  // tawla against the computer (quick match fills the empty seat)
  const p = await new Client().open(); p.autoplay = true;
  p.send({ t: 'quick', mode: 'tawla' });
  await p.until(() => p.board?.game === 'tawla' && p.board.phase === 'play');
  p.play();
  await p.until(() => p.board?.phase === 'mend', 120000);
  assert.ok(Math.max(...p.board!.match!.scores) >= 3, 'quick tawla plays a set to 3');
  await p.until(() => p.last('user')?.user.ratings.tawla?.games === 1);
  p.send({ t: 'leave' }); await p.until(() => p.last('left') !== undefined);
  // ludo for four: one person and three computer players
  p.send({ t: 'quick', mode: 'ludo4' });
  await p.until(() => p.board?.game === 'ludo' && p.board.phase === 'play');
  assert.equal(p.board!.seats.filter(s => s?.bot).length, 3);
  p.play();
  await p.until(() => p.board?.phase === 'mend', 120000);
  await p.until(() => p.last('user')?.user.ratings.ludo?.games === 1);
  p.send({ t: 'lb', game: 'ludo' });
  await p.until(() => p.last('lb')?.game === 'ludo');
  p.close();
  // chess between two people in a private room
  const a = await new Client().open(), b = await new Client().open();
  a.autoplay = b.autoplay = true;
  a.send({ t: 'create', mode: 'chess', target: 1 });
  await a.until(() => !!a.board?.code);
  b.send({ t: 'join', code: a.board!.code! });
  await a.until(() => a.board?.seats.filter(Boolean).length === 2);
  a.send({ t: 'start' });
  await a.until(() => a.board?.phase === 'play');
  await b.until(() => b.board?.phase === 'play');
  assert.ok(a.board!.clocks && a.board!.clocks[0] > 0, 'chess has clocks');
  a.play(); b.play();
  await a.until(() => a.board?.phase === 'mend' || a.board?.phase === 'lobby', 60000);   // 15-second clocks in tests
  a.close(); b.close();
});

test('leaderboard and health endpoints answer', async () => {
  const c = await new Client().open();
  c.send({ t: 'lb' });
  await c.until(() => !!c.last('lb'));
  const res = await fetch(`http://127.0.0.1:${port}/healthz`);
  assert.equal(res.status, 200);
  assert.equal((await res.json()).ok, true);
  c.close();
});
