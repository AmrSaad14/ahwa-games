// Push notifications with Firebase Cloud Messaging (FCM HTTP v1), and the twice-daily "come and play" reminders.
//
//   FIREBASE_SERVICE_ACCOUNT  the Firebase project's service account key (JSON, or the same JSON base64-encoded):
//                             Firebase console → Project settings → Service accounts → Generate new private key
//   PUSH_TIMES                when the reminders go out, Cairo time (default "14:00,20:00")
//   PUSH_TZ                   the time zone of PUSH_TIMES (default Africa/Cairo)
//   DAILY_PUSH_TIME           when signed-in players who haven't collected today's daily reward are reminded
//                             (default "18:00"; empty: no daily-reward reminder)
//
// Free hosts that sleep when nobody plays can't run a clock: point any free cron service (cron-job.org, for
// example) at PUBLIC_URL/api/cron/push?key=ADMIN_KEY every 15 minutes; it wakes the server and sends any reminder
// that is due. A reminder is sent at most once per time slot, even across restarts.
import { prayerTimes, PRAYERS, PRAYER_TEXT } from './prayer';
import crypto from 'node:crypto';
import type { Store } from './db';

interface ServiceAccount { client_email: string; private_key: string; project_id: string }
const b64url = (b: Buffer | string) => Buffer.from(b).toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');

/** The reminder texts, a different one each time (Arabic and English). */
const REMINDERS: { ar: [string, string]; en: [string, string] }[] = [
  { ar: ['القهوة مستنياك ☕', 'الترابيزة جاهزة والعيال مستنيين… العب دور واكسب كوينز'], en: ['The café is waiting ☕', 'The table is set and the regulars are waiting. Play a round and win coins'] },
  { ar: ['مكافأتك اليومية مستنياك 🎁', 'ادخل خد كوينز النهارده'], en: ['Your daily bonus is waiting 🎁', 'Come and grab today\'s free coins'] },
  { ar: ['دور دومينو؟ 🁣', 'عم فتحي بيقول محدش يقدر يغلبه… وريه'], en: ['A round of dominoes? 🁣', 'Am Fathi says nobody can beat him. Show him'] },
  { ar: ['طاولة على السريع 🎲', 'ارمي الزهر واكسب كوينز'], en: ['A quick game of tawla 🎲', 'Roll the dice and win coins'] },
  { ar: ['الشلة مستنياك 🃏', 'ادخل العب دور لودو مع صحابك'], en: ['Your friends are waiting 🃏', 'Jump in for a round of ludo'] },
  { ar: ['الكابتن سامي عايز ماتش شطرنج ♟️', 'ادخل العب ماتش سريع'], en: ['Captain Samy wants a chess match ♟️', 'Jump in for a quick game'] },
];

export class Push {
  private sa: ServiceAccount | null = null;
  private token: { value: string; until: number } | null = null;
  private times: string[];
  private dailyTime: string;
  private tz: string;
  private running = false;

  constructor(private store: Store, cfg: { serviceAccount: string; times: string; tz: string; dailyTime?: string }) {
    const raw = cfg.serviceAccount.trim();
    if (raw) {
      try { this.sa = JSON.parse(raw.startsWith('{') ? raw : Buffer.from(raw, 'base64').toString('utf8')) as ServiceAccount; }
      catch { console.error('[push] FIREBASE_SERVICE_ACCOUNT is not valid JSON'); }
    }
    this.times = cfg.times.split(',').map(s => s.trim()).filter(s => /^\d{1,2}:\d{2}$/.test(s));
    this.tz = cfg.tz;
    const d = (cfg.dailyTime ?? '18:00').trim();
    this.dailyTime = /^\d{1,2}:\d{2}$/.test(d) ? d : '';
  }
  get enabled() { return !!this.sa; }

  private async accessToken(): Promise<string> {
    if (this.token && this.token.until > Date.now() + 60_000) return this.token.value;
    const sa = this.sa!, now = Math.floor(Date.now() / 1000);
    const head = b64url(JSON.stringify({ alg: 'RS256', typ: 'JWT' }));
    const body = b64url(JSON.stringify({ iss: sa.client_email, scope: 'https://www.googleapis.com/auth/firebase.messaging', aud: 'https://oauth2.googleapis.com/token', iat: now, exp: now + 3600 }));
    const sig = b64url(crypto.createSign('RSA-SHA256').update(`${head}.${body}`).sign(sa.private_key));
    const r = await fetch('https://oauth2.googleapis.com/token', {
      method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer', assertion: `${head}.${body}.${sig}` }),
    });
    const j = await r.json() as { access_token?: string; expires_in?: number; error?: string };
    if (!j.access_token) throw new Error('Google token: ' + (j.error || r.status));
    this.token = { value: j.access_token, until: Date.now() + (j.expires_in ?? 3600) * 1000 };
    return j.access_token;
  }

  /** Sends one notification. Returns false when the phone's token is gone (the app was removed): it is forgotten. */
  async send(token: string, title: string, body: string, data: Record<string, string> = {}): Promise<boolean> {
    if (!this.sa) return false;
    const r = await fetch(`https://fcm.googleapis.com/v1/projects/${encodeURIComponent(this.sa.project_id)}/messages:send`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${await this.accessToken()}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: {
        token, notification: { title, body }, data,
        android: { priority: 'high', notification: { channel_id: 'reminders', sound: 'default' } },
      } }),
    });
    if (r.ok) return true;
    const text = await r.text().catch(() => '');
    if (r.status === 404 || /UNREGISTERED|INVALID_ARGUMENT.*token|registration-token-not-registered/i.test(text)) { this.store.dropPushToken(token); return false; }
    throw new Error(`FCM ${r.status}: ${text.slice(0, 200)}`);
  }

  /** A message to every phone of one player (cash-out paid, for example). */
  async toUser(uid: string, ar: [string, string], en: [string, string]) {
    if (!this.sa) return;
    for (const t of this.store.pushTokensOf(uid)) {
      const [title, body] = t.lang === 'en' ? en : ar;
      await this.send(t.token, title, body).catch(e => console.error('[push]', (e as Error).message));
    }
  }

  /** The current time slot that is due and not sent yet ("2026-10-20 14:00"), or null. Slots stay due for 3 hours. */
  private dueSlot(now = new Date(), times = this.times, key = 'push_slot_'): string | null {
    const parts = Object.fromEntries(new Intl.DateTimeFormat('en-CA', { timeZone: this.tz, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
      .formatToParts(now).map(p => [p.type, p.value]));
    const day = `${parts.year}-${parts.month}-${parts.day}`, mins = Number(parts.hour) * 60 + Number(parts.minute);
    for (const t of times) {
      const [h, m] = t.split(':').map(Number), at = h * 60 + m;
      if (mins >= at && mins < at + 180) {
        const slot = `${day} ${t}`;
        if (this.store.metaGet(key + t) !== slot) return slot;
      }
    }
    return null;
  }

  /** The daily-reward reminder: once a day, to signed-in players who haven't collected today's reward yet. */
  async dailyTick(now = new Date(), force = false): Promise<number> {
    if (!this.sa || (!this.dailyTime && !force)) return 0;
    const slot = force ? 'now' : this.dueSlot(now, [this.dailyTime], 'push_daily_');
    if (!slot) return 0;
    if (!force) this.store.metaSet('push_daily_' + this.dailyTime, slot);
    let sent = 0;
    for (const t of this.store.dailyPushTargets(2 * 3600_000, now.getTime())) {
      const streak = t.day > 1;
      const msg: [string, string] = t.lang === 'en'
        ? ['Your daily reward is waiting 🎁', streak ? `Day ${t.day} in a row: collect it before midnight to keep your streak` : 'Open the game and collect today\'s free coins']
        : ['مكافأتك اليومية مستنياك 🎁', streak ? `اليوم ${t.day} على التوالي: خدها قبل نص الليل عشان ما تخسرش السلسلة` : 'ادخل خد كوينز النهارده ببلاش'];
      try { if (await this.send(t.token, msg[0], msg[1], { kind: 'daily' })) sent++; }
      catch (e) { console.error('[push]', (e as Error).message); }
    }
    console.log(`[push] daily reward reminder ${slot}: sent to ${sent} phones`);
    return sent;
  }

  /** At each prayer time in Cairo (a few minutes' window, once each): a notification to every phone that wants it. */
  async prayerTick(now = new Date()): Promise<number> {
    if (!this.sa) return 0;
    const times = prayerTimes(now);
    for (const p of PRAYERS) {
      const at = times[p];
      if (now.getTime() < at || now.getTime() > at + 5 * 60_000) continue;
      const key = `push_prayer_${new Date(at).toISOString().slice(0, 10)}_${p}`;
      if (this.store.metaGet(key)) continue;
      this.store.metaSet(key, '1');   // mark first: a crash can't send it twice
      let sent = 0;
      for (const t of this.store.prayerPushTargets()) {
        const [title, body] = t.lang === 'en' ? PRAYER_TEXT[p].en : PRAYER_TEXT[p].ar;
        try { if (await this.send(t.token, title, body, { kind: 'prayer' })) sent++; }
        catch (e) { console.error('[push]', (e as Error).message); }
      }
      console.log(`[push] prayer ${p}: sent to ${sent} phones`);
      return sent;
    }
    return 0;
  }

  /** Sends the reminder if one is due. Called every minute, and by the cron URL. */
  async tick(now = new Date(), force = false): Promise<{ slot: string | null; sent: number }> {
    if (!this.sa || this.running) return { slot: null, sent: 0 };
    if (!force) await this.dailyTick(now).catch(e => console.error('[push]', (e as Error).message));
    if (!force) await this.prayerTick(now).catch(e => console.error('[push]', (e as Error).message));
    const slot = force ? 'now' : this.dueSlot(now);
    if (!slot) return { slot: null, sent: 0 };
    this.running = true;
    if (!force) this.store.metaSet('push_slot_' + slot.split(' ')[1], slot);   // mark first: a crash can't send it twice
    let sent = 0;
    try {
      const n = Number(this.store.metaGet('push_count') || '0');
      this.store.metaSet('push_count', String(n + 1));
      const msg = REMINDERS[n % REMINDERS.length];
      // players who were in the game in the last two hours don't need reminding
      for (const t of this.store.pushTargets(2 * 3600_000)) {
        const [title, body] = t.lang === 'en' ? msg.en : msg.ar;
        try { if (await this.send(t.token, title, body, { kind: 'reminder' })) sent++; }
        catch (e) { console.error('[push]', (e as Error).message); }
      }
      console.log(`[push] reminder ${slot}: sent to ${sent} phones`);
    } finally { this.running = false; }
    return { slot, sent };
  }
}
