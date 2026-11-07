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
