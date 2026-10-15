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
  const hub = new Hub(store, tokens, { phones, google, defaultCountry: opts.login.defaultCountry, adsSsv: opts.adsSsv, turnMs: opts.turnMs, botFillMs: opts.botFillMs, nextRoundMs: opts.nextRoundMs, botDelay: opts.botDelay, lobbyAfterMatchMs: opts.lobbyAfterMatchMs, msgsPer5s: (opts as { msgsPer5s?: number }).msgsPer5s,
    stunUrls: opts.stunUrls, turnUrls: opts.turnUrls, turnSecret: opts.turnSecret, autoMoveMs: opts.autoMoveMs, chessClockMs: opts.chessClockMs });
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
    close: async () => {
      clearInterval(heartbeat); hub.stop();
      wss.clients.forEach(c => c.terminate());
      await new Promise(r => server.close(r)); store.close();
    },
  };
}

if (require.main === module) {
  const app = createServer();
  app.listen().then(() => console.log(`Domino server listening on http://${config.host}:${config.port}`));
  const shutdown = () => { console.log('Shutting down…'); app.close().then(() => process.exit(0)); setTimeout(() => process.exit(1), 5000); };
  process.on('SIGTERM', shutdown); process.on('SIGINT', shutdown);
}
