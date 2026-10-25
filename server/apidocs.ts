// The API's description: an OpenAPI 3 document (/api/v1/openapi.json, for tools like Postman or Swagger) and a
// readable page (/api/docs) with every endpoint, plus the WebSocket protocol the live games use.

interface Ep { m: 'GET' | 'POST' | 'PATCH' | 'DELETE'; p: string; auth: boolean | 'optional'; tag: string; sum: string; body?: Record<string, string>; query?: Record<string, string> }

export const ENDPOINTS: Ep[] = [
  { m: 'GET', p: '/api/v1', auth: false, tag: 'Server', sum: 'About the API: version, links to the docs and the WebSocket' },
  { m: 'GET', p: '/api/v1/health', auth: false, tag: 'Server', sum: 'Server health: connections, tables, players searching' },
  { m: 'GET', p: '/api/v1/catalog', auth: false, tag: 'Server', sum: 'Everything that can be earned or bought: skins, missions, season tiers, weekly prizes, economy' },
  { m: 'POST', p: '/api/v1/auth/guest', auth: false, tag: 'Sign in', sum: 'A new guest player: returns { token, user }', body: { name: 'string (optional, up to 14 characters)', ch: 'integer 0‥5 (optional): the character' } },
  { m: 'POST', p: '/api/v1/auth/login', auth: false, tag: 'Sign in', sum: 'Sign in with e-mail and password: returns { token, user }', body: { email: 'string', password: 'string' } },
  { m: 'POST', p: '/api/v1/auth/register', auth: true, tag: 'Sign in', sum: 'Add an e-mail and password to this account (so it can be opened elsewhere)', body: { email: 'string', password: 'string (6+)' } },
  { m: 'GET', p: '/api/v1/me', auth: true, tag: 'You', sum: 'Your account: coins, level, XP, streak, daily reward, missions, season, skins, ratings, friend code' },
  { m: 'PATCH', p: '/api/v1/me', auth: true, tag: 'You', sum: 'Change your name or character', body: { name: 'string (optional)', ch: 'integer 0‥5 (optional)' } },
  { m: 'GET', p: '/api/v1/me/card', auth: true, tag: 'You', sum: 'Your own player card (as others see it)' },
  { m: 'GET', p: '/api/v1/me/coins', auth: true, tag: 'You', sum: 'Your coin history (last 100 lines)' },
  { m: 'DELETE', p: '/api/v1/me', auth: true, tag: 'You', sum: 'Delete your account for good' },
  { m: 'GET', p: '/api/v1/players/{pid}', auth: 'optional', tag: 'Players', sum: 'A player\'s card: name, character, level, matches, wins, losses, win rate, coins won/lost, net gains, biggest win, per-game ratings, online; with a token, also your friendship with them' },
  { m: 'GET', p: '/api/v1/leaderboard', auth: 'optional', tag: 'Players', sum: 'The top 50 by rating in a game, or this week\'s points (with a token: your own place too)', query: { game: 'domino | tawla | chess | ludo | week' } },
  { m: 'GET', p: '/api/v1/friends', auth: true, tag: 'Friends', sum: 'Your friend code and your list: friends, requests you sent, requests waiting for you, who is online' },
  { m: 'POST', p: '/api/v1/friends', auth: true, tag: 'Friends', sum: 'Add a friend by their friend code or the pid on their card (a request until they accept)', body: { code: 'string (or)', pid: 'string' } },
  { m: 'POST', p: '/api/v1/friends/{pid}/accept', auth: true, tag: 'Friends', sum: 'Accept a friend request' },
  { m: 'POST', p: '/api/v1/friends/{pid}/decline', auth: true, tag: 'Friends', sum: 'Turn down a friend request' },
  { m: 'DELETE', p: '/api/v1/friends/{pid}', auth: true, tag: 'Friends', sum: 'Remove a friend (or cancel a request)' },
  { m: 'GET', p: '/api/v1/daily', auth: true, tag: 'Rewards', sum: 'The daily reward: today\'s day on the 7-day track, whether it\'s ready, when the next one is' },
  { m: 'POST', p: '/api/v1/daily/claim', auth: true, tag: 'Rewards', sum: 'Collect today\'s reward (once per day, Cairo time)' },
  { m: 'GET', p: '/api/v1/missions', auth: true, tag: 'Rewards', sum: 'Today\'s three missions and how far along they are' },
  { m: 'POST', p: '/api/v1/missions/{id}/claim', auth: true, tag: 'Rewards', sum: 'Collect a finished mission\'s coins and XP' },
  { m: 'GET', p: '/api/v1/season', auth: true, tag: 'Season pass', sum: 'This month\'s season: your points, tier, premium or not, what you collected, every tier\'s rewards' },
  { m: 'POST', p: '/api/v1/season/premium', auth: true, tag: 'Season pass', sum: 'Open the premium track with coins (750K)' },
  { m: 'POST', p: '/api/v1/season/claim', auth: true, tag: 'Season pass', sum: 'Collect a tier\'s reward on the free or premium track', body: { tier: 'integer 1‥30', track: 'free | prem' } },
  { m: 'GET', p: '/api/v1/tournament', auth: 'optional', tag: 'Tournament', sum: 'This week\'s tournament: start time, game, prizes, sign-ups, the bracket, results' },
  { m: 'POST', p: '/api/v1/tournament/join', auth: true, tag: 'Tournament', sum: 'Sign up for this week\'s tournament' },
  { m: 'DELETE', p: '/api/v1/tournament/join', auth: true, tag: 'Tournament', sum: 'Drop out (before it starts)' },
  { m: 'GET', p: '/api/v1/shop', auth: true, tag: 'Shop', sum: 'Every skin with its price, and what you own and wear' },
  { m: 'POST', p: '/api/v1/shop/buy', auth: true, tag: 'Shop', sum: 'Buy a skin with coins (and wear it)', body: { id: 'skin id, e.g. hat_fez_black' } },
  { m: 'POST', p: '/api/v1/shop/equip', auth: true, tag: 'Shop', sum: 'Wear a skin you own (or a free one)', body: { id: 'skin id' } },
  { m: 'POST', p: '/api/v1/referral', auth: true, tag: 'Friends', sum: 'A new player enters the friend code of who invited them; both get 50K after the first game', body: { code: 'string' } },
  { m: 'POST', p: '/api/v1/report', auth: true, tag: 'Players', sum: 'Report a player (three reports in a day mute their chat for a day)', body: { pid: 'string', reason: 'spam | abuse | cheating | other' } },
  { m: 'POST', p: '/api/v1/support', auth: 'optional', tag: 'Server', sum: 'A message to the team (e-mailed to support)', body: { text: 'string', contact: 'string (optional)' } },
];

export function openApi() {
  const paths: Record<string, Record<string, unknown>> = {};
  for (const e of ENDPOINTS) {
    const params = [...(e.p.match(/\{(\w+)\}/g) ?? []).map(x => ({ name: x.slice(1, -1), in: 'path', required: true, schema: { type: 'string' } })),
      ...Object.entries(e.query ?? {}).map(([name, d]) => ({ name, in: 'query', required: false, description: d, schema: { type: 'string' } }))];
    paths[e.p] = paths[e.p] ?? {};
    paths[e.p][e.m.toLowerCase()] = {
      tags: [e.tag], summary: e.sum,
      security: e.auth === true ? [{ bearer: [] }] : e.auth === 'optional' ? [{}, { bearer: [] }] : [],
      parameters: params.length ? params : undefined,
      requestBody: e.body ? { content: { 'application/json': { schema: { type: 'object', properties: Object.fromEntries(Object.entries(e.body).map(([k, d]) => [k, { description: d }])) } } } } : undefined,
      responses: { 200: { description: 'OK (JSON)' }, 400: { description: '{ error }' }, 401: { description: '{ error: "sign_in" }' }, 404: { description: '{ error }' }, 409: { description: '{ error }' }, 429: { description: '{ error: "slow_down" }' } },
    };
  }
  return {
    openapi: '3.0.3',
    info: { title: 'Board Battles API', version: '1', description: 'Accounts, player cards, friends, rewards, season pass, tournament and shop. Live games use the WebSocket at /ws (see /api/docs).' },
    components: { securitySchemes: { bearer: { type: 'http', scheme: 'bearer', description: 'The player\'s token (from POST /api/v1/auth/guest, /auth/login, or the game\'s "welcome" message)' } } },
    paths,
  };
}

const esc = (s: string) => s.replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]!));

const WS = [
  ['hello { token?, v: 2 }', 'welcome { user, token, now, ice }', 'Open the connection (with your token to come back as yourself)'],
  ['quick { mode, stake }', 'queue { … } then table { view }', 'Find a game: players of your level, computer players after 8 s'],
  ['practice { mode }', 'table { view }', 'A game against the computer at once'],
  ['create { mode, target, stake } / join { code }', 'table { view }', 'A room for friends, with a code'],
  ['act { a }', 'table { view }', 'A move: domino play/draw/pass, chess move, tawla roll/step, ludo roll/pawn'],
  ['emote { k }', 'emote { seat, k }', 'Quick chat: hi gg nice lol wow clap fire hurry oops think angry love'],
  ['rematch', 'rematch { seats, need, gone }', 'After a matchmade game: the same people again, when everyone asks'],
  ['profileOf { pid | seat }', 'profileCard { card }', 'A player\'s card'],
  ['friendAdd { pid | code } / friendRemove { pid } / friendAnswer { pid, yes }', 'friends { rows }, friendNews', 'Friends'],
  ['invite { id, mode? }', 'invited { from, code, mode }', 'Invite a friend to your room (one is opened if you have none)'],
  ['daily / mission { id } / seasonClaim { tier, track } / seasonBuy', 'bonus, missionClaimed, seasonClaimed, user', 'Rewards'],
  ['tourney / tourneyJoin / tourneyLeave / tourneyGo', 'tourney { view }, tourneyMatch { table }', 'The weekly tournament'],
  ['leave', 'left', 'Leave the table (a stake in play is lost)'],
];

export function docsPage(): string {
  const tags = [...new Set(ENDPOINTS.map(e => e.tag))];
  const rows = tags.map(t => `<h2>${esc(t)}</h2>` + ENDPOINTS.filter(e => e.tag === t).map(e => `
    <div class="ep"><span class="m ${e.m}">${e.m}</span><code>${esc(e.p)}</code>${e.auth === true ? '<span class="lock">token</span>' : e.auth === 'optional' ? '<span class="lock opt">token optional</span>' : ''}
    <div class="sum">${esc(e.sum)}</div>
    ${e.query ? `<div class="args">query: ${Object.entries(e.query).map(([k, d]) => `<code>${esc(k)}</code> ${esc(d)}`).join(', ')}</div>` : ''}
    ${e.body ? `<div class="args">body: ${Object.entries(e.body).map(([k, d]) => `<code>${esc(k)}</code> ${esc(d)}`).join(' · ')}</div>` : ''}</div>`).join('')).join('');
  const ws = WS.map(([c, s, d]) => `<tr><td><code>${esc(c)}</code></td><td><code>${esc(s)}</code></td><td>${esc(d)}</td></tr>`).join('');
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Board Battles API</title>
<style>:root{color-scheme:light dark;--bg:#fbf7f0;--fg:#22150e;--muted:#6b5a4c;--card:#fff;--line:#eadfce;--gold:#a8741a}
@media (prefers-color-scheme:dark){:root{--bg:#17100b;--fg:#f3e6cf;--muted:#b9a58c;--card:#22170f;--line:#3a2a1e;--gold:#f3cd62}}
body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.5 system-ui,-apple-system,Segoe UI,sans-serif}
main{max-width:920px;margin:0 auto;padding:24px 16px 60px}h1{margin:0 0 4px;color:var(--gold)}h2{margin:28px 0 8px;font-size:18px;border-bottom:1px solid var(--line);padding-bottom:4px}
.lead{color:var(--muted)}code{font:13px ui-monospace,Menlo,monospace;background:var(--card);border:1px solid var(--line);border-radius:5px;padding:1px 5px;word-break:break-word}
.ep{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:10px 12px;margin:8px 0}.sum{margin-top:4px}.args{margin-top:4px;color:var(--muted);font-size:13px}
.m{display:inline-block;min-width:58px;text-align:center;font-weight:700;font-size:12px;border-radius:5px;padding:2px 6px;margin-right:8px;color:#fff}
.GET{background:#2f6b3a}.POST{background:#2b5c9e}.PATCH{background:#8a5a12}.DELETE{background:#9e2b2b}.lock{margin-left:8px;font-size:12px;color:var(--gold)}.lock.opt{opacity:.7}
table{width:100%;border-collapse:collapse;font-size:14px}td{border-bottom:1px solid var(--line);padding:8px 6px;vertical-align:top}pre{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px;overflow:auto;font-size:13px}</style></head>
<body><main><h1>Board Battles API</h1><p class="lead">Accounts, player cards, friends, rewards, the season pass, the weekly tournament and the shop over HTTP; live games over a WebSocket. JSON in and out. Machine-readable: <a href="/api/v1/openapi.json">openapi.json</a>.</p>
<h2>Signing in</h2><p>Send the player's token on every request: <code>Authorization: Bearer &lt;token&gt;</code>. A new player gets one from <code>POST /api/v1/auth/guest</code>; the game gets the same token in its <code>welcome</code> message. Google and Apple sign-in happen in the browser: open <code>/auth/google/start</code> or <code>/auth/apple/start</code> (add <code>?to=app</code> to come back to the app); the player returns with a one-time ticket that the game sends as <code>ticket { ticket }</code> on the WebSocket. Errors come back as <code>{ "error": "code" }</code> with a 4xx status; 240 requests a minute per address.</p>
<pre>curl -X POST https://YOUR-SERVER/api/v1/auth/guest
curl -H "Authorization: Bearer TOKEN" https://YOUR-SERVER/api/v1/me
curl -H "Authorization: Bearer TOKEN" https://YOUR-SERVER/api/v1/players/PID</pre>
${rows}
<h2>Live games: the WebSocket</h2><p>Connect to <code>wss://YOUR-SERVER/ws</code> and send JSON messages <code>{ "t": "…", … }</code>. The server answers with messages of the same shape; the full types are in <code>shared/protocol.ts</code>.</p>
<table><tr><td><b>You send</b></td><td><b>You get</b></td><td></td></tr>${ws}</table></main></body></html>`;
}
