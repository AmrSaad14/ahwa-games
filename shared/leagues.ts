// Leagues: each game has its own, from bronze to diamond, worked out from league points (LP) won in rated
// online games during the season (the calendar month, Cairo time). A win gives LP, a loss takes a few (never below
// zero). At the end of the season everyone gets a reward for the
// highest league they reached in each game, and the points start from zero again.
export const LEAGUES = [
  { id: 'bronze', ar: 'برونز', en: 'Bronze', from: 0, reward: 5_000 },
  { id: 'silver', ar: 'فضة', en: 'Silver', from: 300, reward: 15_000 },
  { id: 'gold', ar: 'دهب', en: 'Gold', from: 700, reward: 40_000 },
  { id: 'platinum', ar: 'بلاتين', en: 'Platinum', from: 1200, reward: 100_000 },
  { id: 'diamond', ar: 'ماس', en: 'Diamond', from: 1800, reward: 250_000 },
] as const;
export const LP = { win: 30, loss: -15 };
/** The league (index) for a number of league points. */
export const leagueOf = (lp: number) => { let i = 0; for (let k = 0; k < LEAGUES.length; k++) if (lp >= LEAGUES[k].from) i = k; return i; };
/** A league for the app: which one, the points, and how far to the next. */
export function leagueView(lp: number, peak: number) {
  const i = leagueOf(lp), next = LEAGUES[i + 1];
  return { league: LEAGUES[i].id, lp, peak: LEAGUES[leagueOf(peak)].id, next: next ? next.from : null, from: LEAGUES[i].from };
}
