// The HTTP API (/api/v1): everything about a player's account that isn't a live game, for the app, a website,
// a dashboard or anything else. Live games (matchmaking, moves, chat, voice) run over the WebSocket at /ws.
//
// Sign in: every request but a few public ones carries the player's token: "Authorization: Bearer <token>"
// (the same token the game gets in its "welcome" message; POST /api/v1/auth/guest makes a new guest and returns one).
// Answers are JSON: { ...data } with 200, or { error: "code" } with 4xx. The full list is at /api/docs.
import { TOURNEY } from './tourney';
import type http from 'node:http';
import { Store, REFER_BONUS } from './db';
import { Tokens, Limiter, checkPassword, hashPassword } from './auth';
import type { Hub } from './hub';
import { GAMES, GameId } from '../shared/engine';
import { WEEKLY_PRIZES, weekKey, ECONOMY } from '../shared/gifts';
import { SKINS, isFree } from '../shared/skins';
import { TIERS, SEASON_PASS, SEASON_TIERS, SP_PER_TIER } from '../shared/season';
import { MISSIONS } from '../shared/missions';
import { weekEndsAt } from './hub';
import { openApi, docsPage } from './apidocs';

export interface ApiContext {
  store: Store; tokens: Tokens; hub: Hub;
  support: (m: { uid: string | null; name: string; contact: string; text: string; app: string }) => number;
  ip: (req: http.IncomingMessage) => string;
}

class Fail { constructor(public status: number, public code: string) {} }
const fail = (status: number, code: string) => { throw new Fail(status, code); };
const EMAIL_RE = /^[^\s@]{1,64}@[^\s@]{1,190}\.[^\s@]{2,24}$/;
const cleanName = (s: unknown) => String(s ?? '').replace(/[\u0000-\u001f\u007f-\u009f​-‏‪-‮⁦-⁩<>]/g, '').trim().slice(0, 14);

type Handler = (a: { uid: string | null; body: Record<string, unknown>; params: string[]; query: URLSearchParams; req: http.IncomingMessage }) => unknown | Promise<unknown>;
interface Route { method: string; path: RegExp; auth: boolean; run: Handler }

export function api(ctx: ApiContext) {
  const { store, tokens, hub } = ctx;
  const perIp = new Limiter(240, 60_000);        // requests per minute per address
  const guests = new Limiter(10, 60 * 60_000);   // new guest accounts per hour per address
  const logins = new Limiter(10, 60_000);
  const writes = new Limiter(60, 60_000);        // changes per minute per player

  const me = (uid: string) => {
    const u = store.get(uid); if (!u) fail(401, 'unknown_player');
    return store.publicUser(u!);
  };
  const changed = (uid: string) => hub.refreshPlayer(uid);
  const friends = (uid: string) => store.friendLinks(uid).map(l => {
    const u = store.get(l.other);
    return u ? { pid: u.pid ?? '', name: u.name, ch: u.ch, status: l.status, online: hub.isOnline(u.id) } : null;
  }).filter(Boolean);
  const other = (pid: string) => { const u = store.byPid(pid); if (!u) fail(404, 'player_not_found'); return u!; };

  const routes: Route[] = [
    /* ---------- public ---------- */
    { method: 'GET', path: /^\/api\/v1\/?$/, auth: false, run: () => ({ name: 'Board Battles API', version: 1, docs: '/api/docs', openapi: '/api/v1/openapi.json', websocket: '/ws' }) },
    { method: 'GET', path: /^\/api\/v1\/openapi\.json$/, auth: false, run: () => openApi() },
    { method: 'GET', path: /^\/api\/v1\/health$/, auth: false, run: () => ({ ok: true, ...hub.stats() }) },
    { method: 'GET', path: /^\/api\/v1\/catalog$/, auth: false, run: () => ({
      skins: SKINS.map(s => ({ ...s, free: isFree(s) })), missions: MISSIONS, weeklyPrizes: WEEKLY_PRIZES,
      season: { tiers: TIERS, count: SEASON_TIERS, spPerTier: SP_PER_TIER, pass: SEASON_PASS }, economy: ECONOMY, referralBonus: REFER_BONUS,
    }) },
    { method: 'GET', path: /^\/api\/v1\/leaderboard$/, auth: false, run: ({ uid, query }) => {
      const g = query.get('game') || 'domino';
      if (g === 'week') return { game: 'week', week: weekKey(), endsAt: weekEndsAt(), prizes: WEEKLY_PRIZES, ...store.weeklyBoard(uid) };
      if (!(GAMES as string[]).includes(g)) fail(400, 'bad_game');
      return { game: g, ...store.leaderboard(uid, g as GameId) };
    } },
    { method: 'GET', path: /^\/api\/v1\/players\/([A-Za-z0-9]{4,24})$/, auth: false, run: ({ uid, params }) => {
      const c = hub.card(uid, params[0]); if (!c) fail(404, 'player_not_found'); return c;
    } },
    { method: 'GET', path: /^\/api\/v1\/tournament$/, auth: false, run: ({ uid }) => hub.tourney.view(uid) },

    /* ---------- signing in ---------- */
    { method: 'POST', path: /^\/api\/v1\/auth\/guest$/, auth: false, run: ({ req, body }) => {
      if (!guests.allow(ctx.ip(req))) fail(429, 'slow_down');
      const name = cleanName(body.name) || 'Player' + Math.floor(1000 + Math.random() * 9000);
      const ch = Number.isInteger(body.ch) && (body.ch as number) >= 0 && (body.ch as number) <= 5 ? body.ch as number : Math.floor(Math.random() * 5);
      const u = store.createGuest(name, ch);
      return { token: tokens.issue(u.id), user: store.publicUser(u) };
    } },
    { method: 'POST', path: /^\/api\/v1\/auth\/login$/, auth: false, run: ({ req, body }) => {
      if (!logins.allow(ctx.ip(req))) fail(429, 'slow_down');
      const u = store.byEmail(String(body.email || '').trim().toLowerCase());
      if (!u || !checkPassword(String(body.password || ''), u.pw)) fail(401, 'bad_login');
      return { token: tokens.issue(u!.id), user: store.publicUser(u!) };
    } },
    { method: 'POST', path: /^\/api\/v1\/auth\/register$/, auth: true, run: ({ uid, req, body }) => {
      if (!logins.allow(ctx.ip(req))) fail(429, 'slow_down');
      const email = String(body.email || '').trim().toLowerCase(), pw = String(body.password || '');
      if (!EMAIL_RE.test(email)) fail(400, 'bad_email');
      if (pw.length < 6 || pw.length > 128) fail(400, 'weak_password');
      if (store.get(uid!)!.email) fail(409, 'already_registered');
      if (store.byEmail(email)) fail(409, 'email_taken');
      store.setCredentials(uid!, email, hashPassword(pw));
      changed(uid!);
      return me(uid!);
    } },

    /* ---------- you ---------- */
    { method: 'GET', path: /^\/api\/v1\/me$/, auth: true, run: ({ uid }) => me(uid!) },
    { method: 'PATCH', path: /^\/api\/v1\/me$/, auth: true, run: ({ uid, body }) => {
      const u = store.get(uid!)!;
      const name = body.name !== undefined ? cleanName(body.name) || u.name : u.name;
      const ch = Number.isInteger(body.ch) && (body.ch as number) >= 0 && (body.ch as number) <= 5 ? body.ch as number : u.ch;
      store.setProfile(uid!, name, ch); changed(uid!);
      return me(uid!);
    } },
    { method: 'GET', path: /^\/api\/v1\/me\/card$/, auth: true, run: ({ uid }) => hub.card(uid, store.get(uid!)!.pid ?? '') },
    { method: 'GET', path: /^\/api\/v1\/me\/coins$/, auth: true, run: ({ uid }) => ({ rows: store.coinHistory(uid!, 100) }) },
    { method: 'DELETE', path: /^\/api\/v1\/me$/, auth: true, run: ({ uid }) => { store.deleteUser(uid!); return { deleted: true }; } },

    /* ---------- the daily reward ---------- */
    { method: 'GET', path: /^\/api\/v1\/daily$/, auth: true, run: ({ uid }) => store.dailyState(store.get(uid!)!) },
    { method: 'POST', path: /^\/api\/v1\/daily\/claim$/, auth: true, run: ({ uid }) => {
      const d = store.claimDaily(uid!);
      if (!d.coins) fail(409, 'already_claimed');
      hub.notify(uid!, { t: 'bonus', coins: d.coins, streak: d.streak }); changed(uid!);
      return d;
    } },

    /* ---------- missions ---------- */
    { method: 'GET', path: /^\/api\/v1\/missions$/, auth: true, run: ({ uid }) => ({ missions: store.missions(uid!), resetsAt: me(uid!).missionsAt }) },
    { method: 'POST', path: /^\/api\/v1\/missions\/([a-z0-9_]+)\/claim$/, auth: true, run: ({ uid, params }) => {
      const r = store.missionClaim(uid!, params[0]);
      if (typeof r === 'string') fail(409, r);
      const x = store.addXp(uid!, (r as { xp: number }).xp);
      changed(uid!);
      return { ...(r as object), level: x.level, levelUp: x.levelUp };
    } },

    /* ---------- the season pass ---------- */
    { method: 'GET', path: /^\/api\/v1\/season$/, auth: true, run: ({ uid }) => ({ ...store.seasonState(uid!), tiers: TIERS, spPerTier: SP_PER_TIER }) },
    { method: 'POST', path: /^\/api\/v1\/season\/premium$/, auth: true, run: ({ uid }) => {
      const e = store.seasonPremium(uid!, false); if (e) fail(409, e);
      changed(uid!); return store.seasonState(uid!);
    } },
    { method: 'POST', path: /^\/api\/v1\/season\/claim$/, auth: true, run: ({ uid, body }) => {
      const r = store.seasonClaim(uid!, Number(body.tier), body.track === 'prem' ? 'prem' : 'free');
      if (typeof r === 'string') fail(409, r);
      changed(uid!); return r;
    } },

    /* ---------- the weekly tournament ---------- */
    { method: 'POST', path: /^\/api\/v1\/tournament\/join$/, auth: true, run: ({ uid }) => {
      if (!TOURNEY.enabled) fail(409, 'tourney_off');
      if (hub.tourney.state !== 'open') fail(409, 'tourney_started');
      store.tourneyJoin(hub.tourney.event, uid!); return hub.tourney.view(uid);
    } },
    { method: 'DELETE', path: /^\/api\/v1\/tournament\/join$/, auth: true, run: ({ uid }) => {
      if (hub.tourney.state !== 'open') fail(409, 'tourney_started');
      store.tourneyLeave(hub.tourney.event, uid!); return hub.tourney.view(uid);
    } },

    /* ---------- friends ---------- */
    { method: 'GET', path: /^\/api\/v1\/friends$/, auth: true, run: ({ uid }) => ({ code: store.friendCode(uid!), rows: friends(uid!) }) },
    { method: 'POST', path: /^\/api\/v1\/friends$/, auth: true, run: ({ uid, body }) => {
      const o = typeof body.pid === 'string' ? store.byPid(body.pid) : typeof body.code === 'string' ? store.byFriendCode(body.code.replace(/[^a-z0-9]/gi, '')) : undefined;
      if (!o) fail(404, 'friend_not_found');
      if (o!.id === uid) fail(400, 'friend_self');
      if (store.friendCount(uid!) >= 300) fail(409, 'friends_full');
      const r = store.friendRequest(uid!, o!.id);
      const meRow = store.get(uid!)!;
      if (r === 'sent') hub.notify(o!.id, { t: 'friendNews', kind: 'request', name: meRow.name });
      if (r === 'friends') hub.notify(o!.id, { t: 'friendNews', kind: 'accepted', name: meRow.name });
      hub.friendsUpdated(uid!); hub.friendsUpdated(o!.id);
      return { result: r, status: store.friendStatus(uid!, o!.id) };
    } },
    { method: 'POST', path: /^\/api\/v1\/friends\/([A-Za-z0-9]{4,24})\/(accept|decline)$/, auth: true, run: ({ uid, params }) => {
      const o = other(params[0]);
      if (params[1] === 'accept') { if (!store.friendAccept(uid!, o.id)) fail(404, 'no_request'); hub.notify(o.id, { t: 'friendNews', kind: 'accepted', name: store.get(uid!)!.name }); }
      else store.friendRemove(uid!, o.id);
      hub.friendsUpdated(uid!); hub.friendsUpdated(o.id);
      return { status: store.friendStatus(uid!, o.id) };
    } },
    { method: 'DELETE', path: /^\/api\/v1\/friends\/([A-Za-z0-9]{4,24})$/, auth: true, run: ({ uid, params }) => {
      const o = other(params[0]);
      store.friendRemove(uid!, o.id);
      hub.friendsUpdated(uid!); hub.friendsUpdated(o.id);
      return { status: null };
    } },

    /* ---------- the shop (coins; Google Play purchases go through the app) ---------- */
    { method: 'GET', path: /^\/api\/v1\/shop$/, auth: true, run: ({ uid }) => {
      const u = me(uid!);
      return { coins: u.coins, owned: u.skins, equipped: u.equip, skins: SKINS.map(s => ({ ...s, free: isFree(s), owned: isFree(s) || u.skins.includes(s.id) })) };
    } },
    { method: 'POST', path: /^\/api\/v1\/shop\/buy$/, auth: true, run: ({ uid, body }) => {
      const e = store.buySkin(uid!, String(body.id || '')); if (e) fail(409, e);
      changed(uid!); return me(uid!);
    } },
    { method: 'POST', path: /^\/api\/v1\/shop\/equip$/, auth: true, run: ({ uid, body }) => {
      const e = store.equipSkin(uid!, String(body.id || '')); if (e) fail(409, e);
      changed(uid!); return me(uid!);
    } },

    /* ---------- referrals, reports, support ---------- */
    { method: 'POST', path: /^\/api\/v1\/referral$/, auth: true, run: ({ uid, body }) => {
      const e = store.setReferrer(uid!, String(body.code || '')); if (e) fail(409, e);
      changed(uid!); return { referred: true, bonus: REFER_BONUS, note: 'paid to both of you after your first game' };
    } },
    { method: 'POST', path: /^\/api\/v1\/report$/, auth: true, run: ({ uid, body }) => {
      const o = other(String(body.pid || ''));
      if (o.id === uid) fail(400, 'bad_report');
      store.report(uid!, o.id, String(body.reason || 'other').slice(0, 40), 'api');
      if (store.reporters(o.id, Date.now() - 86_400_000) >= 3 && store.mutedUntil(o.id) < Date.now()) store.mute(o.id, Date.now() + 86_400_000);
      return { reported: true };
    } },
    { method: 'POST', path: /^\/api\/v1\/support$/, auth: false, run: ({ uid, body }) => {
      const text = String(body.text || '').trim().slice(0, 4000);
      if (text.length < 3) fail(400, 'support_empty');
      const u = uid ? store.get(uid) : undefined;
      const id = ctx.support({ uid, name: u?.name ?? String(body.name || '').slice(0, 60), contact: String(body.contact || '').slice(0, 200) || u?.google_email || u?.email || '', text, app: String(body.app || 'api').slice(0, 80) });
      return { id };
    } },
  ];

  /** Handles /api/v1/… (true), or leaves the request to the rest of the server (false). */
  return function handle(req: http.IncomingMessage, res: http.ServerResponse, url: URL): boolean {
    if (url.pathname === '/api/docs' || url.pathname === '/api/docs/') {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'max-age=300' });
      res.end(docsPage()); return true;
    }
    if (!url.pathname.startsWith('/api/v1')) return false;
    const send = (status: number, body: unknown) => {
      res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store', 'Access-Control-Allow-Origin': '*' });
      res.end(JSON.stringify(body));
    };
    if (req.method === 'OPTIONS') {
      res.writeHead(204, { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Methods': 'GET, POST, PATCH, DELETE, OPTIONS',
        'Access-Control-Allow-Headers': 'Authorization, Content-Type', 'Access-Control-Max-Age': '86400' });
      res.end(); return true;
    }
    if (!perIp.allow(ctx.ip(req))) { send(429, { error: 'slow_down' }); return true; }
    let route: Route | undefined, params: string[] = [];
    for (const r of routes) {
      const m = r.method === req.method ? url.pathname.match(r.path) : null;
      if (m) { route = r; params = m.slice(1); break; }
    }
    if (!route) { send(routes.some(r => r.path.test(url.pathname)) ? 405 : 404, { error: routes.some(r => r.path.test(url.pathname)) ? 'method_not_allowed' : 'not_found' }); return true; }
    const auth = String(req.headers.authorization || '');
    const token = auth.startsWith('Bearer ') ? auth.slice(7).trim() : '';
    const uid = token ? tokens.verify(token) : null;
    if (uid && !store.get(uid)) { send(401, { error: 'unknown_player' }); return true; }
    if (route.auth && !uid) { send(401, { error: 'sign_in' }); return true; }
    const run = (body: Record<string, unknown>) => {
      if (uid && req.method !== 'GET' && !writes.allow(uid)) return send(429, { error: 'slow_down' });
      Promise.resolve().then(() => route!.run({ uid, body, params, query: url.searchParams, req }))
        .then(out => send(200, out ?? {}))
        .catch(e => { if (e instanceof Fail) send(e.status, { error: e.code }); else { console.error('[api]', (e as Error).message); send(500, { error: 'server_error' }); } });
    };
    if (req.method === 'GET' || req.method === 'DELETE') { run({}); return true; }
    let raw = '';
    req.on('data', c => { raw += c; if (raw.length > 16_384) req.destroy(); });
    req.on('end', () => {
      let body: Record<string, unknown> = {};
      try { const j = JSON.parse(raw || '{}'); if (j && typeof j === 'object' && !Array.isArray(j)) body = j; } catch { return send(400, { error: 'bad_json' }); }
      run(body);
    });
    return true;
  };
}
