import path from 'node:path';

const num = (v: string | undefined, d: number) => (v && !Number.isNaN(Number(v)) ? Number(v) : d);

export const config = {
  port: num(process.env.PORT, 8080),
  host: process.env.HOST || '0.0.0.0',
  dbPath: process.env.DB_PATH || path.resolve('data/domino.db'),
  staticDir: process.env.STATIC_DIR || path.resolve('client/dist'),
  secret: process.env.SECRET || '',                       // generated and stored in the database when empty
  turnMs: num(process.env.TURN_SECONDS, 10) * 1000,
  botFillMs: num(process.env.BOT_FILL_SECONDS, 12) * 1000,
  nextRoundMs: num(process.env.NEXT_ROUND_SECONDS, 7) * 1000,
  botDelay: [900, 1700] as [number, number],
  // chess: each mode has its own clock (1, 3, 5 or 10 minutes); CHESS_MINUTES forces one length for all of them
  chessClockMs: process.env.CHESS_MINUTES ? num(process.env.CHESS_MINUTES, 5) * 60_000 : undefined,
  autoMoveMs: 900,
  lobbyAfterMatchMs: 25_000,
  // Voice chat: STUN finds a direct route between phones; TURN relays audio when a direct route is blocked (common on mobile data).
  stunUrls: (process.env.STUN_URLS || 'stun:stun.l.google.com:19302,stun:stun1.l.google.com:19302').split(',').map(s => s.trim()).filter(Boolean),
  turnUrls: (process.env.TURN_URLS || '').split(',').map(s => s.trim()).filter(Boolean),
  turnSecret: process.env.TURN_SECRET || '',
  adminKey: process.env.ADMIN_KEY || '',                  // lets you read player reports at /api/admin/reports?key=…
  trustProxy: process.env.TRUST_PROXY === '1',             // read client IPs from X-Forwarded-For (behind Caddy/nginx)
  maxConnectionsPerIp: num(process.env.MAX_CONNECTIONS_PER_IP, 20),
};
