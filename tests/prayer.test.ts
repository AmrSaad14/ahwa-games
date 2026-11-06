import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Store } from '../server/db';
import { Push } from '../server/push';
import { prayerTimes, PRAYERS } from '../server/prayer';

const cairo = (t: number) => new Date(t).toLocaleTimeString('en-GB', { timeZone: 'Africa/Cairo', hour: '2-digit', minute: '2-digit' });

test('prayer times for Cairo are in the right order and close to the published calendar', () => {
  const t = prayerTimes(new Date('2026-01-27T10:00:00Z'));
  for (let i = 1; i < PRAYERS.length; i++) assert.ok(t[PRAYERS[i]] > t[PRAYERS[i - 1]]);
  // winter (UTC+2): Fajr about 05:18, Dhuhr about 12:09, Maghrib about 17:30
  const min = (s: string) => { const [h, m] = s.split(':').map(Number); return h * 60 + m; };
  assert.ok(Math.abs(min(cairo(t.fajr)) - min('05:18')) <= 3, cairo(t.fajr));
  assert.ok(Math.abs(min(cairo(t.dhuhr)) - min('12:09')) <= 3, cairo(t.dhuhr));
  assert.ok(Math.abs(min(cairo(t.maghrib)) - min('17:30')) <= 3, cairo(t.maghrib));
  // summer time (UTC+3) is handled by the calendar: Maghrib about 20:00 on 20 June
  const s = prayerTimes(new Date('2026-06-20T10:00:00Z'));
  assert.ok(Math.abs(min(cairo(s.maghrib)) - min('20:00')) <= 3, cairo(s.maghrib));
});

test('a prayer notification goes out once, to phones that did not turn it off', async () => {
  const store = new Store(':memory:');
  const a = store.createGuest('A', 0), b = store.createGuest('B', 1);
  store.savePushToken(a.id, 'tok-a', 'ar', 'android');
  store.savePushToken(b.id, 'tok-b', 'en', 'android');
  store.setPrayerPush(b.id, false);
  const sent: { token: string; title: string }[] = [];
  const push = new Push(store, { serviceAccount: JSON.stringify({ client_email: 'x', private_key: 'y', project_id: 'p' }), times: '', tz: 'Africa/Cairo' });
  (push as unknown as { send: (t: string, ti: string) => Promise<boolean> }).send = async (token, title) => { sent.push({ token, title }); return true; };
  const asr = prayerTimes(new Date('2026-11-07T10:00:00Z')).asr;
  assert.equal(await push.prayerTick(new Date(asr - 60_000)), 0, 'not before the time');
  assert.equal(await push.prayerTick(new Date(asr + 30_000)), 1);
  assert.deepEqual(sent, [{ token: 'tok-a', title: 'أذان العصر' }]);
  assert.equal(await push.prayerTick(new Date(asr + 90_000)), 0, 'only once');
});
