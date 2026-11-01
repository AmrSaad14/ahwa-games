// Walking out of a live two-sided match loses it: the other player wins at once and takes the stake.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import WebSocket from 'ws';
import { createServer } from '../server/index';
import { config } from '../server/config';

let app: ReturnType<typeof createServer>;
let port = 0;
before(async () => {
  app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent', botFillMs: 300, msgsPer5s: 10000,
    guestsOnline: true, login: { ...config.login, sms: 'console' } } as typeof config);
  port = await app.listen();
});
after(async () => { await app.close(); });

async function client() {
  const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
  const msgs: any[] = [];
  ws.on('message', d => msgs.push(JSON.parse(d.toString())));
  await new Promise<void>((r, j) => { ws.once('open', () => r()); ws.once('error', j); });
  ws.send(JSON.stringify({ t: 'hello' }));
  const until = async (fn: () => boolean) => { const end = Date.now() + 8000; while (!fn()) { if (Date.now() > end) throw new Error('timed out: ' + JSON.stringify(msgs.slice(-4)).slice(0, 500)); await new Promise(r => setTimeout(r, 30)); } };
  await until(() => msgs.some(m => m.t === 'welcome'));
  const last = (t: string) => [...msgs].reverse().find(m => m.t === t);
  return { ws, msgs, until, last, send: (m: unknown) => ws.send(JSON.stringify(m)) };
}

for (const mode of ['chess', 'ffa2']) {
  test(`${mode}: leaving mid-match loses the stake and the other player wins it`, async () => {
    const a = await client(), b = await client();
    const coinsA = a.last('welcome').user.coins, coinsB = b.last('welcome').user.coins;
    a.send({ t: 'create', mode, stake: 5000 });
    await a.until(() => !!a.last('table')?.view.code);
    b.send({ t: 'join', code: a.last('table').view.code });
    await a.until(() => a.last('table')?.view.seats.filter(Boolean).length === 2);
    a.send({ t: 'start' });
    await b.until(() => b.last('table')?.view.phase === 'play');
    a.send({ t: 'leave' });
    await a.until(() => a.msgs.some(m => m.t === 'payout' && m.kind === 'left'));
    await b.until(() => b.msgs.some(m => m.t === 'payout' && m.kind === 'win'));
    await b.until(() => b.last('table')?.view.phase === 'mend');
    const win = b.msgs.find(m => m.t === 'payout' && m.kind === 'win');
    assert.ok(win.delta > 0, 'the one who stayed gains coins');
    await a.until(() => a.last('user')?.user.coins === coinsA - 5000);
    await b.until(() => b.last('user')?.user.coins === coinsB + win.delta);
    a.ws.close(); b.ws.close();
  });
}
