import './styles.css';
import { App as CapApp } from '@capacitor/app';
import { Share } from '@capacitor/share';
import { Browser } from '@capacitor/browser';
import { Capacitor } from '@capacitor/core';
import { CHESS_TIME, End, GAMES, GAME_MODES, GameId, MODES, MODE_PLAYERS, Mode, TILES, canPlay, defaultTarget, endsFor, gameOf, targetOptions, teamOf } from '../../shared/engine';
import type { Action, AnyView, BoardAction, BoardView, CoinRow, Emote, LeaderRow, PublicUser, S2C, TableView } from '../../shared/protocol';
import { LUDO_COLORS } from './boards';
import type { BoardUi, Pick } from './boards';
import { BAR, OFF } from '../../shared/games/tawla';
import { EMOTES } from '../../shared/protocol';
import { CHAR_NAMES, Key, Lang, errText, getLang, num, rules, setLang, t } from './i18n';
import { Net, httpBase } from './net';
import { Practice } from './practice';
import { CHARS, TableScene, charColor } from './scene';
import { prefs, sfx, unlockAudio } from './sound';
import { Voice } from './voice';
import { DemoWallet } from './demowallet';
import { adsSupported, afterMatch, initAds, showRewarded } from './ads';
import { GIFTS, GiftKind, STAKES, giftByKind, isStake, shortCoins, winnings } from '../../shared/gifts';

/** Demo build (no game server): stakes are played against the computer with a wallet kept in this browser. */
declare const __DEMO__: boolean;
const DEMO = typeof __DEMO__ !== 'undefined' && __DEMO__;
let wallet: DemoWallet | null = null;

/* ---------------- storage ---------------- */
const store = {
  get(k: string, d = '') { try { return localStorage.getItem('dom.' + k) ?? d; } catch { return d; } },
  set(k: string, v: string) { try { localStorage.setItem('dom.' + k, v); } catch { /* private mode */ } },
  del(k: string) { try { localStorage.removeItem('dom.' + k); } catch { /* ignore */ } },
};

/* ---------------- state ---------------- */
type Screen = 'home' | 'friends' | 'lb' | 'settings' | 'rules' | 'coins';
type Sheet = null | 'leave' | 'emotes' | 'delete' | 'player' | 'signin' | 'fakead';
const S = {
  screen: 'home' as Screen,
  sheet: null as Sheet,
  online: false,
  user: null as PublicUser | null,
  mode: (MODES.includes(store.get('mode') as Mode) ? store.get('mode') : 'ffa2') as Mode,
  target: 101,
  queue: null as null | { mode: Mode; count: number; need: number; botsAt: number; stake?: number },
  view: null as AnyView | null,
  practice: null as Practice | null,
  sel: null as number | null,
  choose: null as End[] | null,
  lb: null as null | { rows: LeaderRow[]; me: LeaderRow | null },
  joinCode: '',
  busy: false,
  lastKey: -1,
  bubbles: new Map<number, { k?: Emote; text?: string; until: number }>(),
  player: -1,              // seat shown in the player sheet
  game: (GAMES.includes(store.get('game') as GameId) ? store.get('game') : 'domino') as GameId,
  boardSel: null as number | null,   // chess square / tawla point the player picked up
  promo: null as null | { from: number; to: number },
  lbGame: 'domino' as GameId,
  reporting: false,
  stake: (isStake(Number(store.get('stake'))) ? Number(store.get('stake')) : STAKES[0]) as number,
  coinRows: null as CoinRow[] | null,
  login: { google: false, phone: false },
  otpPhone: null as string | null,     // number a sign-in code was sent to
  phoneDraft: '',
  payout: null as null | { kind: string; delta: number; fee: number; stake: number; table: string },
};
if (gameOf(S.mode) !== S.game) S.mode = GAME_MODES[S.game][0];
S.target = defaultTarget(S.mode);
S.lbGame = S.game;
prefs.sound = store.get('sound', '1') === '1';
prefs.vibrate = store.get('vibrate', '1') === '1';
setLang((store.get('lang') as Lang) || (navigator.language?.startsWith('ar') ? 'ar' : 'en'));
const localName = () => S.user?.name || store.get('name') || CHAR_NAMES[getLang()][localCh()];
const localCh = () => S.user?.ch ?? (Number(store.get('ch', '0')) || 0);
if (DEMO) { wallet = new DemoWallet(); S.user = wallet.user(localName(), localCh()); }
const syncWallet = () => { if (wallet && S.user) S.user = { ...S.user, coins: wallet.coins, adsLeft: Math.max(0, 10 - wallet.adsToday()) }; };

/* ---------------- dom helpers ---------------- */
type Attrs = Record<string, unknown>;
function h(tag: string, attrs: Attrs | null = null, ...kids: (Node | string | null | false | undefined)[]): HTMLElement {
  const el = document.createElement(tag);
  if (attrs) for (const [k, v] of Object.entries(attrs)) {
    if (v === undefined || v === null || v === false) continue;
    if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v as EventListener);
    else if (k === 'class') el.className = String(v);
    else if (k === 'style') el.setAttribute('style', String(v));
    else if (v === true) el.setAttribute(k, '');
    else el.setAttribute(k, String(v));
  }
  for (const c of kids) if (c !== null && c !== false && c !== undefined) el.append(c);
  return el;
}
const btn = (label: string, onclick: () => void, cls = 'btn', extra: Attrs = {}) => h('button', { type: 'button', class: cls, onclick, ...extra }, label);
const $ = (id: string) => document.getElementById(id)!;

/* ---------------- net ---------------- */
const net = new Net(() => store.get('token') || null);
const scene = new TableScene($('scene') as HTMLCanvasElement);
// once the 3D characters are drawn, their faces replace the plain colour dots
scene.onPortraits = urls => {
  const st = document.createElement('style');
  st.textContent = urls.map((u, i) => u ? `.sw[data-ch="${i}"]{background-image:url(${u});background-size:cover;background-position:center}` : '').join('\n');
  document.head.append(st);
};
const voice = new Voice(m => net.send(m));
voice.onChange = () => { renderLabels(true); updateVoiceButtons(); };
(window as unknown as { __voice: Voice }).__voice = voice; // handy for debugging and tests

/* players you blocked: their voice is muted and their emotes and gifts are hidden, in every game */
const blocked = new Set<string>((() => { try { return JSON.parse(store.get('blocked', '[]')) as string[]; } catch { return []; } })());
const saveBlocked = () => store.set('blocked', JSON.stringify([...blocked]));
const isBlockedSeat = (seat: number) => { const p = S.view?.seats[seat]?.pid; return !!p && blocked.has(p); };

net.onStatus(up => { S.online = up; render(); });
net.on(onMessage);

function onMessage(m: S2C) {
  switch (m.t) {
    case 'welcome':
      store.set('token', m.token); S.user = m.user; voice.ice = m.ice ?? [];
      S.login = m.login ?? { google: false, phone: false };
      if (pendingTicket) { net.send({ t: 'ticket', ticket: pendingTicket }); pendingTicket = null; }
      else if (!signedIn(m.user) && (S.login.google || S.login.phone) && !store.get('signinAsked') && !S.view) S.sheet = 'signin'; store.set('name', m.user.name); store.set('ch', String(m.user.ch));
      // carry a name/character picked while offline over to the server
      if (store.get('pendingProfile') === '1') { net.send({ t: 'profile', name: store.get('name'), ch: Number(store.get('ch', '0')) }); store.del('pendingProfile'); }
      break;
    case 'user': S.user = m.user; store.set('name', m.user.name); store.set('ch', String(m.user.ch)); break;
    case 'err':
      S.busy = false;
      if (['not_your_turn', 'not_playing', 'can_play', 'choose_end'].includes(m.code)) break;
      toast(errText(m.code));
      break;
    case 'queue': S.queue = m.mode ? { mode: m.mode, count: m.count, need: m.need, botsAt: m.botsAt, stake: m.stake } : null; break;
    case 'table': if (!S.practice) { S.queue = null; S.busy = false; S.joinCode = ''; applyView(m.view); } return;
    case 'left': if (!S.practice) { voice.leave(); S.view = null; S.sel = null; S.choose = null; S.sheet = null; S.screen = 'home'; } break;
    case 'emote': if (!isBlockedSeat(m.seat)) showEmote(m.seat, m.k); return;
    case 'bonus': toast(t('dailyBonus', { n: m.coins })); sfx.gift(); return;
    case 'gift': if (!isBlockedSeat(m.from)) flyGift(m.from, m.to, m.k); return;
    case 'rtc': voice.onSignal(m.from, m.data).catch(() => {}); return;
    case 'lb': if (m.game === S.lbGame) S.lb = { rows: m.rows, me: m.me }; break;
    case 'coins': S.coinRows = m.rows; break;
    case 'adTicket': playRewarded(m.ticket, m.ssv); return;
    case 'adReward': toast(t('adRewarded', { n: shortCoins(m.coins) })); sfx.gift(); break;
    case 'otp': S.otpPhone = m.phone; toast(t('codeSentTo', { phone: m.phone })); break;
    case 'signedIn':
      S.otpPhone = null; S.phoneDraft = ''; store.set('signinAsked', '1');
      if (S.sheet === 'signin') S.sheet = null;
      toast(t('signedInOk')); sfx.gift();
      break;
    case 'payout':
      S.payout = { ...m, table: S.view?.id ?? '' };
      if (m.kind === 'left') toast(t('payoutLeft', { n: shortCoins(-m.delta) }));
      if (m.kind === 'win') sfx.gift();
      break;
    default: return;
  }
  render();
}

function applyView(v: AnyView) {
  const prev = S.view;
  S.view = v;
  if (!S.practice) {
    voice.sync(v);
    v.seats.forEach((s, i) => { if (s?.pid && blocked.has(s.pid) && !voice.muted.has(i)) voice.setMuted(i, true); });
  }
  if (v.game !== 'domino') { applyBoardView(prev, v); return; }
  // keep a selection only while it is still valid
  if (S.sel !== null && (v.phase !== 'play' || v.turn !== v.you || !v.hand.includes(S.sel))) { S.sel = null; S.choose = null; }
  if (v.last && v.last.k !== S.lastKey && (!prev || prev.id === v.id)) {
    S.lastKey = v.last.k;
    const l = v.last;
    if (l.auto && l.s === v.you && v.you >= 0) toast(t('autoPlayed'));
    else if (l.forced && l.t === 'pass' && l.s === v.you && v.you >= 0) toast(t('autoPassed'));
    if (l.t === 'play') { sfx.place(); scene.animate(l.s, 'play'); }
    else if (l.t === 'draw') { sfx.draw(); scene.animate(l.s, 'draw'); }
    else if (l.t === 'pass') {
      sfx.pass(); scene.animate(l.s, 'pass');
      const who = v.seats[l.s];
      if (who && !isBlockedSeat(l.s)) S.bubbles.set(l.s, { text: 'جلا!', until: Date.now() + 2200 });
    }
    else if (l.t === 'round' || l.t === 'match') {
      const won = v.result && v.result.team >= 0 && v.you >= 0 && v.result.team === teamOf(v.mode, v.you);
      if (l.t === 'match' && v.match && v.you >= 0 && v.match.team === teamOf(v.mode, v.you)) sfx.matchWin(); else if (won) sfx.roundWin(); else sfx.roundLose();
      if (l.s >= 0) scene.animate(l.s, 'win');
    }
  } else if (!prev || prev.id !== v.id) S.lastKey = v.last?.k ?? -1;
  if (v.phase === 'play' && v.turn === v.you && (!prev || prev.turn !== v.you || prev.phase !== 'play')) sfx.yourTurn();
  render();
}

/* ---------------- chess, tawla, ludo ---------------- */
function boardFroms(v: BoardView): number[] {
  if (v.chess) return [...new Set(v.chess.legal.map(m => m[0]))];
  if (v.tawla) return [...new Set(v.tawla.steps.map(m => m[0]))];
  return [];
}
function boardUi(v: BoardView): BoardUi {
  if (v.chess) return { sel: S.boardSel, targets: S.boardSel === null ? [] : v.chess.legal.filter(m => m[0] === S.boardSel).map(m => m[1]) };
  if (v.tawla) return { sel: S.boardSel, targets: S.boardSel === null ? [] : v.tawla.steps.filter(m => m[0] === S.boardSel).map(m => m[1]), movable: S.boardSel === null ? boardFroms(v) : [] };
  if (v.ludo) return { sel: null, targets: v.ludo.movable };
  return { sel: null, targets: [] };
}
function applyBoardView(prev: AnyView | null, v: BoardView) {
  const mine = v.phase === 'play' && v.turn === v.you;
  if (!mine) { S.boardSel = null; S.promo = null; }
  else if (S.boardSel !== null && !boardFroms(v).includes(S.boardSel)) S.boardSel = null;
  if (mine && v.tawla && S.boardSel === null) { const f = boardFroms(v); if (f.length && f.every(x => x === BAR)) S.boardSel = BAR; }
  if (v.last && v.last.k !== S.lastKey && (!prev || prev.id === v.id)) {
    S.lastKey = v.last.k;
    const l = v.last;
    if (l.auto && l.s === v.you && v.you >= 0) toast(t('autoPlayed'));
    if (l.t === 'move' || l.t === 'step' || l.t === 'pawn') {
      if (l.capture || l.hit || l.captured?.length) sfx.capture(); else sfx.place();
      if (v.tawla?.variant === 'mahbousa' && l.hit && v.you >= 0) toast(l.s === v.you ? t('youTrapped') : t('gotTrapped'));
      scene.animate(l.s, 'play');
    } else if (l.t === 'roll') sfx.dice();
    else if (l.t === 'pass') { sfx.pass(); if (l.forced && l.s === v.you) toast(t('noMovesPassed')); }
    else if (l.t === 'end') {
      if (v.phase === 'mend' && v.match && v.you >= 0 && v.match.team === v.you) sfx.matchWin();
      else if (v.result && v.result.winner === v.you) sfx.roundWin(); else sfx.roundLose();
      if (l.s >= 0) scene.animate(l.s, 'win');
    }
  } else if (!prev || prev.id !== v.id) S.lastKey = v.last?.k ?? -1;
  if (mine && (!prev || prev.turn !== v.you || prev.phase !== 'play')) sfx.yourTurn();
  render();
}
/** Taps on the 3D board. */
function handlePick(p: Pick) {
  const v = S.view;
  if (!v || v.game === 'domino' || v.phase !== 'play' || v.turn !== v.you) return;
  unlockAudio();
  if (v.chess && p.kind === 'square') {
    const c = v.chess;
    if (S.boardSel !== null) {
      const mv = c.legal.find(m => m[0] === S.boardSel && m[1] === p.sq);
      if (mv) {
        if (mv[2]) { S.promo = { from: mv[0], to: mv[1] }; return render(); }
        act({ t: 'move', from: mv[0], to: mv[1] }); S.boardSel = null; return render();
      }
    }
    S.boardSel = c.legal.some(m => m[0] === p.sq) && S.boardSel !== p.sq ? p.sq : null;
    return render();
  }
  if (v.tawla && p.kind === 'point') {
    const tw = v.tawla;
    if (!tw.rolled) return;
    if (S.boardSel !== null) {
      const st = tw.steps.find(x => x[0] === S.boardSel && x[1] === p.p);
      if (st) { act({ t: 'step', from: st[0], to: st[1] }); S.boardSel = null; return render(); }
    }
    const dests = [...new Set(tw.steps.filter(x => x[0] === p.p).map(x => x[1]))];
    if (dests.length === 1 && S.boardSel !== p.p) { act({ t: 'step', from: p.p, to: dests[0] }); S.boardSel = null; return render(); }  // only one place to go
    S.boardSel = dests.length && S.boardSel !== p.p ? p.p : null;
    return render();
  }
  if (v.ludo) {
    if (p.kind === 'pawn' && v.ludo.movable.includes(p.i)) act({ t: 'pawn', i: p.i });
    else if (p.kind === 'dice' && v.ludo.die === null) act({ t: 'roll' });
  }
}
scene.onPick = handlePick;
/** Pressing a piece and moving: pick it up and show where it can go. */
scene.onDragStart = p => {
  const v = S.view;
  if (!v || v.game === 'domino' || v.phase !== 'play' || v.turn !== v.you) return false;
  let ok = false;
  if (v.chess && p.kind === 'square') ok = v.chess.legal.some(m => m[0] === p.sq);
  if (v.tawla && p.kind === 'point') ok = !!v.tawla.rolled && v.tawla.steps.some(x => x[0] === p.p);
  if (!ok) return false;
  unlockAudio();
  S.boardSel = p.kind === 'square' ? p.sq : (p as { p: number }).p; S.promo = null;
  render();
  return true;
};
/** Letting go of a dragged piece: move it if that square or point is allowed. */
scene.onDrop = (from, to) => {
  const v = S.view;
  let done = false;
  if (v && v.game !== 'domino' && v.phase === 'play' && v.turn === v.you && to) {
    if (v.chess && from.kind === 'square' && to.kind === 'square') {
      const mv = v.chess.legal.find(m => m[0] === from.sq && m[1] === to.sq);
      if (mv && mv[2]) { S.promo = { from: mv[0], to: mv[1] }; render(); return false; }
      if (mv) { S.boardSel = null; act({ t: 'move', from: mv[0], to: mv[1] }); done = true; }
    }
    if (v.tawla && from.kind === 'point' && to.kind === 'point') {
      const st = v.tawla.steps.find(x => x[0] === from.p && x[1] === to.p);
      if (st) { S.boardSel = null; act({ t: 'step', from: st[0], to: st[1] }); done = true; }
    }
  }
  if (!done) render();   // stays picked up, with its targets showing
  return done;
};

/* ---------------- actions ---------------- */
const inGame = () => !!S.view;
function act(a: Action | BoardAction) {
  if (S.practice) { S.practice.act(a); return; }
  net.send({ t: 'act', a });
}
function sendEmote(k: Emote) {
  S.sheet = null;
  if (S.practice) S.practice.emote(k); else net.send({ t: 'emote', k });
  render();
}
function showEmote(seat: number, k: Emote) {
  S.bubbles.set(seat, { k, until: Date.now() + 2600 });
  scene.animate(seat, 'emote');
  sfx.pop();
}
function quick() {
  unlockAudio();
  if (wallet) return demoMatch(S.mode, S.stake);
  if (!S.online) return toast(t('offline'));
  store.set('mode', S.mode); store.set('game', gameOf(S.mode));
  if ((S.user?.coins ?? 0) < S.stake) return toast(t('needCoins', { n: shortCoins(S.stake) }) + (canWatchAd() ? ' ' + t('orWatchAd') : ''));
  net.send({ t: 'quick', mode: S.mode, stake: S.stake });
  S.queue = { mode: S.mode, count: 1, need: 0, botsAt: 0, stake: S.stake };
  render();
}
/** Demo: put the stake in and play the computer for it. */
function demoMatch(mode: Mode, stake: number) {
  if (!wallet || !wallet.take(stake)) return toast(t('needCoins', { n: shortCoins(stake) }));
  syncWallet();
  store.set('mode', mode); store.set('game', gameOf(mode));
  startPractice(mode, stake);
}
function demoSettle(mode: Mode, stake: number, team: number) {
  if (!wallet || !stake) return;   // a practice game without a stake moves no coins
  const n = MODE_PLAYERS[mode], winners = mode === 'team' ? 2 : 1, w = winnings(stake, winners, n - winners);
  let kind: string, delta: number, fee = 0;
  if (team < 0) { kind = 'draw'; delta = 0; wallet.add('draw', stake, 0, { stake, mode }); }
  else if (team === teamOf(mode, 0)) { kind = 'win'; delta = w.net; fee = w.fee; wallet.add('win', stake + w.net, w.net, { fee, stake, mode }); }
  else { kind = 'loss'; delta = -stake; wallet.add('loss', 0, -stake, { stake, mode }); }
  syncWallet();
  S.payout = { kind, delta, fee, stake, table: S.practice?.table.id ?? '' };
  if (kind === 'win') sfx.gift();
}
/** Walking out of a staked demo match loses the stake, as online. */
function demoForfeit() {
  const v = S.view;
  if (!wallet || !v || !v.stake || (v.phase !== 'play' && v.phase !== 'rend')) return;
  wallet.add('left', 0, -v.stake, { stake: v.stake, mode: v.mode });
  syncWallet();
  toast(t('payoutLeft', { n: shortCoins(v.stake) }));
}
function practiceAgain() {
  afterMatch();
  const v = S.view;
  if (wallet && v?.stake) { if (!wallet.take(v.stake)) return toast(t('needCoins', { n: shortCoins(v.stake) })); syncWallet(); }
  S.practice!.restart();
}
function startPractice(mode = S.mode, stake = 0) {
  unlockAudio();
  stopPractice();
  S.queue = null;
  // the how-to-play recorder (?rec) slows the computer down so every move can be captured
  const pace = /[?&]rec\b/.test(location.search) ? { botDelay: [9000, 9500] as [number, number], turnMs: 600_000 } : undefined;
  S.practice = new Practice(mode, localName(), localCh(), v => applyView(v), (seat, k) => showEmote(seat, k), pace, stake, team => demoSettle(mode, stake, team));
  S.practice.begin();
}
function stopPractice() { S.practice?.stop(); S.practice = null; }
function leaveGame() {
  if (S.view?.phase === 'mend') afterMatch();
  voice.leave();
  S.sheet = null; S.sel = null; S.choose = null; S.screen = 'home';
  if (S.practice) { demoForfeit(); stopPractice(); S.view = null; render(); return; }
  net.send({ t: 'leave' });
  S.view = null; render();
}
const tileName = (id: number) => `${num(TILES[id][0])}|${num(TILES[id][1])}`;
function tapTile(id: number) {
  const v = S.view; if (!v || v.game !== 'domino' || v.phase !== 'play' || v.turn !== v.you) return;
  unlockAudio();
  if (S.sel === id) { S.sel = null; S.choose = null; return render(); }
  if (v.open !== null && id !== v.open) { toast(t('openWith', { tile: tileName(v.open) })); return; }
  const ends = endsFor(v.line, id);
  if (!ends.length) return;
  if (ends.length === 1) { S.sel = null; S.choose = null; act({ t: 'play', id, e: ends[0] }); return; }
  S.sel = id; S.choose = ends; render();
}
function chooseEnd(e: End) {
  if (S.sel === null) return;
  const id = S.sel; S.sel = null; S.choose = null;
  act({ t: 'play', id, e });
  render();
}
scene.onGhost = chooseEnd;

/* dragging a domino from your hand onto an open end of the line */
let tileDrag: null | { id: number; pid: number; x: number; y: number; el: HTMLElement; ghost: HTMLElement | null; ends: End[] } = null;
let dragEndedAt = 0;
function pressTile(e: PointerEvent, id: number, el: HTMLElement) {
  const v = S.view;
  if (tileDrag || !v || v.game !== 'domino' || v.phase !== 'play' || v.turn !== v.you) return;
  const ends = endsFor(v.line, id);
  if (ends.length) tileDrag = { id, pid: e.pointerId, x: e.clientX, y: e.clientY, el, ghost: null, ends };
}
window.addEventListener('pointermove', e => {
  const d = tileDrag; if (!d || d.pid !== e.pointerId) return;
  if (!d.ghost) {
    if (Math.hypot(e.clientX - d.x, e.clientY - d.y) < 10) return;
    unlockAudio();
    const r = d.el.getBoundingClientRect();
    d.ghost = d.el.cloneNode(true) as HTMLElement;
    d.ghost.classList.add('drag-tile'); d.ghost.classList.remove('sel', 'ok', 'dim');
    d.ghost.style.width = r.width + 'px'; d.ghost.style.height = r.height + 'px';
    document.body.append(d.ghost);
    S.sel = d.id; S.choose = d.ends; render();   // glowing spots appear at every end it fits
  }
  const w = d.ghost.offsetWidth, h0 = d.ghost.offsetHeight;
  d.ghost.style.transform = `translate(${e.clientX - w / 2}px, ${e.clientY - h0 * 0.75}px) scale(1.2)`;
  d.ghost.classList.toggle('over', !!scene.ghostAt(e.clientX, e.clientY));
});
const dropTile = (e: PointerEvent) => {
  const d = tileDrag; if (!d || d.pid !== e.pointerId) return;
  tileDrag = null;
  if (!d.ghost) return;   // just a tap: the click plays it
  d.ghost.remove(); dragEndedAt = Date.now();
  let end = e.type === 'pointerup' ? scene.ghostAt(e.clientX, e.clientY) : null;
  // with only one place to go, letting go anywhere over the table plays it
  if (!end && e.type === 'pointerup' && d.ends.length === 1 && e.clientY < window.innerHeight - chrome.bottom) end = d.ends[0];
  S.sel = null; S.choose = null;
  const v = S.view;
  if (end && v && v.game === 'domino' && v.phase === 'play' && v.turn === v.you) act({ t: 'play', id: d.id, e: end });
  render();
};
window.addEventListener('pointerup', dropTile);
window.addEventListener('pointercancel', dropTile);

async function shareRoom(code: string) {
  const text = t('shareText', { code: code.toUpperCase(), game: t(('game_' + gameOf(S.view?.mode ?? S.mode)) as Key) });
  const url = Capacitor.isNativePlatform() ? undefined : `${location.origin}/#${code}`;
  try { await Share.share({ title: t('brand'), text, url }); }
  catch { copyText(code.toUpperCase()); }
}
function copyText(s: string) {
  navigator.clipboard?.writeText(s).then(() => toast(t('copied')), () => toast(s));
}

let toastTimer: ReturnType<typeof setTimeout>;
function toast(msg: string) {
  const el = $('toast'); el.textContent = msg; el.hidden = false;
  clearTimeout(toastTimer); toastTimer = setTimeout(() => { el.hidden = true; }, 3200);
}

/* ---------------- naming ---------------- */
function seatName(v: AnyView, i: number) {
  const s = v.seats[i]; if (!s) return '';
  if (s.bot) return CHAR_NAMES[getLang()][s.ch];
  return s.name || CHAR_NAMES[getLang()][s.ch];
}
const teamName = (team: number) => t(team === 0 ? 'teamA' : 'teamB');
function sideName(v: TableView, team: number) {
  if (v.mode === 'team') return teamName(team);
  return seatName(v, team);
}

/* ---------------- rendering ---------------- */
function render() {
  const ui = $('ui'); ui.innerHTML = '';
  const v = S.view;
  if (v) {
    scene.showTable(v, S.sel, S.choose, v.game === 'domino' ? undefined : boardUi(v));
    if (v.phase === 'lobby') ui.append(roomScreen(v)); else ui.append(...(v.game === 'domino' ? gameScreen(v) : boardScreen(v)));
    if (S.promo && v.game === 'chess') ui.append(promoSheet(v));
  } else {
    scene.showHome(localCh());
    if (S.queue) ui.append(searchScreen());
    else ui.append(topBar(), ({ home: homeScreen, friends: friendsScreen, lb: lbScreen, settings: settingsScreen, rules: rulesScreen, coins: coinsScreen })[S.screen]());
  }
  if (S.sheet === 'leave') ui.append(sheet(h('p', null, t('leaveConfirm')), v && v.stake ? h('p', { class: 'bad' }, t('leaveStake', { n: shortCoins(v.stake) })) : h('span'), h('div', { class: 'row' }, btn(t('stay'), () => { S.sheet = null; render(); }, 'btn alt'), btn(t('leaveYes'), leaveGame, 'btn danger'))));
  if (S.sheet === 'emotes') ui.append(emoteSheet());
  if (S.sheet === 'signin' && !v) ui.append(signInSheet());
  if (S.sheet === 'fakead') ui.append(fakeAdSheet());
  if (S.sheet === 'player' && v) { const ps = playerSheet(v); if (ps) ui.append(ps); }
  if (S.sheet === 'delete') ui.append(sheet(h('p', null, t('deleteConfirm')), h('div', { class: 'row' }, btn(t('stay'), () => { S.sheet = null; render(); }, 'btn alt'),
    btn(t('deleteYes'), () => { net.send({ t: 'delete' }); S.sheet = null; S.screen = 'home'; toast(t('deleted')); render(); }, 'btn danger'))));
  if (!S.online && net.ready === false && inGame() && !S.practice) ui.append(h('div', { class: 'banner' }, t('reconnecting')));
  measureChrome();
  renderLabels(true);
  const rot = document.getElementById('rotate');
  if (rot) { rot.querySelector('b')!.textContent = t('rotateTitle'); rot.querySelector('span')!.textContent = t('rotateHint'); }
}

function topBar() {
  const u = S.user;
  return h('header', { class: 'topbar' },
    S.screen !== 'home' ? btn('‹', () => { S.screen = 'home'; render(); }, 'iconbtn back', { 'aria-label': t('home') }) : h('h1', { class: 'brand' }, t('brand')),
    h('div', { class: 'grow' }),
    btn(getLang() === 'ar' ? 'EN' : 'ع', () => { const l: Lang = getLang() === 'ar' ? 'en' : 'ar'; setLang(l); store.set('lang', l); render(); }, 'chip', { 'aria-label': t('language') }),
    h('button', { type: 'button', class: 'chip profile', onclick: () => { S.screen = 'settings'; render(); } },
      h('span', { class: 'sw', 'data-ch': localCh(), style: `background-color:${charColor(localCh())}` }),
      h('span', { class: 'pname' }, localName()),
      u ? h('span', { class: 'muted' }, num(u.ratings?.[S.game]?.rating ?? 1000)) : null),
    u ? btn('🪙 ' + num(u.coins), openCoins, 'chip coins', { 'aria-label': t('coinsTitle') }) : null,
    u ? adButton() : null,
  );
}

const GAME_ICONS: Record<GameId, string> = {
  domino: '<svg viewBox="0 0 48 48" aria-hidden="true"><rect x="13" y="3" width="22" height="42" rx="4" fill="#f4ecd8" stroke="#2a1d14" stroke-width="2"/><line x1="15" y1="24" x2="33" y2="24" stroke="#2a1d14" stroke-width="2"/><g fill="#2a1d14"><circle cx="19" cy="9" r="2.2"/><circle cx="29" cy="18" r="2.2"/><circle cx="19" cy="30" r="2.2"/><circle cx="29" cy="30" r="2.2"/><circle cx="19" cy="39" r="2.2"/><circle cx="29" cy="39" r="2.2"/></g></svg>',
  tawla: '<svg viewBox="0 0 48 48" aria-hidden="true"><rect x="3" y="7" width="42" height="34" rx="4" fill="#7a4a26" stroke="#2a1d14" stroke-width="2"/><line x1="24" y1="7" x2="24" y2="41" stroke="#2a1d14" stroke-width="2"/><g fill="#e8d6a8"><path d="M7 9l3 13 3-13z"/><path d="M15 9l3 13 3-13z"/><path d="M27 39l3-13 3 13z"/><path d="M35 39l3-13 3 13z"/></g><circle cx="12" cy="34" r="4" fill="#f4ecd8" stroke="#2a1d14"/><circle cx="36" cy="14" r="4" fill="#2a1d14" stroke="#e8d6a8"/></svg>',
  chess: '<svg viewBox="0 0 48 48" aria-hidden="true"><path d="M16 42h16l-2-6c4-2 6-6 5-11l-5-4 2-4-3-2-3 3c-6 1-10 6-10 12l5-2-1 6z" fill="#f4ecd8" stroke="#2a1d14" stroke-width="2" stroke-linejoin="round"/><rect x="13" y="41" width="22" height="4" rx="1.5" fill="#2a1d14"/><circle cx="25" cy="18" r="1.4" fill="#2a1d14"/></svg>',
  ludo: '<svg viewBox="0 0 48 48" aria-hidden="true"><rect x="4" y="4" width="18" height="18" rx="3" fill="#c8402f"/><rect x="26" y="4" width="18" height="18" rx="3" fill="#2f8a4a"/><rect x="26" y="26" width="18" height="18" rx="3" fill="#e0b030"/><rect x="4" y="26" width="18" height="18" rx="3" fill="#2f6fb8"/><circle cx="24" cy="24" r="6" fill="#f4ecd8" stroke="#2a1d14" stroke-width="2"/></svg>',
};
function pickGame(g: GameId) {
  S.game = g; store.set('game', g);
  if (gameOf(S.mode) !== g) { S.mode = GAME_MODES[g][0]; store.set('mode', S.mode); }
  S.target = defaultTarget(S.mode);
  render();
}
function gamePicker() {
  return h('div', { class: 'games', role: 'radiogroup', 'aria-label': t('chooseGame') }, ...GAMES.map(g => {
    const b = h('button', { type: 'button', class: 'game', role: 'radio', 'aria-checked': String(S.game === g), onclick: () => pickGame(g) },
      h('span', { class: 'gicon' }), h('b', null, t(('game_' + g) as Key)), h('span', { class: 'small' }, t(('gameHint_' + g) as Key)));
    (b.firstChild as HTMLElement).innerHTML = GAME_ICONS[g];
    return b;
  }));
}
const targetLabel = (m: Mode, n: number) => gameOf(m) === 'tawla' ? t('pointsTo', { n }) : t('points', { n });

/** What a win and a loss mean in coins for a mode and stake. */
function stakeOutcome(mode: Mode, stake: number) {
  const n = MODE_PLAYERS[mode], winners = mode === 'team' ? 2 : 1;
  return { win: winnings(stake, winners, n - winners).net, lose: stake };
}
/** The stake: a one-row stepper from 5K to 1M, with what a win and a loss mean; stakes you can't cover are skipped. */
function stakePicker(mode: Mode, cur = S.stake, pick = (n: number) => { S.stake = n; store.set('stake', String(n)); render(); }, canEdit = true) {
  const coins = S.user?.coins ?? 0;
  const o = stakeOutcome(mode, cur);
  const i = STAKES.indexOf(cur as typeof STAKES[number]);
  const prev = i > 0 ? STAKES[i - 1] : null, next = i < STAKES.length - 1 ? STAKES[i + 1] : null;
  const afford = (n: number | null) => n !== null && (!S.user || coins >= n);
  return h('div', { class: 'stake' },
    h('span', { class: 'lab' }, t('stakeLabel')),
    h('div', { class: 'stepper', role: 'group', 'aria-label': t('stakeLabel') },
      btn('−', () => prev !== null && pick(prev), 'iconbtn step', { disabled: !canEdit || prev === null, 'aria-label': '−' }),
      h('b', { class: 'stake-val', dir: 'ltr', 'aria-live': 'polite' }, '🪙 ' + shortCoins(cur)),
      btn('+', () => next !== null && pick(next), 'iconbtn step', { disabled: !canEdit || !afford(next), 'aria-label': '+' })),
    h('span', { class: 'small stake-odds', dir: 'ltr' }, h('span', { class: 'good' }, '+' + shortCoins(o.win)), ' / ', h('span', { class: 'bad' }, '−' + shortCoins(o.lose))));
}

function openCoins() { S.screen = 'coins'; S.coinRows = wallet ? wallet.rows.slice() : null; if (S.online && !wallet) net.send({ t: 'coins' }); render(); }
function coinsScreen() {
  const u = S.user;
  const rows = S.coinRows ?? [];
  const when = (ts: number) => new Date(ts).toLocaleString(getLang() === 'ar' ? 'ar-EG' : 'en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  const label = (r: CoinRow) => {
    const what = t(('coin_' + r.kind) as Key);
    const game = r.mode && MODES.includes(r.mode as Mode) ? t(('game_' + gameOf(r.mode as Mode)) as Key) + ' · ' + t(('mode_' + r.mode) as Key) : '';
    return [what, game, r.stake ? t('stakeTag', { n: shortCoins(r.stake) }) : '', r.fee ? t('feeNote', { n: shortCoins(r.fee) }) : ''].filter(Boolean).join(' · ');
  };
  return h('section', { class: 'sheet-card tall coins-page' },
    h('div', { class: 'row between' }, h('h2', null, t('coinsTitle')), u ? h('b', { class: 'coins big-coins' }, '🪙 ' + num(u.coins)) : null),
    h('p', { class: 'muted small' }, t('coinsHow')),
    canWatchAd() ? h('div', { class: 'row' }, adButton('btn tq', true), h('span', { class: 'muted small' }, t('adsLeft', { n: S.user!.adsLeft }))) : null,
    h('p', { class: 'lab' }, t('coinsHistory')),
    !S.online && !wallet ? h('p', { class: 'muted' }, t('offline')) : !S.coinRows ? h('p', { class: 'muted' }, t('connecting')) : !rows.length ? h('p', { class: 'muted' }, t('coinsEmpty')) :
      h('ol', { class: 'coin-log' }, ...rows.map(r => h('li', null,
        h('span', { class: 'ci' }, r.delta > 0 ? '▲' : r.delta < 0 ? '▼' : '•'),
        h('span', { class: 'cl' }, h('b', null, label(r)), h('span', { class: 'muted small' }, when(r.ts) + ' · ' + t('balanceAfter', { n: num(r.balance) }))),
        h('b', { class: 'cd ' + (r.delta > 0 ? 'good' : r.delta < 0 ? 'bad' : 'muted'), dir: 'ltr' }, (r.delta > 0 ? '+' : r.delta < 0 ? '−' : '') + num(Math.abs(r.delta)))))));
}
/** The coin line on a result card, once the server has settled the stakes. */
function payoutLine(v: AnyView): HTMLElement | null {
  const p = S.payout;
  if (!p || p.table !== v.id || v.phase !== 'mend') return null;
  const n = shortCoins(Math.abs(p.delta));
  const text = p.kind === 'win' ? t('payoutWin', { n, fee: shortCoins(p.fee) }) : p.kind === 'loss' ? t('payoutLoss', { n }) : p.kind === 'draw' ? t('payoutDraw', { n: shortCoins(p.stake) }) : t('payoutLeft', { n });
  return h('p', { class: 'payout ' + (p.delta > 0 ? 'good' : p.delta < 0 ? 'bad' : 'muted') }, '🪙 ' + text);
}

function homeScreen() {
  const modes = GAME_MODES[S.game];
  const modeBtns = modes.length < 2 ? null : h('div', { class: 'modes', role: 'radiogroup' }, ...modes.map(m =>
    h('button', { type: 'button', class: 'mode', role: 'radio', 'aria-checked': String(S.mode === m), onclick: () => { S.mode = m; store.set('mode', m); render(); } },
      h('b', null, t(('mode_' + m) as Key)), h('span', null, t(('modeHint_' + m) as Key)))));
  // on landscape screens: what to play on one side, how to play it on the other, links under the first
  return h('section', { class: 'sheet-card home' },
    h('div', { class: 'home-pick' },
      gamePicker(),
      modeBtns ?? h('p', { class: 'muted center small' }, t(('modeHint_' + S.mode) as Key))),
    h('div', { class: 'home-act' },
      stakePicker(S.mode),
      btn(t('quick'), quick, 'btn big', { disabled: !S.online && !DEMO }),
      DEMO ? h('p', { class: 'note' }, t('demoNote')) : !S.online ? h('p', { class: 'note' }, net.ready ? t('connecting') : t('offline')) : null,
      h('div', { class: 'row' },
        btn(t('friends'), () => { S.screen = 'friends'; render(); }, 'btn tq', { disabled: !S.online }),
        btn(t('practice'), () => startPractice(), 'btn alt'))),
    h('div', { class: 'row links home-links' },
      btn(t('coinsTitle'), openCoins, 'link'),
      btn(t('leaderboard'), () => openLb(S.game), 'link'),
      btn(t('rules'), () => { S.screen = 'rules'; render(); }, 'link')),
  );
}

function searchScreen() {
  const q = S.queue!;
  const secs = q.botsAt ? Math.max(0, Math.ceil((q.botsAt - net.serverNow()) / 1000)) : 0;
  return h('section', { class: 'sheet-card center' },
    h('div', { class: 'spinner', 'aria-hidden': 'true' }, h('span'), h('span')),
    h('h2', null, t('searching')),
    h('p', { class: 'muted' }, t(('game_' + gameOf(q.mode)) as Key) + ' · ' + t(('mode_' + q.mode) as Key) + (q.stake ? ' · 🪙 ' + shortCoins(q.stake) : '')),
    q.need ? h('p', { id: 'q-count' }, t('found', { n: q.count, m: q.need })) : null,
    q.botsAt ? h('p', { class: 'muted', id: 'q-bots' }, t('botsIn', { s: secs })) : null,
    btn(t('cancel'), () => { net.send({ t: 'cancel' }); S.queue = null; render(); }, 'btn alt'));
}

function chips<T extends string | number>(values: readonly T[], cur: T, label: (v: T) => string, pick: (v: T) => void, disabled = false) {
  return h('div', { class: 'chips', role: 'radiogroup' }, ...values.map(v =>
    h('button', { type: 'button', class: 'chip', role: 'radio', 'aria-checked': String(v === cur), disabled, onclick: () => pick(v) }, label(v))));
}

function friendsScreen() {
  const code = h('input', { type: 'text', id: 'join-code', maxlength: '5', placeholder: t('code'), autocomplete: 'off', autocapitalize: 'characters', value: S.joinCode }) as HTMLInputElement;
  code.addEventListener('input', () => { code.value = code.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 5); S.joinCode = code.value; });
  const join = () => { if (code.value.length === 5) { unlockAudio(); net.send({ t: 'join', code: code.value.toLowerCase() }); } };
  code.addEventListener('keydown', e => { if (e.key === 'Enter') join(); });
  return h('section', { class: 'sheet-card' },
    h('h2', null, t('joinRoom')),
    h('div', { class: 'row' }, code, btn(t('join'), join, 'btn tq')),
    h('hr'),
    h('h2', null, t('createRoom')),
    gamePicker(),
    GAME_MODES[S.game].length > 1 ? chips(GAME_MODES[S.game], S.mode, m => t(('mode_' + m) as Key), m => { S.mode = m; S.target = defaultTarget(m); render(); }) : null,
    targetOptions(S.mode).length > 1 ? h('label', { class: 'lab' }, t('target')) : null,
    targetOptions(S.mode).length > 1 ? chips(targetOptions(S.mode), S.target, n => num(n), n => { S.target = n; render(); }) : null,
    stakePicker(S.mode),
    btn(t('create'), () => {
      unlockAudio();
      if ((S.user?.coins ?? 0) < S.stake) return toast(t('needCoins', { n: shortCoins(S.stake) }));
      net.send({ t: 'create', mode: S.mode, target: S.target, stake: S.stake });
    }, 'btn big'));
}

function openLb(g: GameId) {
  S.screen = 'lb'; S.lbGame = g; S.lb = null; net.send({ t: 'lb', game: g }); render();
}
function lbScreen() {
  const rows = S.lb?.rows ?? [];
  const row = (r: LeaderRow) => h('li', { class: r.me ? 'me' : '' },
    h('span', { class: 'rank' }, num(r.rank)),
    h('span', { class: 'sw', 'data-ch': r.ch, style: `background-color:${charColor(r.ch)}` }),
    h('span', { class: 'nm' }, r.name),
    h('span', { class: 'muted small' }, t('wins', { n: r.wins }) + ' · ' + t('games', { n: r.games })),
    h('b', null, num(r.rating)));
  return h('section', { class: 'sheet-card tall' },
    h('h2', null, t('lbTitle')),
    chips(GAMES, S.lbGame, g => t(('game_' + g) as Key), g => openLb(g)),
    !S.lb ? h('p', { class: 'muted' }, t('connecting')) : rows.length ? h('ol', { class: 'lb' }, ...rows.map(row)) : h('p', { class: 'muted' }, t('lbEmpty')),
    S.lb?.me && !rows.some(r => r.me) ? h('div', null, h('p', { class: 'lab' }, t('yourRank')), h('ol', { class: 'lb' }, row(S.lb.me))) : null);
}

/** A short looping recording of the game being played, with captions, in the player's language. */
function howToGif(g: GameId): HTMLElement {
  const img = h('img', { src: `howto/${g}-${getLang()}.gif`, alt: t('howToTitle') + ' · ' + t(('game_' + g) as Key), width: '300', height: '420', loading: 'lazy', decoding: 'async' }) as HTMLImageElement;
  const fig = h('figure', { class: 'howto' }, img, h('figcaption', null, t('howToTitle')));
  img.addEventListener('error', () => fig.remove());
  return fig;
}
/** Rules lists hold section titles as lines starting with '## '. */
function ruleSections(g: GameId): HTMLElement[] {
  const out: HTMLElement[] = [];
  let ul: HTMLElement | null = null;
  for (const line of rules(g)) {
    if (line.startsWith('## ')) { out.push(h('h3', { class: 'rules-h' }, line.slice(3))); ul = null; continue; }
    if (!ul) { ul = h('ul', { class: 'rules' }); out.push(ul); }
    ul.append(h('li', null, line));
  }
  return out;
}
function rulesScreen() {
  return h('section', { class: 'sheet-card tall' }, h('h2', null, t('rules')),
    chips(GAMES, S.game, g => t(('game_' + g) as Key), g => pickGame(g)),
    howToGif(S.game),
    ...ruleSections(S.game));
}

/* ---------------- rewarded ads ---------------- */
let adBusy = false;
/** Ads pay on the phone apps (and in the demo, with a stand-in ad). */
const canWatchAd = () => (adsSupported() || !!wallet) && (S.user?.adsLeft ?? 0) > 0;
function watchAd() {
  unlockAudio();
  if (adBusy) return;
  if (wallet) { S.sheet = 'fakead'; render(); return; }
  if (!adsSupported()) return toast(t('adsAppOnly'));
  if (!S.online) return toast(t('offline'));
  adBusy = true;
  net.send({ t: 'adStart' });
  setTimeout(() => { adBusy = false; }, 15_000);
}
async function playRewarded(ticket: string, ssv: boolean) {
  const ok = await showRewarded(S.user?.id ?? '', ticket);
  adBusy = false;
  if (!ok) return toast(t('adNotFinished'));
  // with server-side verification the server pays when Google confirms; otherwise the app reports it
  if (!ssv) net.send({ t: 'adWatched', ticket });
}
/** The coin button for an ad: "🎬 +5K". */
function adButton(cls = 'chip ad-chip', long = false) {
  if (!canWatchAd()) return null;
  return btn(long ? '🎬 ' + t('watchAd', { n: shortCoins(5_000) }) : '🎬 +' + shortCoins(5_000), watchAd, cls, { 'aria-label': t('watchAd', { n: shortCoins(5_000) }), title: t('watchAd', { n: shortCoins(5_000) }) });
}
/** Demo only: a stand-in for the video, five seconds long. */
function fakeAdSheet() {
  const box = h('div', { class: 'card fakead' }, h('span', { class: 'tag' }, t('adTag')), h('h2', null, t('brand')), h('p', { class: 'muted' }, t('demoAdText')));
  const count = h('b', { class: 'big' }, '5');
  const close = btn(t('close'), () => {
    S.sheet = null;
    if (wallet && wallet.adsToday() < 10) { wallet.add('ad', 5_000, 5_000); syncWallet(); toast(t('adRewarded', { n: shortCoins(5_000) })); sfx.gift(); }
    render();
  }, 'btn', { disabled: true });
  box.append(count, close);
  let n = 5;
  const tick = setInterval(() => {
    n--; count.textContent = String(Math.max(0, n));
    if (n <= 0) { clearInterval(tick); (close as HTMLButtonElement).disabled = false; count.textContent = '✓'; }
  }, 1000);
  return h('div', { class: 'overlay' }, box);
}

/* ---------------- signing in: Google and mobile number ---------------- */
let pendingTicket: string | null = null;
const signedIn = (u: PublicUser | null) => !!u && !!(u.google || u.phone || u.email);
function sendTicket(ticket: string) {
  if (net.ready) net.send({ t: 'ticket', ticket }); else pendingTicket = ticket;
}
// the web version comes back from Google as ?ticket=…
{
  const q = new URLSearchParams(location.search), tk = q.get('ticket');
  if (tk) { pendingTicket = tk; q.delete('ticket'); history.replaceState(null, '', location.pathname + (q.toString() ? '?' + q : '') + location.hash); }
}
// the phone apps come back as ahwagames://auth?ticket=…
CapApp.addListener('appUrlOpen', ({ url }: { url: string }) => {
  if (!url.startsWith('ahwagames://auth')) return;
  const tk = new URL(url.replace(/^ahwagames:\/\//, 'https://x/')).searchParams.get('ticket');
  Browser.close().catch(() => {});
  if (tk) sendTicket(tk);
});
function googleSignIn() {
  unlockAudio();
  if (!S.online) return toast(t('offline'));
  store.set('signinAsked', '1');
  const native = Capacitor.isNativePlatform();
  const url = httpBase() + '/auth/google/start?to=' + (native ? 'app' : 'web');
  if (native) Browser.open({ url }).catch(() => toast(t('err_generic'))); else location.href = url;
}
const GOOGLE_G = '<svg viewBox="0 0 48 48" width="18" height="18" aria-hidden="true"><path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z"/><path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z"/><path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-7.9l-6.5 5C9.5 39.6 16.2 44 24 44z"/><path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z"/></svg>';
/** The sign-in choices: Google, and a mobile number with a code by SMS. */
function signInOptions(): HTMLElement {
  const u = S.user;
  const box = h('div', { class: 'signin stack' });
  if (S.login.google && !u?.google) {
    const g = btn(t('withGoogle'), googleSignIn, 'btn google', { disabled: !S.online });
    g.insertAdjacentHTML('afterbegin', GOOGLE_G);
    box.append(g);
  }
  if (S.login.phone && !u?.phone) {
    if (!S.otpPhone) {
      const tel = h('input', { type: 'tel', id: 'phone', inputmode: 'tel', autocomplete: 'tel', placeholder: '01X XXXX XXXX', dir: 'ltr', value: S.phoneDraft, 'aria-label': t('phoneLabel') }) as HTMLInputElement;
      tel.addEventListener('input', () => { S.phoneDraft = tel.value; });
      const send = () => { if (tel.value.trim()) net.send({ t: 'otp', phone: tel.value }); };
      tel.addEventListener('keydown', e => { if (e.key === 'Enter') send(); });
      box.append(h('label', { class: 'lab', for: 'phone' }, t('phoneLabel')), h('div', { class: 'row' }, tel, btn(t('sendCode'), send, 'btn tq', { disabled: !S.online })));
    } else {
      const code = h('input', { type: 'text', id: 'otp', inputmode: 'numeric', autocomplete: 'one-time-code', maxlength: '6', placeholder: '••••••', dir: 'ltr', 'aria-label': t('codeLabel') }) as HTMLInputElement;
      const check = () => net.send({ t: 'otpCheck', phone: S.otpPhone!, code: code.value });
      code.addEventListener('input', () => { code.value = code.value.replace(/[^0-9٠-٩]/g, '').slice(0, 6); if (code.value.length === 6) check(); });
      box.append(h('p', { class: 'muted small' }, t('codeSentTo', { phone: S.otpPhone })),
        h('div', { class: 'row' }, code, btn(t('confirm'), check, 'btn tq')),
        h('div', { class: 'row links' },
          btn(t('resendCode'), () => net.send({ t: 'otp', phone: S.otpPhone! }), 'link'),
          btn(t('changeNumber'), () => { S.otpPhone = null; render(); }, 'link')));
      setTimeout(() => code.focus(), 50);
    }
  }
  return box;
}
function signInSheet() {
  const later = () => { store.set('signinAsked', '1'); S.sheet = null; render(); };
  return h('div', { class: 'overlay' }, h('div', { class: 'card signin-card' },
    h('h2', null, t('signInTitle')), h('p', { class: 'muted' }, t('signInHint')),
    signInOptions(),
    btn(t('playAsGuest'), later, 'link')));
}

function settingsScreen() {
  const u = S.user;
  const name = h('input', { type: 'text', id: 'set-name', maxlength: '14', value: localName(), autocomplete: 'nickname' }) as HTMLInputElement;
  const saveProfile = (ch = localCh()) => {
    const nm = name.value.trim().slice(0, 14) || localName();
    store.set('name', nm); store.set('ch', String(ch));
    if (S.online) net.send({ t: 'profile', name: nm, ch }); else store.set('pendingProfile', '1');
    if (S.user) S.user = { ...S.user, name: nm, ch };
  };
  name.addEventListener('change', () => { saveProfile(); toast(t('saved')); });
  const charGrid = h('div', { class: 'chars' }, ...CHARS.map((_, i) =>
    h('button', { type: 'button', class: 'chr', 'aria-pressed': String(localCh() === i), onclick: () => { saveProfile(i); render(); } },
      h('span', { class: 'sw', 'data-ch': i, style: `background-color:${charColor(i)}` }), h('span', null, CHAR_NAMES[getLang()][i]))));
  const toggle = (label: string, on: boolean, set: (v: boolean) => void) => h('div', { class: 'setrow' }, h('span', null, label),
    chips(['on', 'off'] as const, on ? 'on' : 'off', v => t(v), v => { set(v === 'on'); render(); }));
  const account = h('div', { class: 'stack' });
  if (signedIn(u)) {
    if (u!.google) account.append(h('p', null, '✓ ' + t('linkedGoogle', { v: u!.google })));
    if (u!.phone) account.append(h('p', { dir: 'auto' }, '✓ ' + t('linkedPhone', { v: u!.phone })));
    if (u!.email) account.append(h('p', null, '✓ ' + t('signedInAs', { email: u!.email })));
  } else account.append(h('p', { class: 'muted' }, t('guestNote')));
  // add the sign-in ways this account doesn't have yet (another phone can then open it with either)
  account.append(signInOptions());
  if (!u?.email) {
    const email = h('input', { type: 'email', id: 'acc-email', placeholder: t('email'), autocomplete: 'email' }) as HTMLInputElement;
    const pw = h('input', { type: 'password', id: 'acc-pw', placeholder: t('password'), autocomplete: 'current-password' }) as HTMLInputElement;
    account.append(h('details', { class: 'use-email' }, h('summary', null, t('useEmail')), h('div', { class: 'stack' }, email, pw,
      h('div', { class: 'row' },
        btn(t('register'), () => net.send({ t: 'register', email: email.value, pw: pw.value }), 'btn', { disabled: !S.online }),
        btn(t('login'), () => net.send({ t: 'login', email: email.value, pw: pw.value }), 'btn alt', { disabled: !S.online })))));
  }
  if (signedIn(u)) account.append(btn(t('logout'), () => { store.del('token'); S.user = null; net.reconnect(); render(); }, 'btn alt'));
  return h('section', { class: 'sheet-card tall' },
    h('h2', null, t('settings')),
    h('label', { class: 'lab', for: 'set-name' }, t('name')), name,
    h('span', { class: 'lab' }, t('character')), charGrid,
    h('div', { class: 'setrow' }, h('span', null, t('language')),
      chips(['ar', 'en'] as Lang[], getLang(), l => (l === 'ar' ? 'العربية' : 'English'), l => { setLang(l); store.set('lang', l); render(); })),
    toggle(t('sound'), prefs.sound, x => { prefs.sound = x; store.set('sound', x ? '1' : '0'); }),
    toggle(t('vibration'), prefs.vibrate, x => { prefs.vibrate = x; store.set('vibrate', x ? '1' : '0'); }),
    h('hr'), h('h2', null, t('account')), account,
    u ? h('p', { class: 'muted small' }, `${t('games', { n: u.games })} · ${t('wins', { n: u.wins })} · 🪙 ${num(u.coins)} · ${t('giftsReceived', { n: u.gifts })}`) : null,
    u ? h('div', { class: 'ratings' }, h('span', { class: 'lab' }, t('ratingsTitle')), ...GAMES.map(g => {
      const r = u.ratings?.[g];
      return h('div', { class: 'setrow' }, h('span', null, t(('game_' + g) as Key)),
        h('span', { class: 'muted small' }, r ? `${num(r.rating)} · ${t('games', { n: r.games })} · ${t('wins', { n: r.wins })}` : num(1000)));
    })) : null,
    blocked.size ? btn(t('unblockAll', { n: blocked.size }), () => { blocked.clear(); saveBlocked(); toast(t('saved')); render(); }, 'link') : null,
    u ? btn(t('deleteAccount'), () => { S.sheet = 'delete'; render(); }, 'link danger-link') : null);
}

function roomScreen(v: AnyView) {
  const host = v.you >= 0 && v.seats[v.you]?.host;
  const seats = h('div', { class: 'seats' }, ...v.seats.map((s, i) => {
    const row = h('div', { class: 'seat' },
      h('span', { class: 'sw', 'data-ch': s ? s.ch : undefined, style: s ? `background-color:${charColor(s.ch)}` : 'border:1px dashed var(--line)' }),
      h('span', { class: 'nm' + (s ? '' : ' muted') }, s ? seatName(v, i) : t('empty')),
      v.mode === 'team' ? h('span', { class: 'tag team' + (i % 2) }, teamName(i % 2)) : null,
      s?.host ? h('span', { class: 'tag hl' }, t('host')) : null,
      i === v.you ? h('span', { class: 'tag hl' }, t('you')) : null,
      s?.bot ? h('span', { class: 'tag' }, t('bot')) : null);
    if (host && !s) row.append(btn(t('addBot'), () => net.send({ t: 'bot', seat: i }), 'btn small alt'));
    if (host && s && !s.host) row.append(btn(t('remove'), () => net.send({ t: 'kick', seat: i }), 'btn small alt'));
    return row;
  }));
  const full = v.seats.every(Boolean);
  return h('section', { class: 'sheet-card tall' },
    h('div', { class: 'row between' }, h('h2', null, t('room')), btn(t('leave'), leaveGame, 'btn small alt')),
    h('div', { class: 'codebig', dir: 'ltr' }, (v.code || '').toUpperCase()),
    h('div', { class: 'row' }, btn(t('share'), () => shareRoom(v.code || ''), 'btn tq'), btn(t('copy'), () => copyText((v.code || '').toUpperCase()), 'btn alt')),
    h('p', { class: 'lab' }, t(('game_' + gameOf(v.mode)) as Key)),
    host && GAME_MODES[gameOf(v.mode)].length > 1 ? chips(GAME_MODES[gameOf(v.mode)], v.mode, m => t(('mode_' + m) as Key), m => net.send({ t: 'setup', mode: m }))
      : h('p', { class: 'muted' }, t(('mode_' + v.mode) as Key) + (gameOf(v.mode) === 'domino' || gameOf(v.mode) === 'tawla' ? ' · ' + t('to', { n: v.target }) : '')),
    host && targetOptions(v.mode).length > 1 ? chips(targetOptions(v.mode), v.target, n => targetLabel(v.mode, n), n => net.send({ t: 'setup', target: n })) : null,
    v.stake ? stakePicker(v.mode, v.stake, n => net.send({ t: 'setup', stake: n }), host) : null,
    seats,
    host ? btn(t('start'), () => { unlockAudio(); net.send({ t: 'start' }); }, 'btn big', { disabled: !full }) : h('p', { class: 'muted center' }, t('waitHost')),
    host && !full ? h('p', { class: 'muted small center' }, t('fillSeats')) : null);
}

function gameScreen(v: TableView): HTMLElement[] {
  const myTurn = v.phase === 'play' && v.turn === v.you;
  const teams = v.scores.map((sc, team) => {
    const active = v.phase === 'play' && v.turn >= 0 && teamOf(v.mode, v.turn) === team;
    const seat = v.mode === 'team' ? -1 : team;
    const color = seat >= 0 && v.seats[seat] ? charColor(v.seats[seat]!.ch) : (team === 0 ? 'var(--brass)' : 'var(--tq)');
    return h('div', { class: 'sc' + (active ? ' on' : '') }, h('span', { class: 'sw', style: `background:${color}` }), h('span', { class: 'nm' }, sideName(v, team)), h('b', null, num(sc)));
  });
  const hud = h('header', { class: 'hud' },
    h('div', { class: 'row' },
      btn('‹', () => { if ((S.practice && !v.stake) || v.phase !== 'play') leaveGame(); else { S.sheet = 'leave'; render(); } }, 'iconbtn back', { 'aria-label': t('leave') }),
      h('div', { class: 'scores' }, ...teams)),
    h('div', { class: 'meta muted small' }, `${t('round', { n: v.round })} · ${t('to', { n: v.target })}` + (v.mode === 'ffa2' || v.mode === 'ffa3' ? ' · ' + t('boneyard', { n: v.yard }) : '') + (v.stake ? ' · 🪙 ' + shortCoins(v.stake) : '')));

  // status line
  let status = '', warn = false;
  if (v.phase === 'play') {
    if (myTurn) {
      if (tileDrag?.ghost) status = t('dropOnSpot');
      else if (S.choose) status = t('chooseEnd');
      else if (v.line.f === null) status = v.open !== null ? t('openWith', { tile: tileName(v.open) }) : t('firstMove');
      else if (canPlay(v.line, v.hand)) status = t('yourTurn');
      else { status = t(v.yard ? 'mustDraw' : 'mustPass'); warn = true; }
    } else if (v.turn >= 0) {
      const l = v.last;
      const pre = l && l.s >= 0 && l.s !== v.you && (l.t === 'draw' || l.t === 'pass') ? t(l.t === 'draw' ? 'drew' : 'passed', { name: seatName(v, l.s) }) + ' · ' : '';
      status = pre + t('turnOf', { name: seatName(v, v.turn) });
    }
  }
  const bottom = h('div', { class: 'bottom' },
    h('div', { class: 'status' + (warn ? ' warn' : '') }, status),
    myTurn && v.turnEndsAt && canPlay(v.line, v.hand) ? h('div', { class: 'timer-row' }, h('div', { class: 'timer', id: 'my-timer' }, h('i')), h('b', { id: 'my-secs', class: 'secs' })) : null,
    S.choose && S.choose.length > 1 && S.sel !== null && v.line.fo && !tileDrag?.ghost ? h('div', { class: 'row center' }, ...S.choose.map(e =>
      btn(t('endN', { n: e === 'L' ? (v.line.L.length ? v.line.L[v.line.L.length - 1][2] : v.line.fo![0]) : (v.line.R.length ? v.line.R[v.line.R.length - 1][2] : v.line.fo![1]) }), () => chooseEnd(e), 'btn alt small'))) : null,
    h('div', { class: 'handbar' },
      h('div', { class: 'sidebtns' },
        btn('☺', () => { S.sheet = S.sheet === 'emotes' ? null : 'emotes'; render(); }, 'iconbtn', { 'aria-label': t('reactions') }),
        ...voiceButtons()),
      h('div', { class: 'hand' + (v.hand.length > 10 ? ' many' : ''), style: `--n:${Math.max(7, v.hand.length)}` }, ...v.hand.map(id => {
        const el = TableScene.tileEl(id);
        if (myTurn) el.classList.add(endsFor(v.line, id).length && (v.open === null || v.open === id) ? 'ok' : 'dim');
        if (S.sel === id) el.classList.add('sel');
        if (tileDrag?.ghost && tileDrag.id === id) el.classList.add('lifted');
        el.addEventListener('click', () => { if (Date.now() - dragEndedAt > 400) tapTile(id); });
        el.addEventListener('pointerdown', e => pressTile(e, id, el));
        return el;
      })),
      h('span', { class: 'act-space' })));
  const out = [hud, bottom];
  if ((v.phase === 'rend' || v.phase === 'mend') && v.result) out.push(resultSheet(v));
  return out;
}

function resultSheet(v: TableView): HTMLElement {
  const r = v.result!, mend = v.phase === 'mend', myTeam = v.you >= 0 ? teamOf(v.mode, v.you) : -1;
  let head: string;
  if (mend && v.match) {
    const w = v.match.team;
    head = v.mode === 'team' ? (w === myTeam ? t('teamMatchWin') : t('teamMatchLose', { team: teamName(w) })) : (w === myTeam ? t('matchWin') : t('matchLose', { name: seatName(v, w) }));
  } else if (r.team < 0) head = t('blocked');
  else head = v.mode === 'team' ? t('teamRoundWin', { team: teamName(r.team) }) : t('roundWin', { name: seatName(v, r.winner) });
  const rows = v.seats.map((s, i) => s && h('div', { class: 'res-row' },
    h('span', { class: 'sw', 'data-ch': s.ch, style: `background-color:${charColor(s.ch)}` }),
    h('span', { class: 'nm' }, seatName(v, i)),
    h('span', { class: 'mini' }, ...r.hands[i].map(id => TableScene.tileEl(id, 'div'))),
    h('b', null, num(r.pips[i]))));
  const secs = Math.max(0, Math.ceil((v.nextAt - net.serverNow()) / 1000));
  const delta = mend && v.match && v.you >= 0 ? v.match.deltas[v.you] : null;
  const actions = h('div', { class: 'row center' });
  if (mend) {
    if (S.practice) actions.append(btn(t('playAgain'), practiceAgain, 'btn'), btn(t('home'), leaveGame, 'btn alt'));
    else if (!v.private) actions.append(btn(t('playAgain'), () => { const m = v.mode, st = v.stake; leaveGame(); S.mode = m; if (st) S.stake = st; quick(); }, 'btn'), btn(t('home'), leaveGame, 'btn alt'));
    else actions.append(btn(t('leave'), leaveGame, 'btn alt'));
  }
  return h('div', { class: 'overlay' }, h('div', { class: 'card' },
    h('h2', null, head),
    r.blocked && r.team >= 0 ? h('p', { class: 'muted' }, t('blocked')) : null,
    h('div', { class: 'big' }, r.team >= 0 ? t('plus', { n: r.points }) : t('tie')),
    payoutLine(v),
    delta !== null && delta !== undefined ? h('p', { class: delta >= 0 ? 'good' : 'bad' }, `${t('rating')} ${delta >= 0 ? '+' : '−'}${num(Math.abs(delta))}`) : null,
    h('p', { class: 'lab' }, t('handsLeft')),
    h('div', { class: 'res' }, ...rows),
    !mend ? h('p', { class: 'muted', id: 'next-count' }, t('nextRound', { s: secs })) : v.private && !S.practice ? h('p', { class: 'muted', id: 'next-count' }, t('backToRoom', { s: secs })) : null,
    actions));
}

/* ---------------- chess, tawla and ludo screens ---------------- */
const TAWLA_COLORS = ['#efe4cc', '#6e1f17'];
function seatSwatch(v: BoardView, seat: number) {
  if (v.chess) return seat === v.chess.white ? '#efe4cc' : '#2a1a12';
  if (v.tawla) return TAWLA_COLORS[seat];
  if (v.ludo) return LUDO_COLORS[v.ludo.colors[seat]];
  return charColor(v.seats[seat]?.ch ?? 0);
}
/** Chess time left for a seat right now. */
function clockLeft(v: BoardView, seat: number) {
  if (!v.clocks) return 0;
  const running = v.phase === 'play' && v.turn === seat;
  return Math.max(0, v.clocks[seat] - (running ? net.serverNow() - v.now : 0));
}
const fmtClock = (ms: number) => { const s = Math.ceil(ms / 1000); return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`; };

function boardScreen(v: BoardView): HTMLElement[] {
  const myTurn = v.phase === 'play' && v.turn === v.you;
  const sides = v.seats.map((s, seat) => {
    if (!s) return null;
    const active = v.phase === 'play' && v.turn === seat;
    let extra: HTMLElement | null = null;
    if (v.chess) extra = h('b', { class: 'clock', id: 'clock-' + seat, dir: 'ltr' }, fmtClock(clockLeft(v, seat)));
    else if (v.tawla) extra = h('b', null, num(v.scores[seat] ?? 0));
    else if (v.ludo) extra = h('span', { class: 'muted small' }, `${num(v.ludo.pawns[seat].filter(p => p >= 56).length)}/${num(4)}`);
    return h('div', { class: 'sc' + (active ? ' on' : ''), onclick: () => openPlayer(seat) },
      h('span', { class: 'sw', style: `background:${seatSwatch(v, seat)};border:1px solid #0006` }),
      h('span', { class: 'nm' }, seat === v.you ? t('you') : seatName(v, seat)), extra);
  }).filter(Boolean) as HTMLElement[];
  let meta = '';
  if (v.tawla) meta = `${t(('mode_' + v.mode) as Key)} · ${t('gameN', { n: v.round })} · ${t('to', { n: v.target })} · ${t('pipsShort', { n: v.tawla.pips[Math.max(0, v.you)] })}`;
  else if (v.chess) meta = t(('mode_' + v.mode) as Key) + ' · ' + t('moveN', { n: v.chess.moveNo });
  else if (v.ludo) meta = t(('mode_' + v.mode) as Key) + (v.ludo.variant === 'master' && v.you >= 0 && !v.ludo.captures[v.you] ? ' · ' + t('masterNeedCapture') : '');
  const hud = h('header', { class: 'hud' },
    h('div', { class: 'row' },
      btn('‹', () => { if ((S.practice && !v.stake) || v.phase !== 'play') leaveGame(); else { S.sheet = 'leave'; render(); } }, 'iconbtn back', { 'aria-label': t('leave') }),
      h('div', { class: 'scores' }, ...sides)),
    h('div', { class: 'meta muted small' }, meta));

  // status line and the main button
  let status = '', warn = false;
  let action: HTMLElement | null = null;
  if (v.phase === 'play') {
    if (!myTurn) {
      status = v.turn >= 0 ? t('turnOf', { name: seatName(v, v.turn) }) : '';
      if (v.chess && v.chess.check >= 0) status = t('check') + ' · ' + status;
    } else if (v.chess) {
      status = v.chess.check >= 0 ? t('check') : t('chessYourMove');
      warn = v.chess.check >= 0;
    } else if (v.tawla) {
      if (!v.tawla.rolled) { status = t('rollDice'); action = btn(t('roll'), () => act({ t: 'roll' }), 'btn big roll', { id: 'roll-btn' }); }
      else if (!v.tawla.steps.length) { status = t('noMovesPassed'); warn = true; }
      else {
        status = S.boardSel === null ? t('pickChecker') : t('youRolled');
        if (v.tawla.carry) status = t('gulCarry') + ' · ' + status;
        else if (v.tawla.later?.length || (v.tawla.rolled[0] === v.tawla.rolled[1] && v.tawla.dice[0] !== v.tawla.rolled[0])) status = t('gulRun', { n: v.tawla.dice[0] ?? v.tawla.rolled[0] }) + ' · ' + status;
        const sel = S.boardSel;
        if (sel !== null && v.tawla.steps.some(x => x[0] === sel && x[1] === OFF))
          action = btn(t('bearOff'), () => { act({ t: 'step', from: sel, to: OFF }); S.boardSel = null; render(); }, 'btn', { id: 'bearoff-btn' });
      }
    } else if (v.ludo) {
      if (v.ludo.die === null) { status = t('rollDice'); action = btn(t('roll'), () => act({ t: 'roll' }), 'btn big roll', { id: 'roll-btn' }); }
      else if (v.ludo.movable.length) status = t('rolledN', { n: v.ludo.die }) + ' · ' + t('pawnTap');
      else { status = t('rolledN', { n: v.ludo.die }) + ' · ' + (v.ludo.die === 6 ? t('noMovesPassed') : t('needSix')); warn = true; }
    }
  }
  const showTimer = myTurn && !v.chess && v.turnEndsAt;
  const bottom = h('div', { class: 'bottom board-bottom' },
    h('div', { class: 'status' + (warn ? ' warn' : '') }, status),
    showTimer ? h('div', { class: 'timer-row' }, h('div', { class: 'timer', id: 'my-timer' }, h('i')), h('b', { id: 'my-secs', class: 'secs' })) : null,
    v.chess && v.chess.captured.length ? capturedRow(v) : null,
    h('div', { class: 'handbar' },
      h('div', { class: 'sidebtns' },
        btn('☺', () => { S.sheet = S.sheet === 'emotes' ? null : 'emotes'; render(); }, 'iconbtn', { 'aria-label': t('reactions') }),
        ...voiceButtons()),
      h('div', { class: 'board-act' }, action),
      h('span', { class: 'act-space' })));
  const out = [hud, bottom];
  if ((v.phase === 'rend' || v.phase === 'mend') && v.result) out.push(boardResultSheet(v));
  return out;
}

const PIECE_GLYPH: Record<string, string> = { p: '♟', n: '♞', b: '♝', r: '♜', q: '♛', k: '♚' };
function capturedRow(v: BoardView) {
  const c = v.chess!;
  const byWhite = c.captured.filter(p => p === p.toLowerCase()), byBlack = c.captured.filter(p => p !== p.toLowerCase());
  const line = (ps: string[], white: boolean) => h('span', { class: 'cap ' + (white ? 'cap-w' : 'cap-b') }, ps.map(p => PIECE_GLYPH[p.toLowerCase()]).join(''));
  return h('div', { class: 'captured', dir: 'ltr' }, line(byBlack, true), line(byWhite, false));
}

function promoSheet(v: BoardView) {
  const pr = S.promo!;
  const white = v.chess?.you === 'w';
  const pick = (p: 'q' | 'r' | 'b' | 'n') => { act({ t: 'move', from: pr.from, to: pr.to, promo: p }); S.promo = null; S.boardSel = null; render(); };
  return h('div', { class: 'overlay', onclick: (e: Event) => { if (e.target === e.currentTarget) { S.promo = null; render(); } } },
    h('div', { class: 'card' }, h('h2', null, t('promoteTo')),
      h('div', { class: 'row center promo' }, ...(['q', 'r', 'b', 'n'] as const).map(p =>
        btn(PIECE_GLYPH[p], () => pick(p), 'piece-btn' + (white ? ' w' : ' b'), { 'aria-label': p })))));
}

function boardResultSheet(v: BoardView): HTMLElement {
  const r = v.result!, mend = v.phase === 'mend';
  const me = v.you;
  let head: string, sub = '';
  if (r.winner < 0) head = t('drawGame');
  else if (mend && v.match) head = v.match.team === me ? (v.tawla ? t('matchWin') : t('youWin')) : (v.tawla ? t('matchLose', { name: seatName(v, v.match.team) }) : t('winsGame', { name: seatName(v, v.match.team) }));
  else head = r.winner === me ? t('youWin') : t('gameWin', { name: seatName(v, r.winner) });
  const kindKey = ('res_' + r.kind) as Key;
  if (r.kind === 'count') sub = t('res_count', { n: r.points });
  else if (r.kind !== 'win') sub = t(kindKey);
  const delta = mend && v.match && me >= 0 ? v.match.deltas[me] : null;
  const actions = h('div', { class: 'row center' });
  if (mend) {
    if (S.practice) actions.append(btn(t('playAgain'), practiceAgain, 'btn'), btn(t('home'), leaveGame, 'btn alt'));
    else if (!v.private) actions.append(btn(t('playAgain'), () => { const m = v.mode, st = v.stake; leaveGame(); S.mode = m; if (st) S.stake = st; quick(); }, 'btn'), btn(t('home'), leaveGame, 'btn alt'));
    else actions.append(btn(t('leave'), leaveGame, 'btn alt'));
  }
  const secs = Math.max(0, Math.ceil((v.nextAt - net.serverNow()) / 1000));
  return h('div', { class: 'overlay' }, h('div', { class: 'card' },
    h('h2', null, head),
    sub ? h('p', { class: r.kind === 'mars' || r.kind === 'mother' ? 'good' : 'muted' }, sub) : null,
    v.tawla ? h('div', { class: 'big' }, v.scores.map(n => num(n)).join(' – ')) : null,
    payoutLine(v),
    delta !== null && delta !== undefined ? h('p', { class: delta >= 0 ? 'good' : 'bad' }, `${t('rating')} ${delta >= 0 ? '+' : '−'}${num(Math.abs(delta))}`) : null,
    !mend ? h('p', { class: 'muted', id: 'next-count' }, t('nextGame', { s: secs })) : v.private && !S.practice ? h('p', { class: 'muted', id: 'next-count' }, t('backToRoom', { s: secs })) : null,
    actions));
}

function emoteSheet() {
  return h('div', { class: 'emotes', role: 'dialog' }, ...EMOTES.map(k => btn(t(('emote_' + k) as Key), () => sendEmote(k), 'chip')));
}

function sheet(...kids: HTMLElement[]) { return h('div', { class: 'overlay' }, h('div', { class: 'card' }, ...kids)); }

/* ---------------- voice chat ---------------- */
function voiceButtons(): HTMLElement[] {
  if (S.practice || !Voice.supported()) return [];
  if (!voice.joined) return [btn('🎙', joinVoice, 'iconbtn voice-btn', { id: 'mic-btn', 'aria-label': t('joinVoice'), title: t('joinVoice') })];
  return [
    btn(voice.micMuted ? '🔇' : '🎙', () => voice.setMicMuted(!voice.micMuted), 'iconbtn voice-btn on' + (voice.micMuted ? ' muted' : ''), { id: 'mic-btn', 'aria-label': voice.micMuted ? t('unmuteMic') : t('muteMic'), 'aria-pressed': String(!voice.micMuted) }),
    btn('✕', () => { voice.leave(); render(); }, 'iconbtn small-icon', { 'aria-label': t('leaveVoice'), title: t('leaveVoice') }),
  ];
}
async function joinVoice() {
  unlockAudio();
  const r = await voice.join();
  if (r === 'denied') toast(t('micDenied'));
  else if (r === 'unsupported') toast(t('voiceUnsupported'));
  else { toast(t('voiceJoined')); if (S.view) voice.sync(S.view); }
  render();
}
function updateVoiceButtons() {
  const b = document.getElementById('mic-btn');
  if (b) b.classList.toggle('talking', voice.joined && !voice.micMuted && voice.talking(-1));
}

/* ---------------- players, gifts, blocking ---------------- */
function openPlayer(seat: number) {
  const v = S.view; if (!v || S.practice) return;
  const s = v.seats[seat]; if (!s || seat === v.you) return;
  S.player = seat; S.reporting = false; S.sheet = 'player'; render();
}
function playerSheet(v: AnyView): HTMLElement | null {
  const seat = S.player, s = v.seats[seat];
  if (!s) { S.sheet = null; return null; }
  const close = () => { S.sheet = null; render(); };
  const coins = S.user?.coins ?? 0;
  const kids: (HTMLElement | null)[] = [
    h('div', { class: 'row between' },
      h('div', { class: 'row' }, h('span', { class: 'sw big-sw', 'data-ch': s.ch, style: `background-color:${charColor(s.ch)}` }),
        h('div', { class: 'stack tight' }, h('b', null, seatName(v, seat)), h('span', { class: 'muted small' }, s.bot ? t('bot') : `${t('rating')} ${num(s.rating)}`))),
      btn('✕', close, 'iconbtn small-icon', { 'aria-label': t('close') })),
  ];
  if (s.bot) kids.push(h('p', { class: 'muted' }, t('giftBot')));
  else if (S.reporting) {
    kids.push(h('p', null, t('reportWhy')), h('div', { class: 'chips' }, ...(['abuse', 'cheating', 'spam', 'name'] as const).map(r =>
      btn(t(('report_' + r) as Key), () => { net.send({ t: 'report', seat, reason: r }); S.reporting = false; S.sheet = null; toast(t('reported')); render(); }, 'chip'))));
  } else {
    kids.push(
      h('div', { class: 'row between' }, h('span', { class: 'lab' }, t('sendGift')), h('span', { class: 'coins' }, '🪙 ' + num(coins))),
      h('div', { class: 'gifts' }, ...GIFTS.map(g => h('button', { type: 'button', class: 'gift', disabled: coins < g.price, onclick: () => sendGift(seat, g.k) },
        h('span', { class: 'gi' }, g.icon), h('span', { class: 'gn' }, t(('gift_' + g.k) as Key)), h('span', { class: 'gp' }, '🪙 ' + num(g.price))))),
      coins < GIFTS[0].price ? h('p', { class: 'muted small' }, t('earnCoins')) : null,
      h('hr'),
      voice.joined && s.mic ? btn(voice.muted.has(seat) ? t('unmutePlayer') : t('mutePlayer'), () => voice.setMuted(seat, !voice.muted.has(seat)), 'btn alt') : null,
      h('div', { class: 'row' },
        btn(blocked.has(s.pid) ? t('unblock') : t('block'), () => {
          if (blocked.has(s.pid)) { blocked.delete(s.pid); voice.setMuted(seat, false); toast(t('unblocked')); }
          else { blocked.add(s.pid); voice.setMuted(seat, true); toast(t('blockedMsg')); }
          saveBlocked(); render();
        }, 'btn alt'),
        btn(t('report'), () => { S.reporting = true; render(); }, 'btn alt danger-text')));
  }
  return h('div', { class: 'overlay', onclick: (e: Event) => { if (e.target === e.currentTarget) close(); } }, h('div', { class: 'card player-card' }, ...kids.filter(Boolean) as HTMLElement[]));
}
function sendGift(seat: number, k: GiftKind) {
  unlockAudio();
  net.send({ t: 'gift', to: seat, k });
  S.sheet = null; render();
}
/** Where a seat is on screen: above the character, or above your hand for your own seat. */
function seatScreen(seat: number): { x: number; y: number } {
  const v = S.view, pos = scene.posOf(seat);
  if (v && pos === 0 && v.you >= 0) return { x: window.innerWidth / 2, y: window.innerHeight - Math.max(60, chrome.bottom * 0.6) };
  return (pos >= 0 && scene.headScreen(pos)) || { x: window.innerWidth / 2, y: window.innerHeight / 3 };
}
function flyGift(from: number, to: number, k: GiftKind) {
  const g = giftByKind(k); const v = S.view; if (!g || !v) return;
  const a = seatScreen(from), b = seatScreen(to);
  const el = h('div', { class: 'gift-fly' }, g.icon);
  el.style.left = a.x + 'px'; el.style.top = a.y + 'px';
  document.body.append(el);
  requestAnimationFrame(() => requestAnimationFrame(() => { el.style.transform = `translate(${b.x - a.x}px, ${b.y - a.y}px) scale(1.6)`; }));
  setTimeout(() => { el.classList.add('land'); sfx.gift(); }, 900);
  setTimeout(() => el.remove(), 1900);
  scene.animate(from, 'play'); setTimeout(() => scene.animate(to, 'win'), 900);
  const gift = `${t(('gift_' + k) as Key)} ${g.icon}`;
  toast(to === v.you ? t('giftSentYou', { from: seatName(v, from), gift }) : t('giftSent', { from: seatName(v, from), to: seatName(v, to), gift }));
}

/* ---------------- labels over characters (per frame) ---------------- */
/**
 * How much of the screen the HUD and the controls cover, so the 3D table is framed in the space that is left
 * (any phone, tablet, foldable or desktop window), and labels and drops keep clear of them.
 */
const chrome = { top: 0, right: 0, bottom: 0, left: 0 };
/** the round/match line under the scores: the 3D picture may run under it, a name tag may not cover it */
const metaBox = { left: 0, right: 0, bottom: 0 };
function measureChrome() {
  const W = window.innerWidth, H = window.innerHeight;
  chrome.top = chrome.right = chrome.bottom = chrome.left = 0;
  const shown = (el: Element) => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0 ? r : null; };
  // the small round/match line under the scores may sit over the far edge of the picture; the score bar may not
  const hud = document.querySelector('#ui .hud');
  metaBox.left = metaBox.right = metaBox.bottom = 0;
  if (hud) for (const c of hud.children) {
    const r = shown(c); if (!r) continue;
    if (!c.classList.contains('meta')) chrome.top = Math.max(chrome.top, r.bottom);
    else { const txt = document.createRange(); txt.selectNodeContents(c); const t = txt.getBoundingClientRect(); Object.assign(metaBox, { left: t.left - 6, right: t.right + 6, bottom: r.bottom }); }
  }
  const bot = document.querySelector('#ui .bottom');
  const br = bot && shown(bot);
  if (bot && br) {
    if (br.height > H * 0.55 && br.width < W * 0.45) {
      // a control panel down the side (board games on a landscape phone)
      if (br.left > W / 2) chrome.right = W - br.left; else chrome.left = br.right;
    } else {
      // domino: the turn line and timer may float over the table, the hand may not; a board stays clear of all of it
      let top = H;
      const hand = !bot.classList.contains('board-bottom') && bot.querySelector('.handbar');
      const solid = hand ? [hand] : [...bot.children];
      for (const c of solid) { const r = shown(c); if (r) top = Math.min(top, r.top); }
      chrome.bottom = H - top;
    }
  }
  scene.setInsets(chrome);
}
let resizeRaf = 0;
window.addEventListener('resize', () => { cancelAnimationFrame(resizeRaf); resizeRaf = requestAnimationFrame(() => { measureChrome(); renderLabels(false); }); });

const labels = [0, 1, 2, 3].map(pos => {
  const d = h('div', { class: 'lbl', role: 'button', tabindex: '0' });
  d.hidden = true; $('labels').append(d);
  const open = () => { const seat = scene.seatAt(pos); if (seat >= 0) openPlayer(seat); };
  d.addEventListener('click', open);
  d.addEventListener('keydown', e => { if ((e as KeyboardEvent).key === 'Enter') open(); });
  return d;
});
let lastSecond = -1;
function renderLabels(content: boolean) {
  const v = S.view;
  labels.forEach((el, pos) => {
    const seat = v ? scene.seatAt(pos) : -1;
    const s = v && seat >= 0 ? v.seats[seat] : null;
    // your own seat is shown in the score bar and the hand, so it gets no floating label
    const mine = pos === 0 && !!v && v.you >= 0;
    const myBubble = mine && (S.bubbles.get(v!.you)?.until ?? 0) > Date.now();
    if (!v || !s || v.phase === 'lobby' || (mine && !myBubble)) { el.hidden = true; return; }
    const p = scene.headScreen(pos);
    if (!p) { el.hidden = true; return; }
    if (content || !el.dataset.k) {
      const bubble = S.bubbles.get(seat);
      const showBubble = bubble && bubble.until > Date.now();
      const talking = !!s.mic && voice.joined && !voice.muted.has(seat) && voice.talking(seat);
      const key = [seatName(v, seat), s.count, s.away, s.online, v.turn, v.phase, showBubble ? (bubble!.text ?? bubble!.k) : '', getLang(), s.mic, talking, voice.muted.has(seat)].join('|');
      if (el.dataset.k !== key) {
        el.dataset.k = key;
        el.innerHTML = '';
        el.className = 'lbl' + (v.phase === 'play' && v.turn === seat ? ' on' : '') + (s.away || !s.online ? ' away' : '') + (talking ? ' talking' : '') + (S.practice ? ' static' : '');
        if (s.mic) el.append(h('span', { class: 'mic', 'aria-label': t('inVoice') }, voice.muted.has(seat) ? '🔇' : '🎙'));
        el.append(h('span', { class: 'n' }, seatName(v, seat) + (seat === v.you ? ` (${t('you')})` : '')));
        if (!s.online && !s.bot) el.append(h('span', { class: 'c' }, t('offlineTag')));
        else if (s.away) el.append(h('span', { class: 'c' }, t('away')));
        if (!mine && v.game === 'domino') el.append(h('span', { class: 'c' }, t('tiles', { n: s.count })));
        if (v.phase === 'play' && v.turn === seat && v.turnEndsAt && !(v.game === 'chess')) el.append(h('i', { class: 'bar' }));
        if (showBubble) el.append(h('span', { class: 'bubble', dir: 'auto' }, bubble!.text ?? t(('emote_' + bubble!.k) as Key)));
      }
    }
    el.hidden = false;
    // keep labels on screen, clear of the side panel, and below the score bar
    const half = Math.min(el.offsetWidth / 2 + 6, window.innerWidth / 4);
    const x = Math.max(chrome.left + half, Math.min(window.innerWidth - chrome.right - half, p.x));
    const underMeta = x - half < metaBox.right && x + half > metaBox.left;
    el.style.left = x + 'px';
    el.style.top = Math.max((underMeta ? Math.max(chrome.top, metaBox.bottom) : chrome.top) + el.offsetHeight + 4, p.y) + 'px';
  });
  // timers
  if (v && v.phase === 'play' && v.turnEndsAt && v.game !== 'chess') {
    const left = Math.max(0, v.turnEndsAt - net.serverNow());
    const frac = Math.min(1, left / v.turnMs);
    document.querySelectorAll<HTMLElement>('.lbl.on .bar, #my-timer i').forEach(b => { b.style.width = (frac * 100).toFixed(1) + '%'; b.classList.toggle('low', left < 5000); });
    const sec = Math.ceil(left / 1000);
    const secsEl = document.getElementById('my-secs');
    if (secsEl) { secsEl.textContent = t('secs', { s: sec }); secsEl.classList.toggle('low', left < 5000); }
    if (v.turn === v.you && sec <= 3 && sec > 0 && sec !== lastSecond) { lastSecond = sec; sfx.tick(); }
  }
  // chess clocks
  if (v && v.game === 'chess' && v.clocks) v.seats.forEach((_, seat) => {
    const el = document.getElementById('clock-' + seat); if (!el) return;
    const left = clockLeft(v, seat);
    el.textContent = fmtClock(left);
    el.classList.toggle('low', left < 30_000);
    if (seat === v.you && v.turn === seat && v.phase === 'play' && left < 10_000) {
      const sec = Math.ceil(left / 1000); if (sec !== lastSecond) { lastSecond = sec; sfx.tick(); }
    }
  });
  // countdowns
  const nc = document.getElementById('next-count');
  if (nc && v) { const s = Math.max(0, Math.ceil((v.nextAt - net.serverNow()) / 1000)); nc.textContent = t(v.phase === 'rend' ? (v.game === 'domino' ? 'nextRound' : 'nextGame') : 'backToRoom', { s }); }
  const qb = document.getElementById('q-bots');
  if (qb && S.queue?.botsAt) qb.textContent = t('botsIn', { s: Math.max(0, Math.ceil((S.queue.botsAt - net.serverNow()) / 1000)) });
}
// expire emote bubbles
setInterval(() => { const now = Date.now(); let ch = false; S.bubbles.forEach((b, k) => { if (b.until < now) { S.bubbles.delete(k); ch = true; } }); if (ch) renderLabels(true); }, 500);
scene.onFrame = () => renderLabels(false);

/* ---------------- platform ---------------- */
CapApp.addListener('backButton', () => {
  if (S.promo) { S.promo = null; return render(); }
  if (S.sheet) { S.sheet = null; return render(); }
  if (S.view) { if ((S.practice && !S.view.stake) || S.view.phase !== 'play') leaveGame(); else { S.sheet = 'leave'; render(); } return; }
  if (S.queue) { net.send({ t: 'cancel' }); S.queue = null; return render(); }
  if (S.screen !== 'home') { S.screen = 'home'; return render(); }
  CapApp.minimizeApp().catch(() => {});
}).catch(() => {});

const hashCode = location.hash.replace('#', '');
if (/^[a-z0-9]{5}$/i.test(hashCode)) { S.joinCode = hashCode.toUpperCase(); S.screen = 'friends'; }
if ('serviceWorker' in navigator && location.protocol === 'https:' && !Capacitor.isNativePlatform()) navigator.serviceWorker.register('/sw.js').catch(() => {});

// how-to-play recorder: with ?rec in the address a script can read the view and find board spots on screen
// recording hooks: ?rec for the how-to GIFs, ?promo for the promo video (the computer plays your seat)
if (/[?&](rec|promo)\b/.test(location.search)) (window as unknown as Record<string, unknown>).__ahwa = {
  view: () => S.view, spot: (p: Pick) => scene.spotScreen(p), ghost: (e: End) => scene.ghostScreen(e),
  practice: (mode: Mode) => { startPractice(mode); const seat = S.practice?.table.seats[0]; if (seat) seat.away = true; S.practice?.table.refresh(); },
};
// the game is played in landscape: lock it where the browser allows (installed app, fullscreen)
const lockLandscape = () => { try { (screen.orientation as ScreenOrientation & { lock?: (o: string) => Promise<void> }).lock?.('landscape').catch(() => {}); } catch { /* not supported */ } };
document.addEventListener('pointerdown', lockLandscape, { once: true });
{
  const rot = h('div', { id: 'rotate', role: 'alert' },
    h('div', { class: 'phone', 'aria-hidden': 'true' }), h('b', null, t('rotateTitle')), h('span', null, t('rotateHint')));
  document.body.append(rot);
}
document.addEventListener('pointerdown', unlockAudio, { once: true });
net.connect();
initAds();
render();
