// Prayer times for Cairo (Egyptian General Authority of Survey: Fajr 19.5°, Isha 17.5°, Asr by the shadow
// equal to the object, as in Egypt), worked out from the sun's position. Times come back as UTC instants, so
// Egypt's summer time needs no special handling.
const LAT = 30.0444, LON = 31.2357;
const FAJR = 19.5, ISHA = 17.5;
// a minute or two of care, as Egyptian calendars do (never call it early)
const SAFETY: Record<PrayerName, number> = { fajr: 1, dhuhr: 2, asr: 1, maghrib: 2, isha: 1 };

export type PrayerName = 'fajr' | 'dhuhr' | 'asr' | 'maghrib' | 'isha';
export const PRAYERS: PrayerName[] = ['fajr', 'dhuhr', 'asr', 'maghrib', 'isha'];

const rad = (d: number) => d * Math.PI / 180, deg = (r: number) => r * 180 / Math.PI;
const fix = (a: number, b: number) => ((a % b) + b) % b;

/** The sun on a day (Julian date): its declination and the equation of time (hours). */
function sun(jd: number) {
  const D = jd - 2451545.0;
  const g = fix(357.529 + 0.98560028 * D, 360), q = fix(280.459 + 0.98564736 * D, 360);
  const L = fix(q + 1.915 * Math.sin(rad(g)) + 0.020 * Math.sin(rad(2 * g)), 360);
  const e = 23.439 - 0.00000036 * D;
  const RA = fix(deg(Math.atan2(Math.cos(rad(e)) * Math.sin(rad(L)), Math.cos(rad(L)))) / 15, 24);
  return { decl: deg(Math.asin(Math.sin(rad(e)) * Math.sin(rad(L)))), eqt: q / 15 - RA };
}

/** The five prayer times on the Cairo calendar day of `day` (any instant in it), as UTC milliseconds. */
export function prayerTimes(day: Date): Record<PrayerName, number> {
  // the calendar date in Cairo
  const ymd = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(day);
  const [y, m, d] = ymd.split('-').map(Number);
  const midnightUtc = Date.UTC(y, m - 1, d);
  const jd = midnightUtc / 86400000 + 2440587.5;
  // noon in UTC hours, refined once at that hour
  let s = sun(jd + 0.5);
  const noon = 12 - LON / 15 - s.eqt;
  s = sun(jd + noon / 24);
  const hourAngle = (alt: number) => deg(Math.acos((Math.sin(rad(alt)) - Math.sin(rad(LAT)) * Math.sin(rad(s.decl))) / (Math.cos(rad(LAT)) * Math.cos(rad(s.decl))))) / 15;
  const asrAlt = deg(Math.atan(1 / (1 + Math.tan(rad(Math.abs(LAT - s.decl))))));
  const hours: Record<PrayerName, number> = {
    fajr: noon - hourAngle(-FAJR),
    dhuhr: noon,
    asr: noon + hourAngle(asrAlt),
    maghrib: noon + hourAngle(-0.833),
    isha: noon + hourAngle(-ISHA),
  };
  const out = {} as Record<PrayerName, number>;
  for (const p of PRAYERS) out[p] = midnightUtc + Math.round((hours[p] * 60 + SAFETY[p])) * 60000;
  return out;
}

export const PRAYER_TEXT: Record<PrayerName, { ar: [string, string]; en: [string, string] }> = {
  fajr: { ar: ['أذان الفجر', 'حان الآن موعد صلاة الفجر (بتوقيت القاهرة)'], en: ['Fajr', 'It is time for the Fajr prayer (Cairo time)'] },
  dhuhr: { ar: ['أذان الظهر', 'حان الآن موعد صلاة الظهر (بتوقيت القاهرة)'], en: ['Dhuhr', 'It is time for the Dhuhr prayer (Cairo time)'] },
  asr: { ar: ['أذان العصر', 'حان الآن موعد صلاة العصر (بتوقيت القاهرة)'], en: ['Asr', 'It is time for the Asr prayer (Cairo time)'] },
  maghrib: { ar: ['أذان المغرب', 'حان الآن موعد صلاة المغرب (بتوقيت القاهرة)'], en: ['Maghrib', 'It is time for the Maghrib prayer (Cairo time)'] },
  isha: { ar: ['أذان العشاء', 'حان الآن موعد صلاة العشاء (بتوقيت القاهرة)'], en: ['Isha', 'It is time for the Isha prayer (Cairo time)'] },
};
