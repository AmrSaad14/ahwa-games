// Titles: a short name shown under a player's name (their card, their seat at the table, the leaderboard),
// each with its own design. The team makes and gives them from the dashboard; the best player of each game
// every month gets "<game> player of the month" by itself. The catalog below is what the game ships with; the
// dashboard can change it (kept in the database).
import type { GameId } from './engine';

/** The icons a title can carry (white shapes, tinted with the title's text colour). 24×24 SVG paths. */
export const TITLE_ICONS: Record<string, string> = {
  none: '',
  crown: 'M3 18h18v2.5H3zM2.5 6.5l5 4L12 3.5l4.5 7 5-4L19.5 16h-15z',
  star: 'M12 2l2.9 6.6 7.1.6-5.4 4.7 1.7 7-6.3-3.9-6.3 3.9 1.7-7L2 9.2l7.1-.6z',
  medal: 'M7 2h4l1.5 5L14 2h4l-3.2 7.4A6 6 0 1 1 9.2 9.4zM12 12.2l1.1 2.2 2.4.3-1.8 1.7.5 2.4-2.2-1.2-2.2 1.2.5-2.4-1.8-1.7 2.4-.3z',
  trophy: 'M7 3h10v2h3.5v3a4.5 4.5 0 0 1-4.3 4.5A5 5 0 0 1 13 15v2.5h3.5V21h-9v-3.5H11V15a5 5 0 0 1-3.2-2.5A4.5 4.5 0 0 1 3.5 8V5H7zM5.5 7v1a2.5 2.5 0 0 0 1.8 2.4A9 9 0 0 1 7 8V7zm13 0H17v1a9 9 0 0 1-.3 2.4A2.5 2.5 0 0 0 18.5 8z',
  diamond: 'M6.5 3h11l4.5 6.5L12 21.5 2 9.5zM8 5L5.6 8.5h4.1L11 5zm5 0l1.3 3.5h4.1L16 5zm-3.3 5.5L12 17l2.3-6.5z',
  shield: 'M12 2l8.5 3.2V11c0 5.2-3.6 9.6-8.5 11-4.9-1.4-8.5-5.8-8.5-11V5.2zm0 5l-1.5 3.1-3.4.4 2.5 2.3-.7 3.4 3.1-1.7 3.1 1.7-.7-3.4 2.5-2.3-3.4-.4z',
  flame: 'M12 1.5c1.2 4.3 5.8 6.4 5.8 12.2a5.8 5.8 0 0 1-11.6 0c0-2.8 1.3-4.6 2.8-5.8.2 2.2 1.1 3.4 2.2 3.9-.6-3.6-.2-7.1.8-10.3z',
  domino: 'M7 1.5h10a2.5 2.5 0 0 1 2.5 2.5v16a2.5 2.5 0 0 1-2.5 2.5H7A2.5 2.5 0 0 1 4.5 20V4A2.5 2.5 0 0 1 7 1.5zm-.5 9.6v1.8h11v-1.8zM12 4.6a1.6 1.6 0 1 0 0 3.2 1.6 1.6 0 0 0 0-3.2zm-3 9.6a1.6 1.6 0 1 0 0 3.2 1.6 1.6 0 0 0 0-3.2zm6 0a1.6 1.6 0 1 0 0 3.2 1.6 1.6 0 0 0 0-3.2z',
  dice: 'M5.5 2.5h13a3 3 0 0 1 3 3v13a3 3 0 0 1-3 3h-13a3 3 0 0 1-3-3v-13a3 3 0 0 1 3-3zM8 6.3a1.7 1.7 0 1 0 0 3.4 1.7 1.7 0 0 0 0-3.4zm4 4a1.7 1.7 0 1 0 0 3.4 1.7 1.7 0 0 0 0-3.4zm4 4a1.7 1.7 0 1 0 0 3.4 1.7 1.7 0 0 0 0-3.4z',
  knight: 'M7 22h11.5v-2.6c0-3.4-1.3-5.6-1.3-9.2C17.2 5.4 14 2 9.6 2L8.5 4 6.4 5.2 4 9.6l1.7 1.9 2.9-.8 1.8-.9c-.6 2.6-3.4 4.2-3.4 7.6V19H7z',
  fez: 'M8.2 4.5h7.6l2.7 15h-13zM15.8 4.5c1.6.4 2.6 1.6 3 3.6l-1.3.5c-.3-1.4-1-2.3-2-2.6z',
};
export const TITLE_EFFECTS = ['none', 'shine', 'glow', 'pulse'] as const;
export type TitleEffect = typeof TITLE_EFFECTS[number];

/** How a title looks: a pill with a two-colour gradient, a border, an icon and maybe a moving effect. */
export interface TitleStyle {
  bg: [string, string];   // gradient, left to right (#rrggbb)
  fg: string;             // text and icon
  border: string;
  icon: string;           // a key of TITLE_ICONS
  effect: TitleEffect;    // shine: a light sweeps across · glow: the border breathes · pulse: it beats gently
}
export interface TitleDef {
  id: string;
  ar: string;
  en: string;
  style: TitleStyle;
  /** Given by the game itself: the best player of `game` every month. */
  monthly?: GameId;
}

const st = (bg1: string, bg2: string, fg: string, border: string, icon: string, effect: TitleEffect): TitleStyle => ({ bg: [bg1, bg2], fg, border, icon, effect });

export const DEFAULT_TITLES: TitleDef[] = [
  // the monthly ones: the best of each game
  { id: 'potm_domino', ar: 'لاعب الدومينو للشهر', en: 'Domino Player of the Month', monthly: 'domino', style: st('#11804f', '#063b25', '#ffd76a', '#ffd76a', 'domino', 'shine') },
  { id: 'potm_tawla', ar: 'لاعب الطاولة للشهر', en: 'Tawla Player of the Month', monthly: 'tawla', style: st('#9c2430', '#4a0d14', '#ffd76a', '#ffd76a', 'dice', 'shine') },
  { id: 'potm_chess', ar: 'لاعب الشطرنج للشهر', en: 'Chess Player of the Month', monthly: 'chess', style: st('#2c4bb0', '#0e1a4a', '#ffd76a', '#ffd76a', 'knight', 'shine') },
  { id: 'potm_ludo', ar: 'لاعب اللودو للشهر', en: 'Ludo Player of the Month', monthly: 'ludo', style: st('#7b3fc4', '#3a1666', '#ffd76a', '#ffd76a', 'star', 'shine') },
  // given by the team
  { id: 'legend', ar: 'أسطورة القهوة', en: 'Café Legend', style: st('#ffe08a', '#b8862b', '#2a1a05', '#fff6d6', 'crown', 'shine') },
  { id: 'domino_king', ar: 'ملك الدومينو', en: 'Domino King', style: st('#2b2b2b', '#0d0d0d', '#f3cd62', '#f3cd62', 'domino', 'glow') },
  { id: 'tawla_sultan', ar: 'سلطان الطاولة', en: 'Tawla Sultan', style: st('#8a2626', '#3d0d0d', '#ffd9a0', '#e0a03a', 'dice', 'glow') },
  { id: 'chess_vizier', ar: 'وزير الشطرنج', en: 'Chess Vizier', style: st('#26397f', '#0f1838', '#e6ecff', '#9fb4ff', 'knight', 'glow') },
  { id: 'ludo_boss', ar: 'معلّم اللودو', en: 'Ludo Boss', style: st('#2f9e57', '#1d6a8a', '#ffffff', '#ffd84d', 'star', 'none') },
  { id: 'champion', ar: 'بطل البطولة', en: 'Tournament Champion', style: st('#6a32a0', '#2a1140', '#ffe08a', '#ffe08a', 'trophy', 'glow') },
  { id: 'weekly_star', ar: 'نجم الأسبوع', en: 'Star of the Week', style: st('#ffd24d', '#e07b1e', '#2a1405', '#fff3c4', 'star', 'pulse') },
  { id: 'vip', ar: 'VIP', en: 'VIP', style: st('#18c3d6', '#0a5d8f', '#ffffff', '#c9f7ff', 'diamond', 'shine') },
  { id: 'golden_hands', ar: 'إيده تتلف في حرير', en: 'Golden Hands', style: st('#ff7a59', '#c2185b', '#ffffff', '#ffd1c2', 'flame', 'pulse') },
  { id: 'cafe_master', ar: 'معلّم القهوة', en: 'Café Master', style: st('#4a3526', '#1b120c', '#f4e6c8', '#c9a25a', 'fez', 'none') },
  { id: 'early', ar: 'من الأوائل', en: 'Early Player', style: st('#b0743f', '#5e3519', '#ffe7cc', '#d9a066', 'medal', 'none') },
  { id: 'team', ar: 'فريق بورد باتلز', en: 'Board Battles Team', style: st('#d4574a', '#8f1d1d', '#ffffff', '#ffb3a8', 'shield', 'glow') },
];

const HEX = /^#[0-9a-f]{6}$/i;
const hex = (v: unknown, d: string) => typeof v === 'string' && HEX.test(v) ? v.toLowerCase() : d;
const GAMES: GameId[] = ['domino', 'tawla', 'chess', 'ludo'];

/** Checks a title from the dashboard (anything wrong keeps `d`'s value). */
export function cleanTitle(v: unknown, d?: TitleDef): TitleDef | null {
  const x = (v ?? {}) as Partial<TitleDef> & { style?: Partial<TitleStyle> };
  const id = String(x.id ?? d?.id ?? '').toLowerCase().replace(/[^a-z0-9_]/g, '').slice(0, 32);
  if (id.length < 2) return null;
  const ar = typeof x.ar === 'string' && x.ar.trim() ? x.ar.trim().slice(0, 30) : d?.ar;
  const en = typeof x.en === 'string' && x.en.trim() ? x.en.trim().slice(0, 30) : d?.en ?? ar;
  if (!ar || !en) return null;
  const s = x.style ?? {}, ds = d?.style ?? DEFAULT_TITLES[4].style;
  const bg = Array.isArray(s.bg) ? s.bg : ds.bg;
  const monthly = x.monthly === undefined ? d?.monthly : GAMES.includes(x.monthly as GameId) ? x.monthly : undefined;
  return {
    id, ar, en,
    style: {
      bg: [hex(bg[0], ds.bg[0]), hex(bg[1], ds.bg[1])],
      fg: hex(s.fg, ds.fg), border: hex(s.border, ds.border),
      icon: typeof s.icon === 'string' && s.icon in TITLE_ICONS ? s.icon : ds.icon,
      effect: TITLE_EFFECTS.includes(s.effect as TitleEffect) ? s.effect as TitleEffect : ds.effect,
    },
    ...(monthly ? { monthly } : {}),
  };
}

/** The month a moment falls in, in Cairo: "2026-10". */
export { seasonOf as monthOf } from './season';
