import type { End, GameId, Line, Mode } from './engine';
import type { GiftKind } from './gifts';
import type { TitleDef } from './titles';
import type { BoardAction, BoardEvent, BoardGameId, ChessView, GameEnd, LudoView, TawlaView } from './games/defs';
export type { BoardAction } from './games/defs';

export interface SeatView {
  name: string;
  ch: number;          // character index
  bot: boolean;
  away: boolean;       // a computer is playing for this person
  online: boolean;
  count: number;       // tiles in hand
  team: number;
  host: boolean;
  rating: number;
  mic: boolean;        // in the table's voice chat
  pid: string;         // public id for blocking a player across games ('' for computer players)
  back?: number;       // dropped out: the time (ms) the computer takes over unless they come back first
  hat?: string;        // their look: the hat on their character and the drink by their place
  drink?: string;
  muted?: boolean;     // their chat is switched off for a while (reported by several players)
  title?: string;      // the title they wear (an id in the titles catalog)
  win?: string;        // how they celebrate a win (a 'win' skin)
}

export interface LastAction {
  t: 'deal' | 'play' | 'draw' | 'pass' | 'round' | 'match';
  s: number;           // seat, -1 when nobody
  id?: number;
  e?: End;
  k: number;           // unique key so clients can animate once
  auto?: boolean;      // the move was made for the player because their time ran out
  forced?: boolean;    // the player had no tile that fits, so the game drew or passed for them
}

export interface RoundResult {
  winner: number;      // seat that won the round, -1 on a tied block
  team: number;        // winning team, -1 on tie
  blocked: boolean;
  points: number;
  pips: number[];      // per seat
  hands: number[][];   // revealed hands per seat
}

/** One line of a player's coin history. */
export interface CoinRow {
  ts: number;
  kind: string;        // win | loss | draw | left | refund | daily | gift | welcome
  delta: number;       // change to the balance (a win is the profit after the fee, a loss the stake)
  fee: number;         // the game's 10% on a win
  stake: number;
  mode: string | null;
  balance: number;     // balance after this line
}

export interface MatchResult {
  team: number;
  scores: number[];
  deltas: (number | null)[]; // rating change per seat (null = unrated)
}

export interface TableView {
  game: 'domino';
  id: string;
  code: string | null;
  mode: Mode;
  target: number;
  private: boolean;
  stake: number;       // coins each player puts in (0: friendly)
  phase: 'lobby' | 'play' | 'rend' | 'mend';
  you: number;
  seats: (SeatView | null)[];
  hand: number[];
  line: Line;
  yard: number;
  block?: boolean;     // the block game: no drawing (a room's house rule)
  /** Tile that must open the round (heaviest double), while the line is empty. */
  open: number | null;
  turn: number;
  turnEndsAt: number;
  turnMs: number;
  round: number;
  scores: number[];    // per team (in free-for-all every seat is its own team)
  last: LastAction | null;
  result: RoundResult | null;
  match: MatchResult | null;
  nextAt: number;      // when the next round (or the return to the room) happens
  now: number;
  v: number;
}

/** A chess, tawla or ludo table as one player sees it. */
export interface BoardView {
  game: BoardGameId;
  id: string;
  code: string | null;
  mode: Mode;
  target: number;
  private: boolean;
  stake: number;
  phase: 'lobby' | 'play' | 'rend' | 'mend';
  you: number;
  seats: (SeatView | null)[];
  turn: number;
  turnEndsAt: number;
  turnMs: number;
  clocks: number[] | null;  // chess: time left per seat (ms) at `now`
  round: number;
  scores: number[];
  last: (BoardEvent & { k: number; auto?: boolean; forced?: boolean }) | null;
  result: GameEnd | null;
  match: MatchResult | null;
  nextAt: number;
  now: number;
  v: number;
  chess?: ChessView;
  tawla?: TawlaView;
  ludo?: LudoView;
}
export type AnyView = TableView | BoardView;

/** WebRTC session description passed between two players through the server (audio only). */
export interface RtcSignal { type: 'offer' | 'answer'; sdp: string; table: string }
export interface IceServer { urls: string | string[]; username?: string; credential?: string }

export type Action = { t: 'play'; id: number; e?: End } | { t: 'draw' } | { t: 'pass' };

export const EMOTES = ['hi', 'gg', 'nice', 'lol', 'wow', 'clap', 'fire', 'hurry', 'oops', 'think', 'angry', 'love'] as const;
export type Emote = typeof EMOTES[number];

/** A friend (or a friend request) in the friends list. */
/** A friends' café group ("قهوة الشلة"): saved friends who play together, with their own weekly ranking. */
export interface GroupRow { id: number; name: string; code: string; members: number; owner: boolean; online: number }
export interface GroupView { id: number; name: string; code: string; owner: boolean; week: string; endsAt: number;
  members: { pid: string; name: string; ch: number; online: boolean; playing: boolean; title: string | null; points: number; games: number; wins: number; rank: number; me: boolean }[] }
export interface FriendRow { id: string; pid: string; name: string; ch: number; status: 'friend' | 'outgoing' | 'incoming'; online: boolean; playing: boolean }

export interface PublicUser {
  id: string;
  name: string;
  ch: number;
  email: string | null;
  rating: number;
  games: number;
  wins: number;
  coins: number;
  gifts: number;       // gifts received
  phone: string | null;   // signed-in mobile number, partly hidden
  google: string | null;  // signed-in Google account (its email)
  apple?: string | null;  // signed in with Apple (its e-mail, or "Apple")
  adsLeft: number;        // rewarded ads that still pay out today
  adFreeUntil: number;    // no full-screen ads until (ms since 1970); 0 = shows ads
  titles?: { id: string; until: number; note: string | null }[];   // the titles you have (until 0: for good)
  title?: string | null;  // the one you wear
  skins: string[];        // skins bought in the shop (the free classic ones aren't listed)
  equip: Partial<Record<'tiles' | 'dice' | 'tawla' | 'chess' | 'ludo', string>>;   // the skin chosen for each kind
  ratings: Partial<Record<GameId, { rating: number; games: number; wins: number }>>;
  playGames?: boolean;   // signed in with Google Play Games
  code?: string;         // friend code others add this player with
  pid?: string;          // the public id on their player card
  xp?: number;           // experience, and the level it makes
  level?: number;
  streak?: number;       // days in a row the player came back
  daily?: { day: number; ready: boolean; nextAt: number; coins: number };   // the daily reward: today's day on the track, whether it's waiting
  missions?: MissionRow[];   // today's three missions, and when they change
  missionsAt?: number;
  season?: SeasonState;
  canRefer?: boolean;        // a new player who can still enter the friend code of who invited them
  referred?: boolean;
  muted?: number;            // chat switched off until (ms), after reports from several players
}

export interface MissionRow { id: string; game: string; kind: 'play' | 'win'; need: number; have: number; coins: number; xp: number; online: boolean; claimed: boolean }
export interface SeasonState { id: string; endsAt: number; sp: number; tier: number; premium: boolean; free: number[]; prem: number[]; price: number; egp: number }

/** The weekly tournament as one player sees it. */
export interface TourneyEntrant { name: string; ch: number; bot: boolean; me: boolean }
export interface TourneyMatch { a: TourneyEntrant | null; b: TourneyEntrant | null; winner: 0 | 1 | null; live: boolean; table?: string }
export interface TourneyView {
  event: string; game: string; mode: string; startsAt: number;
  state: 'open' | 'running' | 'done';
  registered: boolean; count: number; prizes: number[];
  rounds: TourneyMatch[][];
  you: { alive: boolean; table: string | null; place: number | null } | null;
  results: { place: number; name: string; coins: number; me: boolean }[];
  now: number;
}

export interface LeaderRow { rank: number; name: string; ch: number; rating: number; games: number; wins: number; me: boolean; level?: number; pid?: string; title?: string | null }

/** A player's card: who they are, how they play, and where you stand with them. */
export interface PlayerCard {
  pid: string; name: string; ch: number; level: number; xp: number; since: number; title?: string | null;
  games: number; wins: number; losses: number; winRate: number;
  coinsWon: number; coinsLost: number; net: number; bestWin: number; tourneyCoins: number; weeklyCoins: number;
  perGame: Record<string, { rating: number; games: number; wins: number; losses: number }>;
  gifts: number; streak: number;
  online: boolean; playing: boolean;
  me: boolean;
  friend: 'friend' | 'outgoing' | 'incoming' | null;
}

/** Client → server */
export type C2S =
  | { t: 'hello'; token?: string; v?: number }
  /** Collect today's daily reward (clients from v2 on; older ones get it as soon as they connect). */
  | { t: 'daily' }
  /** After a matchmade game: play again with the same people. */
  | { t: 'rematch' }
  /** Collect a finished daily mission. */
  | { t: 'mission'; id: string }
  /** The season pass: open the premium track with coins; collect a tier on a track. */
  | { t: 'seasonBuy' }
  | { t: 'seasonClaim'; tier: number; track: 'free' | 'prem' }
  /** The weekly tournament: see it, sign up or drop out, go to your match. */
  | { t: 'tourney' }
  | { t: 'tourneyJoin' }
  | { t: 'tourneyLeave' }
  | { t: 'tourneyGo' }
  /** A new player enters the friend code of who invited them. */
  | { t: 'referral'; code: string }
  | { t: 'profile'; name?: string; ch?: number }
  | { t: 'register'; email: string; pw: string }
  | { t: 'login'; email: string; pw: string }
  | { t: 'logout' }
  | { t: 'otp'; phone: string }
  | { t: 'otpCheck'; phone: string; code: string }
  | { t: 'ticket'; ticket: string }
  | { t: 'playGames'; code: string }
  | { t: 'friends' }
  | { t: 'friendAdd'; code?: string; seat?: number; pid?: string }
  | { t: 'friendAnswer'; id?: string; pid?: string; yes: boolean }
  | { t: 'friendRemove'; id?: string; pid?: string }
  /** A player's card (from a seat at the table, the leaderboard or the friends list). */
  | { t: 'profileOf'; pid?: string; seat?: number }
  | { t: 'invite'; id: string; mode?: Mode }
  | { t: 'delete' }
  | { t: 'quick'; mode: Mode; stake?: number }
  | { t: 'cancel' }
  | { t: 'create'; mode: Mode; target: number; stake?: number; block?: boolean }
  /** A free, unrated game against computer players that starts at once. */
  | { t: 'practice'; mode: Mode }
  | { t: 'join'; code: string }
  | { t: 'leave' }
  | { t: 'setup'; mode?: Mode; target?: number; stake?: number; block?: boolean }
  | { t: 'bot'; seat: number }
  | { t: 'kick'; seat: number }
  | { t: 'start' }
  | { t: 'act'; a: Action | BoardAction }
  | { t: 'emote'; k: Emote }
  | { t: 'lb'; game?: GameId | 'week' }
  | { t: 'coins' }
  | { t: 'adStart' }
  | { t: 'adWatched'; ticket: string }
  /** A Google Play purchase to check and credit: a coin pack or the ad-free plan. */
  | { t: 'buy'; product: string; token: string }
  /** The skin shop: buy a skin with coins, or wear one you own (or a free one). */
  | { t: 'buySkin'; id: string }
  | { t: 'equipSkin'; id: string }
  | { t: 'gift'; to: number; k: GiftKind }
  | { t: 'voice'; on: boolean }
  | { t: 'rtc'; to: number; data: RtcSignal }
  | { t: 'report'; seat?: number; pid?: string; reason: string }
  /** The phone's Firebase Cloud Messaging token, for the daily reminders. */
  | { t: 'pushToken'; token: string; lang?: string; platform?: string }
  /** Cash out: every 1,000,000 coins pays 10 EGP to a Vodafone Cash number, paid by hand within one working day. */
  | { t: 'prizeClaim'; id: number; phone: string }
  | { t: 'tutorialDone' }
  | { t: 'track'; name: string }
  | { t: 'watch'; pid?: string; table?: string }
  | { t: 'unwatch' }
  | { t: 'groups' }
  | { t: 'groupCreate'; name: string }
  | { t: 'groupJoin'; code: string }
  | { t: 'groupLeave'; id: number }
  | { t: 'groupView'; id: number }
  | { t: 'groupPlay'; id: number; mode: Mode }
  | { t: 'chestOpen'; step: number }
  | { t: 'wearTitle'; id: string | null }
  /** A practice game (played on the phone) finished: a little experience. */
  | { t: 'practiceDone'; game: GameId; won: boolean }
  /** A message to the team from "Contact support". */
  | { t: 'support'; text: string; contact?: string; app?: string }
  | { t: 'ping'; c: number };

/** Server → client */
export type S2C =
  | { t: 'welcome'; user: PublicUser; token: string; now: number; ice: IceServer[]; login?: { google: boolean; apple?: boolean; phone: boolean; playGames?: string } }
  | { t: 'friends'; rows: FriendRow[] }
  | { t: 'friendNews'; kind: 'request' | 'accepted' | 'added' | 'sent'; name: string }
  | { t: 'invited'; from: { id: string; name: string; ch: number }; code: string; mode: Mode; stake: number; group?: string }
  | { t: 'otp'; phone: string }
  | { t: 'signedIn'; via: 'phone' | 'google' | 'playgames' | 'apple'; fresh: boolean }
  | { t: 'bonus'; coins: number; streak?: number }
  | { t: 'xp'; gained: number; xp: number; level: number; levelUp: boolean }
  | { t: 'weeklyPrize'; rank: number; coins: number; week: string }
  | { t: 'gift'; from: number; to: number; k: GiftKind }
  | { t: 'rtc'; from: number; data: RtcSignal }
  | { t: 'user'; user: PublicUser }
  | { t: 'err'; code: string }
  | { t: 'queue'; mode: Mode | null; count: number; need: number; botsAt: number; now: number; stake?: number }
  | { t: 'coins'; rows: CoinRow[] }
  | { t: 'adTicket'; ticket: string; ssv: boolean }
  | { t: 'adReward'; coins: number }
  /** A purchase was checked with the store and credited (or was already credited earlier). */
  | { t: 'bought'; product: string; token: string; coins: number; adFreeUntil: number }
  | { t: 'buyFailed'; product: string; token: string; code: string }
  | { t: 'payout'; kind: string; delta: number; fee: number; stake: number }
  | { t: 'table'; view: AnyView }
  | { t: 'left' }
  | { t: 'emote'; seat: number; k: Emote }
  | { t: 'lb'; game: GameId | 'week'; rows: LeaderRow[]; me: LeaderRow | null; week?: string; endsAt?: number; prizes?: number[];
      /** this month's race for "<game> player of the month": the title, the leader, your place and points, when it ends */
      month?: { title: string; on: boolean; leader: string | null; leaderPoints: number; place: number; points: number; endsAt: number; minGames: number; holder: string | null } }
  | { t: 'titles'; list: TitleDef[] }
  | { t: 'titleWon'; id: string; month: string }
  | { t: 'notice'; text: string }
  | { t: 'prizeClaimed'; id: number }
  | { t: 'chestOpened'; step: number; coins: number; xp: number }
  | { t: 'comeback'; coins: number; days: number }
  | { t: 'tutorialReward'; coins: number }
  | { t: 'groups'; rows: GroupRow[] }
  | { t: 'group'; group: GroupView }
  | { t: 'prizeWon'; id: number; game: string; month: string; egp: number }
  | { t: 'supportSent'; id: number }
  | { t: 'rematch'; seats: number[]; need: number; gone: boolean }
  | { t: 'profileCard'; card: PlayerCard }
  | { t: 'missionDone'; id: string }
  | { t: 'missionClaimed'; id: string; coins: number; xp: number }
  | { t: 'seasonTier'; tier: number }
  | { t: 'seasonClaimed'; tier: number; track: 'free' | 'prem'; coins?: number; skin?: string }
  | { t: 'tourney'; view: TourneyView }
  | { t: 'tourneyMatch'; table: string; round: number }
  | { t: 'referral'; coins: number; name: string; you: 'inviter' | 'invited' }
  | { t: 'pong'; c: number; now: number };

/** A monthly prize: unclaimed (the winner hasn't given a Vodafone Cash number), pending (to pay), paid, rejected, expired. */
export interface PrizeRow { id: number; month: string; game: string; egp: number; status: 'unclaimed' | 'pending' | 'paid' | 'rejected' | 'expired'; phone: string | null; ts: number; doneTs: number | null; note: string | null }
