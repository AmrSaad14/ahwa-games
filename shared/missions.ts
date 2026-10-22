// Daily missions: three small tasks each day (Cairo time), each paying coins, XP and season points.
// The same three for a player all day (picked from the list by a hash of the player and the day).
import type { GameId } from './engine';

export interface MissionDef {
  id: string;
  game: GameId | 'any';
  kind: 'play' | 'win';
  need: number;
  coins: number;
  xp: number;
  online?: boolean;    // only games against people online count
  tier: 1 | 2 | 3;     // 1 easy, 2 medium, 3 hard: a day gets one of each
}

export const MISSIONS: MissionDef[] = [
  { id: 'play_domino_2', game: 'domino', kind: 'play', need: 2, coins: 15_000, xp: 30, tier: 1 },
  { id: 'play_tawla_1', game: 'tawla', kind: 'play', need: 1, coins: 10_000, xp: 20, tier: 1 },
  { id: 'play_chess_1', game: 'chess', kind: 'play', need: 1, coins: 10_000, xp: 20, tier: 1 },
  { id: 'play_ludo_2', game: 'ludo', kind: 'play', need: 2, coins: 15_000, xp: 30, tier: 1 },
  { id: 'play_any_3', game: 'any', kind: 'play', need: 3, coins: 15_000, xp: 30, tier: 1 },
  { id: 'win_domino_1', game: 'domino', kind: 'win', need: 1, coins: 20_000, xp: 40, tier: 2 },
  { id: 'win_tawla_1', game: 'tawla', kind: 'win', need: 1, coins: 20_000, xp: 40, tier: 2 },
  { id: 'win_chess_1', game: 'chess', kind: 'win', need: 1, coins: 25_000, xp: 50, tier: 2 },
  { id: 'win_ludo_1', game: 'ludo', kind: 'win', need: 1, coins: 20_000, xp: 40, tier: 2 },
  { id: 'win_any_3', game: 'any', kind: 'win', need: 3, coins: 40_000, xp: 80, tier: 3 },
  { id: 'win_domino_2', game: 'domino', kind: 'win', need: 2, coins: 35_000, xp: 60, tier: 3 },
  { id: 'online_any_2', game: 'any', kind: 'play', need: 2, coins: 30_000, xp: 60, online: true, tier: 3 },
  { id: 'online_win_1', game: 'any', kind: 'win', need: 1, coins: 40_000, xp: 80, online: true, tier: 3 },
];
export const missionById = (id: string) => MISSIONS.find(m => m.id === id);

/** Season points for finishing a mission. */
export const MISSION_SP = 60;

function hash(s: string): number {
  let h = 2166136261;
  for (let i = 0; i < s.length; i++) h = Math.imul(h ^ s.charCodeAt(i), 16777619);
  return h >>> 0;
}

/** Today's three missions for a player: one easy, one medium, one hard. */
export function missionsFor(uid: string, day: string): MissionDef[] {
  const out: MissionDef[] = [];
  for (const tier of [1, 2, 3] as const) {
    const pool = MISSIONS.filter(m => m.tier === tier && !out.some(o => o.game !== 'any' && o.game === m.game && o.kind === m.kind));
    out.push(pool[hash(uid + '|' + day + '|' + tier) % pool.length]);
  }
  return out;
}

/** Does a finished game count toward a mission? */
export function counts(m: MissionDef, game: GameId, won: boolean, online: boolean): boolean {
  if (m.game !== 'any' && m.game !== game) return false;
  if (m.online && !online) return false;
  return m.kind === 'play' || won;
}
