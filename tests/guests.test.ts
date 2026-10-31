// Online play needs a signed-in account (guestsOnline off, as in production); Google's picture becomes the photo.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import WebSocket from 'ws';
import { createServer } from '../server/index';
import { config } from '../server/config';

let app: ReturnType<typeof createServer>;
let port = 0;
before(async () => {
  app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent', botFillMs: 300, msgsPer5s: 10000,
    guestsOnline: false, login: { ...config.login, sms: 'console' } } as typeof config);
  port = await app.listen();
});
after(async () => { await app.close(); });

async function client() {
  const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
  const msgs: any[] = [];
  ws.on('message', d => msgs.push(JSON.parse(d.toString())));
  await new Promise<void>((r, j) => { ws.once('open', () => r()); ws.once('error', j); });
  ws.send(JSON.stringify({ t: 'hello' }));
  const until = async (fn: () => boolean) => { const end = Date.now() + 8000; while (!fn()) { if (Date.now() > end) throw new Error('timed out: ' + JSON.stringify(msgs.slice(-4)).slice(0, 400)); await new Promise(r => setTimeout(r, 30)); } };
  await until(() => msgs.some(m => m.t === 'welcome'));
  const last = (t: string) => [...msgs].reverse().find(m => m.t === t);
  return { ws, msgs, until, last, send: (m: unknown) => ws.send(JSON.stringify(m)) };
}

test('a guest is asked to sign in before playing online; signed in with Google, they play and have their photo', async () => {
  const a = await client();
  for (const t of [{ t: 'quick', mode: 'ffa2', stake: 5000 }, { t: 'create', mode: 'chess' }, { t: 'join', code: 'abcd' }, { t: 'tourneyJoin' }]) {
    a.msgs.length = 0; a.send(t);
    await a.until(() => a.msgs.some(m => m.t === 'err'));
    assert.equal(a.last('err').code, 'sign_in_required', t.t);
  }
  a.send({ t: 'ticket', ticket: app.google.testTicket({ sub: 'g-photo', email: 'p@example.com', name: 'Mona', picture: 'https://lh3.googleusercontent.com/a/photo' }) });
  await a.until(() => a.msgs.some(m => m.t === 'signedIn'));
  await a.until(() => a.last('user')?.user.photo === 'https://lh3.googleusercontent.com/a/photo');
  a.msgs.length = 0; a.send({ t: 'quick', mode: 'chess', stake: 5000 });
  await a.until(() => a.msgs.some(m => m.t === 'queue' || m.t === 'table'));
  await a.until(() => !!a.last('table'));
  const me = a.last('table').view.seats[a.last('table').view.you];
  assert.equal(me.photo, 'https://lh3.googleusercontent.com/a/photo', 'the photo shows at the table');
  a.ws.close();
});

test('a picture that is not https is not kept', () => {
  const u = app.store.createGuest('Test', 0);
  app.store.setPhoto(u.id, 'http://evil.example/x.png');
  assert.equal(app.store.get(u.id)!.photo, null);
});
