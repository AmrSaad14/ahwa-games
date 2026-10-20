import http from 'node:http';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { WebSocketServer, WebSocket } from 'ws';
import { config } from './config';
import { Store } from './db';
import { Tokens } from './auth';
import { Hub } from './hub';
import { GoogleSignIn, PhoneCodes, backToAppPage } from './login';
import { verifySsv } from './ads';
import { GitHubBackup } from './backup';
import { PlayBilling } from './billing';
import { Push } from './push';
import { cashoutPage } from './admin';
import type { S2C } from '../shared/protocol';

const MIME: Record<string, string> = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json', '.webmanifest': 'application/manifest+json', '.svg': 'image/svg+xml', '.png': 'image/png', '.mp3': 'audio/mpeg', '.m4a': 'audio/mp4', '.ogg': 'audio/ogg', '.wav': 'audio/wav',
  '.ico': 'image/x-icon', '.woff2': 'font/woff2', '.txt': 'text/plain; charset=utf-8', '.map': 'application/json',
};

export function createServer(opts = config) {
  const store = new Store(opts.dbPath);
  const tokens = new Tokens(store.secret(opts.secret));
  const phones = new PhoneCodes(opts.login), google = new GoogleSignIn(opts.login);
  const billing = new PlayBilling(opts.billing);
  const hub = new Hub(store, tokens, { phones, google, billing, defaultCountry: opts.login.defaultCountry, adsSsv: opts.adsSsv, turnMs: opts.turnMs, botFillMs: opts.botFillMs, nextRoundMs: opts.nextRoundMs, botDelay: opts.botDelay, lobbyAfterMatchMs: opts.lobbyAfterMatchMs, msgsPer5s: (opts as { msgsPer5s?: number }).msgsPer5s,
    stunUrls: opts.stunUrls, turnUrls: opts.turnUrls, turnSecret: opts.turnSecret, autoMoveMs: opts.autoMoveMs, chessClockMs: opts.chessClockMs });
  const push = new Push(store, opts.push);
  const pushTimer = setInterval(() => { push.tick().catch(e => console.error('[push]', (e as Error).message)); }, 60_000);
  const staticRoot = path.resolve(opts.staticDir);
  const perIp = new Map<string, number>();

  const clientIp = (req: http.IncomingMessage) => {
    const fwd = opts.trustProxy ? String(req.headers['x-forwarded-for'] || '').split(',')[0].trim() : '';
    return fwd || req.socket.remoteAddress || 'unknown';
  };

  const server = http.createServer((req, res) => {
    const url = new URL(req.url || '/', 'http://x');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Referrer-Policy', 'no-referrer');
    if (url.pathname === '/healthz') {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      return res.end(JSON.stringify({ ok: true, ...hub.stats() }));
    }
    if (url.pathname === '/api/leaderboard') {
      res.writeHead(200, { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*', 'Cache-Control': 'max-age=30' });
      const g = url.searchParams.get('game');
      return res.end(JSON.stringify(store.leaderboard(null, (['domino', 'tawla', 'chess', 'ludo'] as const).find(x => x === g) ?? 'domino', 50)));
    }
    if (url.pathname === '/api/admin/reports') {
      const key = url.searchParams.get('key') || '';
      const ok = !!opts.adminKey && key.length === opts.adminKey.length && crypto.timingSafeEqual(Buffer.from(key), Buffer.from(opts.adminKey));
      res.writeHead(ok ? 200 : 403, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
      return res.end(JSON.stringify(ok ? { reports: store.reports(200) } : { error: 'forbidden' }));
    }
    const isAdmin = (key: string) => !!opts.adminKey && key.length === opts.adminKey.length && crypto.timingSafeEqual(Buffer.from(key), Buffer.from(opts.adminKey));
    // the reminders: a free cron service can call this to wake a sleeping server (see server/push.ts)
    if (url.pathname === '/api/cron/push') {
      if (!isAdmin(url.searchParams.get('key') || '')) { res.writeHead(403); return res.end(); }
      const force = url.searchParams.get('now') === '1';
      push.tick(new Date(), force)
        .then(r => { res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }); res.end(JSON.stringify({ enabled: push.enabled, ...r })); })
        .catch(e => { res.writeHead(500, { 'Content-Type': 'text/plain' }); res.end((e as Error).message); });
      return;
    }
    // cash-out requests: a page to see them and mark them paid (or turn them down: the coins go back)
    if (url.pathname === '/admin/cashouts') {
      if (req.method === 'POST') {
        let raw = '';
        req.on('data', c => { raw += c; if (raw.length > 4096) req.destroy(); });
        req.on('end', () => {
          const f = new URLSearchParams(raw);
          const key = f.get('key') || '';
          if (!isAdmin(key)) { res.writeHead(403); return res.end(); }
          const id = Number(f.get('id')), paid = f.get('action') === 'paid';
          const c = store.settleCashout(id, paid, f.get('note') || '');
          if (c) {
            console.log(`[cashout] #${id} ${paid ? 'paid' : 'rejected'}`);
            hub.refreshPlayer(c.user_id);
            push.toUser(c.user_id,
              paid ? ['تم التحويل 💸', `اتحولك ${c.egp} جنيه على فودافون كاش`] : ['طلب السحب اترفض', 'رجعنالك الكوينز، كلمنا لو محتاج مساعدة'],
              paid ? ['Paid 💸', `${c.egp} EGP was sent to your Vodafone Cash`] : ['Cash-out declined', 'Your coins are back in your balance']).catch(() => {});
          }
          res.writeHead(303, { Location: `/admin/cashouts?key=${encodeURIComponent(key)}`, 'Cache-Control': 'no-store' });
          res.end();
        });
        return;
      }
      const key = url.searchParams.get('key') || '';
      if (!isAdmin(key)) { res.writeHead(403, { 'Content-Type': 'text/plain' }); return res.end('forbidden'); }
      const status = url.searchParams.get('status');
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store', 'X-Robots-Tag': 'noindex', 'Referrer-Policy': 'no-referrer' });
      return res.end(cashoutPage(store.cashoutsAll(status === 'all' ? null : status || 'pending'), key, status || 'pending', push.enabled));
    }
    // AdMob server-side verification: Google calls this after a player watched a rewarded ad
    if (url.pathname === '/ads/ssv') {
      const raw = (req.url || '').split('?')[1] || '';
      verifySsv(raw).then(q => {
        if (!q) { res.writeHead(403); return res.end(); }
        const uid = q.get('user_id'), ticket = q.get('custom_data');
        if (uid && ticket) hub.rewardAd(uid, ticket);
        res.writeHead(200); res.end('ok');   // AdMob retries anything but 200, including its own "verify URL" test
      });
      return;
    }
    // Google sign-in: send the player to Google, then back to the game with a one-time ticket
    if (url.pathname === '/auth/google/start') {
      if (!google.enabled) { res.writeHead(404, { 'Content-Type': 'text/plain' }); return res.end('Google sign-in is not set up on this server.'); }
      res.writeHead(302, { Location: google.startUrl(url.searchParams.get('to') === 'app' ? 'app' : 'web'), 'Cache-Control': 'no-store' });
      return res.end();
    }
    if (url.pathname === '/auth/google/callback') {
      const code = url.searchParams.get('code'), state = url.searchParams.get('state') || '';
      const page = (link: string, ok: boolean) => { res.writeHead(ok ? 200 : 400, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' }); res.end(backToAppPage(link, opts.login.appName, ok)); };
      if (!code) return page('', false);
      google.callback(code, state)
        .then(({ to, ticket }) => {
          if (to === 'web') { res.writeHead(302, { Location: google.webLink(ticket), 'Cache-Control': 'no-store' }); return res.end(); }
          page(google.appLink(ticket), true);
        })
        .catch(e => { console.error('[google]', (e as Error).message); page('', false); });
      return;
    }
    if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405); return res.end(); }
    // static client
    let file = path.normalize(path.join(staticRoot, decodeURIComponent(url.pathname)));
    if (!file.startsWith(staticRoot)) { res.writeHead(403); return res.end(); }
    if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(staticRoot, 'index.html');
    if (!fs.existsSync(file)) {
      res.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' });
      return res.end('Domino server is running. Build the client (npm run build) to serve the game here.');
    }
    const ext = path.extname(file);
    const immutable = url.pathname.startsWith('/assets/');
    res.writeHead(200, {
      'Content-Type': MIME[ext] || 'application/octet-stream',
      'Cache-Control': immutable ? 'public, max-age=31536000, immutable' : 'no-cache',
    });
    if (req.method === 'HEAD') return res.end();
    fs.createReadStream(file).pipe(res);
  });

  const wss = new WebSocketServer({ noServer: true, maxPayload: 48 * 1024 });
  server.on('upgrade', (req, socket, head) => {
    const url = new URL(req.url || '/', 'http://x');
    if (url.pathname !== '/ws') return socket.destroy();
    const ip = clientIp(req);
    if ((perIp.get(ip) || 0) >= opts.maxConnectionsPerIp) return socket.destroy();
    wss.handleUpgrade(req, socket, head, ws => wss.emit('connection', ws, req, ip));
  });

  wss.on('connection', (ws: WebSocket & { alive?: boolean }, _req: http.IncomingMessage, ip: string) => {
    perIp.set(ip, (perIp.get(ip) || 0) + 1);
    ws.alive = true;
    const session = hub.connect({
      ip,
      send: (m: S2C) => { if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(m)); },
      close: (code, reason) => ws.close(code, reason),
    });
    ws.on('pong', () => { ws.alive = true; });
    ws.on('message', (data: Buffer, isBinary: boolean) => { if (!isBinary) hub.handle(session, data.toString()); });
    ws.on('close', () => {
      hub.disconnect(session);
      const n = (perIp.get(ip) || 1) - 1; if (n <= 0) perIp.delete(ip); else perIp.set(ip, n);
    });
    ws.on('error', () => ws.terminate());
  });

  const heartbeat = setInterval(() => {
    wss.clients.forEach((ws: WebSocket & { alive?: boolean }) => {
      if (!ws.alive) return ws.terminate();
      ws.alive = false; ws.ping();
    });
  }, 25_000);

  return {
    server, hub, store, phones, google,
    listen: () => new Promise<number>(r => server.listen(opts.port, opts.host, () => r((server.address() as { port: number }).port))),
    /** Stop the game (stakes in play are refunded); `last` runs just before the database closes (final backup). */
    close: async (last?: () => Promise<void>) => {
      clearInterval(heartbeat); clearInterval(pushTimer); hub.stop();
      wss.clients.forEach(c => c.terminate());
      await new Promise(r => server.close(r));
      if (last) await last().catch(e => console.error('[backup]', (e as Error).message));
      store.close();
    },
  };
}

async function main() {
  const b = config.backup;
  const backup = b.repo && b.token ? new GitHubBackup(b.repo, b.token, b.api ? { api: b.api, uploads: b.api } : {}) : null;
  // a fresh disk (free hosts after a restart): bring the players back first. If GitHub can't be reached, play
  // on but never upload, so an empty database can't replace the real one.
  let canSave = !!backup;
  if (backup && !fs.existsSync(config.dbPath)) {
    let ok = false;
    for (let i = 0; i < 4 && !ok; i++) {
      try { await backup.restore(config.dbPath); ok = true; }
      catch (e) { console.error('[backup] restore failed:', (e as Error).message); await new Promise(r => setTimeout(r, 3000 * (i + 1))); }
    }
    if (!ok) { canSave = false; console.error('[backup] could not restore: running WITHOUT backups this time'); }
  }
  const app = createServer();
  await app.listen();
  console.log(`Board Battles server listening on http://${config.host}:${config.port}${backup ? (canSave ? ' (backups on)' : ' (backups OFF)') : ''}`);

  let savedAt = -1, saving: Promise<void> | null = null;
  const save = async (force = false) => {
    if (!backup || !canSave) return;
    const n = app.store.changes();
    if (!force && n === savedAt) return;
    const tmp = config.dbPath + '.snapshot';
    app.store.snapshot(tmp);
    await backup.save(tmp);
    fs.rmSync(tmp, { force: true });
    savedAt = n;
  };
  const tick = () => { if (!saving) saving = save().catch(e => console.error('[backup] save failed:', (e as Error).message)).finally(() => { saving = null; }); };
  if (backup && canSave) setInterval(tick, Math.max(1, b.minutes) * 60_000);

  let stopping = false;
  const shutdown = () => {
    if (stopping) return; stopping = true;
    console.log('Shutting down…');
    setTimeout(() => process.exit(1), backup ? 25_000 : 5000);
    app.close(async () => { await saving; if (app.store.changes() !== savedAt) await save(true); }).then(() => process.exit(0));
  };
  process.on('SIGTERM', shutdown); process.on('SIGINT', shutdown);
}

if (require.main === module) main();
