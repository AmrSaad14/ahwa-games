import type { End, GameId, Line, Mode } from './engine';
import type { GiftKind } from './gifts';
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
  phase: 'lobby' | 'play' | 'rend' | 'mend';
  you: number;
  seats: (SeatView | null)[];
  hand: number[];
  line: Line;
  yard: number;
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
  ratings: Partial<Record<GameId, { rating: number; games: number; wins: number }>>;
}

export interface LeaderRow { rank: number; name: string; ch: number; rating: number; games: number; wins: number; me: boolean }

/** Client → server */
export type C2S =
  | { t: 'hello'; token?: string; v?: number }
  | { t: 'profile'; name?: string; ch?: number }
  | { t: 'register'; email: string; pw: string }
  | { t: 'login'; email: string; pw: string }
  | { t: 'logout' }
  | { t: 'delete' }
  | { t: 'quick'; mode: Mode }
  | { t: 'cancel' }
  | { t: 'create'; mode: Mode; target: number }
  | { t: 'join'; code: string }
  | { t: 'leave' }
  | { t: 'setup'; mode?: Mode; target?: number }
  | { t: 'bot'; seat: number }
  | { t: 'kick'; seat: number }
  | { t: 'start' }
  | { t: 'act'; a: Action | BoardAction }
  | { t: 'emote'; k: Emote }
  | { t: 'lb'; game?: GameId }
  | { t: 'gift'; to: number; k: GiftKind }
  | { t: 'voice'; on: boolean }
  | { t: 'rtc'; to: number; data: RtcSignal }
  | { t: 'report'; seat: number; reason: string }
  | { t: 'ping'; c: number };

/** Server → client */
export type S2C =
  | { t: 'welcome'; user: PublicUser; token: string; now: number; ice: IceServer[] }
  | { t: 'bonus'; coins: number }
  | { t: 'gift'; from: number; to: number; k: GiftKind }
  | { t: 'rtc'; from: number; data: RtcSignal }
  | { t: 'user'; user: PublicUser }
  | { t: 'err'; code: string }
  | { t: 'queue'; mode: Mode | null; count: number; need: number; botsAt: number; now: number }
  | { t: 'table'; view: AnyView }
  | { t: 'left' }
  | { t: 'emote'; seat: number; k: Emote }
  | { t: 'lb'; game: GameId; rows: LeaderRow[]; me: LeaderRow | null }
  | { t: 'pong'; c: number; now: number };
