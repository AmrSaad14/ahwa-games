// The dashboard's API (/api/admin/…), used by the dashboard page at /admin (server/dashboard.ts).
// Sign in with ADMIN_KEY: the server gives back a session cookie (12 hours, HttpOnly). Everything the team does
// here is written to the admin log.
import crypto from 'node:crypto';
import type http from 'node:http';
import type { Store, UserRow } from './db';
import type { Hub } from './hub';
import type { Settings } from './settings';
import { SKINS } from '../shared/skins';
import { TIERS, SEASON_PASS } from '../shared/season';
import { Limiter } from './auth';
import { TITLE_ICONS, TITLE_EFFECTS } from '../shared/titles';
import { GAMES } from '../shared/engine';
import { seasonOf } from '../shared/season';

export interface AdminCtx {
  store: Store; hub: Hub; settings: Settings;
  adminKey: string; secret: string;
  ip: (req: http.IncomingMessage) => string;
  /** A push to some players' phones: Arabic and English title and text. */
  push: (uids: string[], ar: [string, string], en: [string, string]) => void;
  pushOn: () => boolean;
}

class HttpError extends Error { constructor(public status: number, public code: string) { super(code); } }
const fail = (status: number, code: string): never => { throw new HttpError(status, code); };
const COOKIE = 'bb_admin';
const SESSION_MS = 12 * 3_600_000;

function readBody(req: http.IncomingMessage): Promise<Record<string, unknown>> {
  return new Promise((resolve, reject) => {
    let raw = '';
    req.on('data', c => { raw += c; if (raw.length > 256 * 1024) { reject(new HttpError(413, 'too_big')); req.destroy(); } });
    req.on('end', () => { try { resolve(raw ? JSON.parse(raw) : {}); } catch { reject(new HttpError(400, 'bad_json')); } });
    req.on('error', reject);
  });
}

/** A player as a row in the dashboard's lists. */
const row = (u: UserRow) => ({
  id: u.id, pid: u.pid, code: u.friend_code, name: u.name, coins: u.coins, games: u.games, wins: u.wins, rating: u.rating,
  created: u.created, seen: u.seen, banned: !!u.banned, muted: (u.muted_until ?? 0) > Date.now(),
  login: u.google_sub ? 'google' : u.apple_sub ? 'apple' : u.phone ? 'phone' : u.email ? 'email' : 'guest',
  contact: u.google_email || u.apple_email || u.email || u.phone || '',
});

export function adminApi(ctx: AdminCtx) {
  const { store, hub, settings } = ctx;
  const loginLimiter = new Limiter(8, 10 * 60_000);
  const sign = (exp: number) => crypto.createHmac('sha256', ctx.secret).update('admin:' + exp).digest('base64url');
  const issue = () => { const exp = Date.now() + SESSION_MS; return `${exp}.${sign(exp)}`; };
  const valid = (tok: string | undefined) => {
    if (!tok) return false;
    const [e, sig] = tok.split('.'); const exp = Number(e);
    if (!exp || exp < Date.now() || !sig) return false;
    const good = sign(exp);
    return sig.length === good.length && crypto.timingSafeEqual(Buffer.from(sig), Buffer.from(good));
  };
  const keyOk = (k: string) => !!ctx.adminKey && k.length === ctx.adminKey.length && crypto.timingSafeEqual(Buffer.from(k), Buffer.from(ctx.adminKey));
  const cookieOf = (req: http.IncomingMessage) => (String(req.headers.cookie || '').split(/;\s*/).find(c => c.startsWith(COOKIE + '=')) || '').slice(COOKIE.length + 1);
  const authed = (req: http.IncomingMessage) => valid(cookieOf(req)) || keyOk(String(req.headers['x-admin-key'] || ''));
  const log = (req: http.IncomingMessage, action: string, target: string | null, detail?: unknown) => store.adminLog('admin@' + ctx.ip(req), action, target, detail);

  async function route(req: http.IncomingMessage, res: http.ServerResponse, url: URL): Promise<unknown> {
    const p = url.pathname.slice('/api/admin'.length) || '/', m = req.method || 'GET';
    if (!ctx.adminKey) fail(503, 'admin_off');   // ADMIN_KEY not set on the server
    if (p === '/login' && m === 'POST') {
      if (!loginLimiter.allow(ctx.ip(req))) fail(429, 'slow_down');
      const b = await readBody(req);
      if (!keyOk(String(b.key || ''))) { log(req, 'login_failed', null); fail(403, 'wrong_key'); }
      res.setHeader('Set-Cookie', `${COOKIE}=${issue()}; Path=/; HttpOnly; SameSite=Strict; Max-Age=${SESSION_MS / 1000}${req.headers['x-forwarded-proto'] === 'https' || (req.socket as { encrypted?: boolean }).encrypted ? '; Secure' : ''}`);
      log(req, 'login', null);
      return { ok: true };
    }
    if (p === '/logout' && m === 'POST') { res.setHeader('Set-Cookie', `${COOKIE}=; Path=/; Max-Age=0`); return { ok: true }; }
    if (!authed(req)) fail(401, 'sign_in');

    // ---- the front page
    if (p === '/overview') {
      const st = settings.value;
      return { ...store.adminOverview(), live: { ...hub.stats(), online: hub.onlineIds.length }, daily: store.adminDaily(14),
        tourney: { state: hub.tourney.state, startsAt: hub.tourney.startsAt, game: hub.tourney.def.game, registered: store.tourneyRegs(hub.tourney.event).length, enabled: st.tourney.enabled },
        flags: { season: st.season.enabled, prizes: st.prizes.enabled, maintenance: st.maintenance.on, notice: st.notice.until > Date.now() ? st.notice.text : '', push: ctx.pushOn() } };
    }
    if (p === '/catalog') return { skins: SKINS, tiers: TIERS, pass: SEASON_PASS, titles: hub.titles.list, icons: TITLE_ICONS };

    // ---- players
    if (p === '/players' && m === 'GET') {
      const rows = store.adminSearch(url.searchParams.get('q') || '', 50, Number(url.searchParams.get('offset')) || 0);
      const online = new Set(hub.onlineIds);
      return { rows: rows.map(u => ({ ...row(u), online: online.has(u.id) })) };
    }
    const pm = p.match(/^\/players\/([A-Za-z0-9_-]{4,40})$/);
    if (pm && m === 'GET') {
      const d = store.adminPlayer(pm[1]) ?? fail(404, 'no_player');
      return { ...d, row: row(d.user), online: hub.onlineIds.includes(pm[1]) };
    }
    if (pm && m === 'POST') {
      const id = pm[1], b = await readBody(req), u = store.get(id) ?? fail(404, 'no_player');
      const a = String(b.action || '');
      switch (a) {
        case 'coins': {
          const delta = Math.round(Number(b.amount));
          if (!Number.isFinite(delta) || !delta || Math.abs(delta) > 1_000_000_000) fail(400, 'bad_amount');
          const now = store.adminCoins(id, delta, delta > 0 ? 'admin_gift' : 'admin_take');
          if (delta > 0 && b.notify !== false) ctx.push([id], ['هدية من بورد باتلز 🎁', `جالك ${delta.toLocaleString('en')} كوين`], ['A gift from Board Battles 🎁', `You got ${delta.toLocaleString('en')} coins`]);
          log(req, 'coins', id, { delta, reason: String(b.reason || '').slice(0, 200) });
          hub.adminChanged(id); return { ok: true, coins: now };
        }
        case 'skin': case 'unskin': {
          if (!store.adminGiveSkin(id, String(b.skin || ''), a === 'skin')) fail(400, 'bad_skin');
          log(req, a, id, { skin: b.skin }); hub.adminChanged(id); return { ok: true };
        }
        case 'ban': case 'unban': store.adminBan(id, a === 'ban', String(b.reason || '')); log(req, a, id, { reason: b.reason }); hub.adminChanged(id); return { ok: true };
        case 'mute': store.mute(id, Date.now() + Math.max(1, Math.min(24 * 30, Number(b.hours) || 24)) * 3_600_000); log(req, 'mute', id, { hours: b.hours }); hub.adminChanged(id); return { ok: true };
        case 'unmute': store.adminUnmute(id); log(req, 'unmute', id); hub.adminChanged(id); return { ok: true };
        case 'rename': {
          const name = String(b.name || '').trim(); if (name.length < 2) fail(400, 'bad_name');
          store.adminRename(id, name); log(req, 'rename', id, { from: u.name, to: name }); hub.adminChanged(id); return { ok: true };
        }
        case 'premium': case 'unpremium': store.adminSeasonPremium(id, a === 'premium'); log(req, a, id); hub.adminChanged(id); return { ok: true };
        case 'sp': store.adminSeasonSp(id, Number(b.sp) || 0); log(req, 'season_points', id, { sp: b.sp }); hub.adminChanged(id); return { ok: true };
        case 'notify': {
          const title = String(b.title || '').slice(0, 80), body = String(b.text || '').slice(0, 300); if (!body) fail(400, 'empty');
          ctx.push([id], [title || 'بورد باتلز', body], [title || 'Board Battles', body]); log(req, 'notify', id, { title, body }); return { ok: true };
        }
        case 'title': {
          const t = hub.titles.get(String(b.title || '')) ?? fail(400, 'bad_title');
          const days = Math.max(0, Math.min(3650, Number(b.days) || 0));
          store.grantTitle(id, t.id, days ? Date.now() + days * 86_400_000 : 0, String(b.note || '').slice(0, 100) || null, b.wear !== false);
          if (b.notify !== false) ctx.push([id], ['لقب جديد 🏅', `بقيت «${t.ar}»`], ['A new title 🏅', `You are now "${t.en}"`]);
          log(req, 'title', id, { title: t.id, days }); hub.adminChanged(id); return { ok: true };
        }
        case 'untitle': store.revokeTitle(id, String(b.title || '')); log(req, 'untitle', id, { title: b.title }); hub.adminChanged(id); return { ok: true };
        case 'delete': store.deleteUser(id); log(req, 'delete', id, { name: u.name }); return { ok: true };
        default: fail(400, 'bad_action');
      }
    }

    // ---- rewards for everyone, notices
    if (p === '/rewards' && m === 'POST') {
      const b = await readBody(req);
      const coins = Math.round(Number(b.coins)), days = Math.max(0, Math.round(Number(b.activeDays) || 0));
      if (!(coins > 0 && coins <= 100_000_000)) fail(400, 'bad_amount');
      const n = store.adminCoinsToAll(coins, days);
      const msg = String(b.message || '').slice(0, 200);
      const ids = store.adminIdsSeenSince(days > 0 ? Date.now() - days * 86_400_000 : 0);
      for (const uid of hub.onlineIds) hub.adminChanged(uid);
      if (b.push !== false) ctx.push(ids, ['هدية لكل اللاعيبة 🎁', msg || `جالك ${coins.toLocaleString('en')} كوين`], ['A gift for every player 🎁', msg || `You got ${coins.toLocaleString('en')} coins`]);
      if (msg) hub.noticeAll(msg);
      log(req, 'gift_all', null, { coins, activeDays: days, players: n, message: msg });
      return { ok: true, players: n };
    }
    if (p === '/notice' && m === 'POST') {
      const b = await readBody(req);
      const text = String(b.text || '').trim().slice(0, 400), hours = Math.max(0, Math.min(24 * 30, Number(b.hours) || 24));
      settings.update({ notice: { text, until: text ? Date.now() + hours * 3_600_000 : 0 } });
      if (text) hub.noticeAll(text);
      if (text && b.push) ctx.push(store.adminIdsSeenSince(Date.now() - 30 * 86_400_000), ['بورد باتلز', text], ['Board Battles', text]);
      log(req, 'notice', null, { text, hours, push: !!b.push });
      return { ok: true };
    }

    // ---- monthly prizes: Vodafone Cash, paid by hand
    if (p === '/prizes' && m === 'GET') {
      const status = url.searchParams.get('status') || 'pending';
      return { rows: store.prizesAll(status === 'all' ? null : status, 500), rules: settings.value.prizes, minGames: settings.value.titles.minGames };
    }
    const pzm = p.match(/^\/prizes\/(\d+)$/);
    if (pzm && m === 'POST') {
      const b = await readBody(req), action = String(b.action || '');
      if (!['paid', 'reject', 'again'].includes(action)) fail(400, 'bad_action');
      const note = String(b.note || '').slice(0, 200);
      const c = store.settlePrize(Number(pzm[1]), action as 'paid' | 'reject' | 'again', note) ?? fail(409, 'not_waiting');
      if (action === 'paid') ctx.push([c.user_id], ['اتحولك الجايزة 💸', `${c.egp} جنيه على فودافون كاش ${c.phone}`], ['Prize paid 💸', `${c.egp} EGP sent to Vodafone Cash ${c.phone}`]);
      else if (action === 'again') ctx.push([c.user_id], ['محتاجين رقمك تاني', 'افتح اللعبة واكتب رقم فودافون كاش صح عشان نحولك الجايزة' + (note ? ': ' + note : '')], ['We need your number again', 'Open the game and enter the right Vodafone Cash number for your prize' + (note ? ': ' + note : '')]);
      else ctx.push([c.user_id], ['الجايزة اترفضت', note || 'تواصل مع الدعم من الإعدادات'], ['Prize declined', note || 'Contact support from Settings']);
      hub.refreshPlayer(c.user_id);
      log(req, 'prize_' + action, c.user_id, { id: c.id, egp: c.egp, phone: c.phone, month: c.month, game: c.game, note });
      return { ok: true };
    }

    // ---- the tournament
    if (p === '/tourney' && m === 'GET') {
      const t = hub.tourney, regs = store.tourneyRegs(t.event);
      return { view: t.view(null), settings: settings.value.tourney, history: store.tourneyHistory(20),
        registered: regs.slice(0, 300).map(id => { const u = store.get(id); return { id, name: u?.name ?? '?', online: hub.onlineIds.includes(id) }; }) };
    }
    if (p === '/tourney' && m === 'POST') {
      const b = await readBody(req);
      if (b.action === 'start') { hub.tourney.startNow(); log(req, 'tourney_start', hub.tourney.event); return { ok: true, state: hub.tourney.state }; }
      if (b.action === 'cancel') { const ev = hub.tourney.event; hub.tourney.cancel(); log(req, 'tourney_cancel', ev); return { ok: true }; }
      fail(400, 'bad_action');
    }

    // ---- the season pass
    if (p === '/season' && m === 'GET') return { ...store.adminSeason(), settings: settings.value.season, skins: SKINS };

    // ---- titles: the designs, who has them, the monthly race
    if (p === '/titles' && m === 'GET') {
      const month = seasonOf(Date.now()), cfg = settings.value.titles;
      const winners = (store.metaLike('potm_winners_') as { k: string; v: string }[]).slice(0, 12).map(r => { try { return { month: r.k.slice('potm_winners_'.length), rows: JSON.parse(r.v) }; } catch { return null; } }).filter(Boolean);
      return { list: hub.titles.list, counts: store.titleCounts(), icons: TITLE_ICONS, effects: TITLE_EFFECTS, settings: cfg, month,
        race: Object.fromEntries(GAMES.map(g => [g, store.monthlyBoard(g, month, 5, 0)])), winners };
    }
    if (p === '/titles' && (m === 'PUT' || m === 'POST')) {
      const b = await readBody(req);
      const t = hub.titles.put(b) ?? fail(400, 'bad_title');
      log(req, 'title_saved', null, t); return t;
    }
    const tm = p.match(/^\/titles\/([a-z0-9_]{2,32})(\/holders)?$/);
    if (tm && m === 'GET') return { rows: store.titleHolders(tm[1], 300) };
    if (tm && m === 'DELETE') {
      if (!hub.titles.remove(tm[1])) fail(400, 'cant_remove');
      store.dropTitle(tm[1]); for (const uid of hub.onlineIds) hub.adminChanged(uid);
      log(req, 'title_removed', null, { id: tm[1] }); return { ok: true };
    }

    // ---- settings
    if (p === '/settings' && m === 'GET') return settings.value;
    if (p === '/settings' && (m === 'PUT' || m === 'POST')) {
      const b = await readBody(req), before = JSON.stringify(settings.value);
      const after = settings.update(b);
      log(req, 'settings', null, changed(JSON.parse(before), after));
      for (const uid of hub.onlineIds) hub.adminChanged(uid);
      return after;
    }
    if (p === '/settings/reset' && m === 'POST') { const v = settings.reset(); log(req, 'settings_reset', null); return v; }

    // ---- lists
    if (p === '/reports') return { rows: store.reportsFull(300) };
    if (p === '/support') return { rows: store.supportAll(300) };
    if (p === '/purchases') return { rows: store.purchasesAll(300) };
    if (p === '/log') return { rows: store.adminLogAll(300) };
    fail(404, 'not_found');
  }

  /** Answers /api/admin/… (returns false for anything else). */
  return (req: http.IncomingMessage, res: http.ServerResponse, url: URL): boolean => {
    if (!url.pathname.startsWith('/api/admin/') && url.pathname !== '/api/admin') return false;
    res.setHeader('Cache-Control', 'no-store');
    route(req, res, url).then(out => {
      res.writeHead(200, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(out));
    }).catch(e => {
      const st = e instanceof HttpError ? e.status : 500;
      if (st === 500) console.error('[admin]', (e as Error).message);
      res.writeHead(st, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ error: e instanceof HttpError ? e.code : 'server_error' }));
    });
    return true;
  };
}

/** Which settings a change touched (for the log). */
function changed(a: Record<string, unknown>, b: object): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(b)) if (JSON.stringify(a[k]) !== JSON.stringify(v)) out[k] = v;
  return out;
}
