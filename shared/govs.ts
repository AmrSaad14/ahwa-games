// Egypt's 27 governorates, for the governorate leaderboard ("governorate of the week").
export const GOVS: { id: string; ar: string; en: string }[] = [
  { id: 'cairo', ar: 'القاهرة', en: 'Cairo' }, { id: 'giza', ar: 'الجيزة', en: 'Giza' }, { id: 'alex', ar: 'الإسكندرية', en: 'Alexandria' },
  { id: 'qalyubia', ar: 'القليوبية', en: 'Qalyubia' }, { id: 'sharqia', ar: 'الشرقية', en: 'Sharqia' }, { id: 'dakahlia', ar: 'الدقهلية', en: 'Dakahlia' },
  { id: 'gharbia', ar: 'الغربية', en: 'Gharbia' }, { id: 'monufia', ar: 'المنوفية', en: 'Monufia' }, { id: 'beheira', ar: 'البحيرة', en: 'Beheira' },
  { id: 'kafr', ar: 'كفر الشيخ', en: 'Kafr El Sheikh' }, { id: 'damietta', ar: 'دمياط', en: 'Damietta' }, { id: 'portsaid', ar: 'بورسعيد', en: 'Port Said' },
  { id: 'ismailia', ar: 'الإسماعيلية', en: 'Ismailia' }, { id: 'suez', ar: 'السويس', en: 'Suez' }, { id: 'fayoum', ar: 'الفيوم', en: 'Faiyum' },
  { id: 'benisuef', ar: 'بني سويف', en: 'Beni Suef' }, { id: 'minya', ar: 'المنيا', en: 'Minya' }, { id: 'assiut', ar: 'أسيوط', en: 'Asyut' },
  { id: 'sohag', ar: 'سوهاج', en: 'Sohag' }, { id: 'qena', ar: 'قنا', en: 'Qena' }, { id: 'luxor', ar: 'الأقصر', en: 'Luxor' },
  { id: 'aswan', ar: 'أسوان', en: 'Aswan' }, { id: 'redsea', ar: 'البحر الأحمر', en: 'Red Sea' }, { id: 'newvalley', ar: 'الوادي الجديد', en: 'New Valley' },
  { id: 'matrouh', ar: 'مطروح', en: 'Matrouh' }, { id: 'northsinai', ar: 'شمال سيناء', en: 'North Sinai' }, { id: 'southsinai', ar: 'جنوب سيناء', en: 'South Sinai' },
];
export const isGov = (id: unknown): id is string => typeof id === 'string' && GOVS.some(g => g.id === id);
/** A governorate's weekly score: the points of its best 20 players that week (so a big governorate doesn't win
 *  just by being big). Its players with at least `minGames` games that week share the prize when it wins. */
export const GOV_WEEK = { top: 20, prize: 10_000, minGames: 5 };

/** A governorate comes from where the player is, and stays for 60 days before it can change. */
export const GOV_LOCK_DAYS = 60;

/** Towns of each governorate (lat, lon): a place belongs to the governorate of its nearest town. Several towns a
 *  governorate so the borders come out about right (the Nile between Cairo and Giza is the hardest). */
const TOWNS: [number, number, string][] = [
  [30.044, 31.236, 'cairo'], [30.056, 31.33, 'cairo'], [29.96, 31.25, 'cairo'], [29.85, 31.33, 'cairo'], [30.03, 31.47, 'cairo'],
  [30.09, 31.32, 'cairo'], [30.12, 31.61, 'cairo'], [30.02, 31.76, 'cairo'], [30.06, 31.22, 'cairo'], [30.1, 31.24, 'cairo'], [30.13, 31.4, 'cairo'],
  [30.013, 31.209, 'giza'], [30.038, 31.205, 'giza'], [29.99, 31.15, 'giza'], [29.96, 30.93, 'giza'], [30.04, 30.98, 'giza'], [30.075, 31.2, 'giza'],
  [29.62, 31.26, 'giza'], [29.85, 31.27, 'giza'], [28.36, 28.87, 'giza'],
  [31.2, 29.92, 'alex'], [31.28, 30.01, 'alex'], [30.92, 29.53, 'alex'], [31.03, 29.8, 'alex'],
  [30.46, 31.18, 'qalyubia'], [30.128, 31.244, 'qalyubia'], [30.18, 31.2, 'qalyubia'], [30.21, 31.37, 'qalyubia'], [30.23, 31.48, 'qalyubia'], [30.19, 31.13, 'qalyubia'],
  [30.587, 31.502, 'sharqia'], [30.3, 31.75, 'sharqia'], [30.42, 31.56, 'sharqia'], [30.54, 31.67, 'sharqia'], [30.73, 31.8, 'sharqia'], [30.51, 31.35, 'sharqia'], [30.86, 31.92, 'sharqia'],
  [31.04, 31.38, 'dakahlia'], [30.72, 31.26, 'dakahlia'], [31.19, 31.52, 'dakahlia'], [31.16, 31.94, 'dakahlia'], [30.94, 31.29, 'dakahlia'], [31.21, 31.36, 'dakahlia'],
  [30.79, 31.0, 'gharbia'], [30.97, 31.17, 'gharbia'], [30.82, 30.82, 'gharbia'], [30.71, 31.24, 'gharbia'], [30.96, 31.24, 'gharbia'], [30.94, 30.82, 'gharbia'],
  [30.55, 31.01, 'monufia'], [30.46, 30.93, 'monufia'], [30.36, 30.53, 'monufia'], [30.3, 30.98, 'monufia'], [30.56, 31.15, 'monufia'], [30.68, 30.95, 'monufia'],
  [31.04, 30.47, 'beheira'], [31.13, 30.13, 'beheira'], [31.4, 30.42, 'beheira'], [31.31, 30.3, 'beheira'], [30.4, 30.35, 'beheira'], [30.76, 30.7, 'beheira'], [30.91, 30.17, 'beheira'],
  [31.11, 30.94, 'kafr'], [31.13, 30.65, 'kafr'], [31.55, 31.09, 'kafr'], [31.27, 30.79, 'kafr'], [31.17, 31.22, 'kafr'],
  [31.42, 31.81, 'damietta'], [31.44, 31.68, 'damietta'], [31.33, 31.72, 'damietta'],
  [31.26, 32.3, 'portsaid'], [31.25, 32.32, 'portsaid'],
  [30.6, 32.27, 'ismailia'], [30.33, 32.3, 'ismailia'], [30.86, 32.32, 'ismailia'], [30.55, 31.95, 'ismailia'],
  [29.97, 32.53, 'suez'], [29.6, 32.33, 'suez'],
  [29.31, 30.84, 'fayoum'], [29.41, 30.87, 'fayoum'], [29.24, 30.79, 'fayoum'], [29.48, 30.96, 'fayoum'],
  [29.07, 31.1, 'benisuef'], [29.34, 31.2, 'benisuef'], [28.92, 30.98, 'benisuef'], [29.15, 31.12, 'benisuef'],
  [28.11, 30.75, 'minya'], [27.73, 30.84, 'minya'], [28.31, 30.71, 'minya'], [28.65, 30.84, 'minya'], [28.5, 30.8, 'minya'], [27.64, 30.85, 'minya'],
  [27.18, 31.18, 'assiut'], [27.56, 30.81, 'assiut'], [27.31, 30.97, 'assiut'], [27.27, 31.15, 'assiut'], [27.04, 31.32, 'assiut'], [27.44, 30.82, 'assiut'],
  [26.56, 31.69, 'sohag'], [26.56, 31.75, 'sohag'], [26.34, 31.89, 'sohag'], [26.77, 31.5, 'sohag'], [26.9, 31.43, 'sohag'], [26.24, 32.0, 'sohag'],
  [26.16, 32.72, 'qena'], [26.05, 32.24, 'qena'], [25.91, 32.76, 'qena'], [26.12, 32.47, 'qena'], [26.12, 32.1, 'qena'],
  [25.69, 32.64, 'luxor'], [25.29, 32.55, 'luxor'], [25.62, 32.54, 'luxor'],
  [24.09, 32.9, 'aswan'], [24.47, 32.95, 'aswan'], [24.98, 32.87, 'aswan'], [24.4, 32.93, 'aswan'], [22.34, 31.63, 'aswan'],
  [27.26, 33.81, 'redsea'], [26.73, 33.94, 'redsea'], [26.1, 34.28, 'redsea'], [25.07, 34.89, 'redsea'], [28.36, 33.08, 'redsea'], [23.13, 35.59, 'redsea'], [22.2, 36.4, 'redsea'],
  [25.44, 30.55, 'newvalley'], [25.49, 28.98, 'newvalley'], [27.06, 27.97, 'newvalley'], [24.67, 30.6, 'newvalley'], [22.5, 28.5, 'newvalley'],
  [31.35, 27.24, 'matrouh'], [30.83, 28.95, 'matrouh'], [29.2, 25.52, 'matrouh'], [31.55, 25.16, 'matrouh'], [31.03, 28.43, 'matrouh'], [31.61, 25.93, 'matrouh'],
  [31.13, 33.8, 'northsinai'], [31.21, 34.11, 'northsinai'], [31.28, 34.24, 'northsinai'], [31.02, 33.0, 'northsinai'], [29.91, 33.75, 'northsinai'], [30.47, 33.79, 'northsinai'],
  [27.91, 34.33, 'southsinai'], [28.5, 34.51, 'southsinai'], [29.03, 34.66, 'southsinai'], [29.49, 34.9, 'southsinai'], [28.24, 33.62, 'southsinai'],
  [28.56, 33.95, 'southsinai'], [29.59, 32.71, 'southsinai'], [28.9, 33.2, 'southsinai'],
];

const km = (a: number, b: number, c: number, d: number) => {
  const r = Math.PI / 180, x = (d - b) * r * Math.cos(((a + c) / 2) * r), y = (c - a) * r;
  return Math.sqrt(x * x + y * y) * 6371;
};

/** The governorate at a place, or null outside Egypt (or too far from any town to tell). */
export function govAt(lat: number, lon: number): string | null {
  if (!Number.isFinite(lat) || !Number.isFinite(lon)) return null;
  if (lat < 21.7 || lat > 31.8 || lon < 24.6 || lon > 37) return null;
  // Sinai's eastern border (Rafah to Taba) and the Gulf of Aqaba: what lies east is not Egypt
  if (lat >= 29.45 && lon > (lat > 31.32 ? 34.24 : 34.24 + (31.32 - lat) / (31.32 - 29.49) * (34.9 - 34.24)) + 0.03) return null;
  if (lat >= 27.8 && lat < 29.45 && lon > 34.95) return null;
  let best: string | null = null, bestKm = Infinity;
  for (const [a, b, g] of TOWNS) { const d = km(lat, lon, a, b); if (d < bestKm) { bestKm = d; best = g; } }
  return bestKm <= 150 ? best : null;
}
