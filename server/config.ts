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
  // Rewarded ads: ADS_SSV=1 once AdMob's server-side verification calls PUBLIC_URL/ads/ssv (README → Ads)
  adsSsv: process.env.ADS_SSV === '1',
  // Sign-in (see README → Sign-in with Google and a mobile number)
  login: {
    publicUrl: process.env.PUBLIC_URL || '',                // https address players reach this server on
    appScheme: process.env.APP_SCHEME || 'ahwagames',
    appName: process.env.APP_NAME || 'Ahwa Games',
    googleClientId: process.env.GOOGLE_CLIENT_ID || '',
    googleClientSecret: process.env.GOOGLE_CLIENT_SECRET || '',
    sms: (process.env.SMS_PROVIDER || '') as 'twilio' | 'http' | 'console' | '',
    twilioSid: process.env.TWILIO_ACCOUNT_SID || '',
    twilioToken: process.env.TWILIO_AUTH_TOKEN || '',
    twilioVerifySid: process.env.TWILIO_VERIFY_SID || '',
    smsHttpUrl: process.env.SMS_HTTP_URL || '',
    smsHttpToken: process.env.SMS_HTTP_TOKEN || '',
    defaultCountry: process.env.DEFAULT_COUNTRY_CODE || '20',
  },
};
