// Names for the computer players that fill seats in matchmade games and the tournament, so a table reads
// like the café (the game still marks them as computer players).
// Characters: 0 Am Fathi, 1 Captain Sami, 2 Ziko, 3 Mona, 4 Nada, 5 Hajj Mahmoud.
const MEN = ['Ahmed', 'Mido', 'Hamada', 'Karim', 'Sherif', 'Hossam', 'Amr', 'Mostafa', 'Yousef', 'Omar', 'Mahmoud', 'Hany', 'Tamer', 'Wael',
  'Ayman', 'Khaled', 'Islam', 'Bassem', 'Ramy', 'Sayed', 'Zizo', 'Mena', 'Abdo', 'Fares', 'Ziad', 'Shady', 'Hazem', 'Adel',
  'أحمد', 'محمود', 'حمادة', 'كريم', 'عمرو', 'مصطفى', 'يوسف', 'شريف', 'هاني', 'سيد', 'عبده', 'زياد', 'حازم', 'عادل'];
const WOMEN = ['Salma', 'Yasmin', 'Mariam', 'Hager', 'Rana', 'Dina', 'Aya', 'Menna', 'Farida', 'Nour', 'Habiba', 'Rehab',
  'سلمى', 'ياسمين', 'مريم', 'هاجر', 'رنا', 'دينا', 'آية', 'منة', 'فريدة', 'نور'];
const TAGS = ['', '', '', '', '_10', '7', '_eg', '99', '22', '_x', '2005', '.m', '_king', '11'];

const pick = <T>(a: T[]) => a[Math.floor(Math.random() * a.length)];

/** A name and a character for a computer player (not one of the characters already at the table). */
export function botIdentity(usedCh: number[], rating: number): { name: string; ch: number; rating: number } {
  const free = [0, 1, 2, 3, 4, 5].filter(c => !usedCh.includes(c));
  const ch = free.length ? pick(free) : Math.floor(Math.random() * 6);
  const female = ch === 3 || ch === 4;
  const base = pick(female ? WOMEN : MEN);
  const latin = /^[A-Za-z]/.test(base);
  const name = latin ? base + pick(TAGS) : base;
  return { name: name.slice(0, 14), ch, rating: Math.max(600, Math.round(rating + (Math.random() - 0.5) * 160)) };
}
