// Events: Ramadan, Eid or anything else, set up in the dashboard with dates. While one is on, season points and
// mission coins can be multiplied, players who play enough games during it get its title, and its limited skins
// are in the shop (only while it is on).
export type EventKind = 'ramadan' | 'eid' | 'custom';
export interface EventDef {
  id: string;
  kind: EventKind;
  ar: string; en: string;        // its name
  from: number; to: number;      // when it runs (ms)
  spMult: number;                // season points ×
  missionMult: number;           // mission coins ×
  titleId: string;               // a title for playing `titleGames` online games during it ('' for none)
  titleGames: number;
}

/** The events (the dashboard's list, set by server/settings.ts). */
export const EVENTS: EventDef[] = [];

/** The event on now (the first one whose dates include `now`). */
export const activeEvent = (now = Date.now()): EventDef | null => EVENTS.find(e => e.from <= now && now < e.to) ?? null;

/** Which kinds of event sell which limited skins. */
export const EVENT_SKIN_KIND = (skinEvent: string | undefined, ev: EventDef | null) => !!skinEvent && !!ev && ev.kind === skinEvent;

const clean = (s: unknown, max: number) => typeof s === 'string' ? s.trim().slice(0, max) : '';
export function cleanEvent(v: unknown): EventDef | null {
  const x = (v ?? {}) as Partial<EventDef>;
  const id = String(x.id ?? '').toLowerCase().replace(/[^a-z0-9_]/g, '').slice(0, 32);
  const from = Math.round(Number(x.from)), to = Math.round(Number(x.to));
  if (id.length < 2 || !Number.isFinite(from) || !Number.isFinite(to) || to <= from) return null;
  const ar = clean(x.ar, 40), en = clean(x.en, 40);
  if (!ar || !en) return null;
  const num = (n: unknown, lo: number, hi: number, d: number) => { const k = Number(n); return Number.isFinite(k) ? Math.min(hi, Math.max(lo, k)) : d; };
  return {
    id, kind: (['ramadan', 'eid', 'custom'] as const).includes(x.kind as EventKind) ? x.kind as EventKind : 'custom', ar, en, from, to,
    spMult: Math.round(num(x.spMult, 1, 5, 1) * 10) / 10, missionMult: Math.round(num(x.missionMult, 1, 5, 1) * 10) / 10,
    titleId: clean(x.titleId, 32), titleGames: Math.round(num(x.titleGames, 0, 1000, 0)),
  };
}
