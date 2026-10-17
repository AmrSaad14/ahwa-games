import type { CoinRow, PublicUser } from '../../shared/protocol';
import { ECONOMY } from '../../shared/gifts';
import { skinById } from '../../shared/skins';

/**
 * Demo builds only (no game server): a coin wallet kept in this browser, so stakes can be tried
 * against the computer. Real games settle coins on the server.
 */
export class DemoWallet {
  coins = 0;
  adFreeUntil = 0;
  skins: string[] = [];
  equip: Record<string, string> = {};
  rows: CoinRow[] = [];
  constructor() {
    try {
      const saved = JSON.parse(localStorage.getItem('dom.demoWallet') || 'null') as { coins: number; rows: CoinRow[]; day: string } | null;
      if (saved) { this.coins = saved.coins; this.rows = saved.rows; this.adFreeUntil = (saved as { adFreeUntil?: number }).adFreeUntil ?? 0; this.skins = (saved as { skins?: string[] }).skins ?? []; this.equip = (saved as { equip?: Record<string, string> }).equip ?? {}; }
      else this.add('welcome', ECONOMY.startCoins, ECONOMY.startCoins);
      const day = new Date().toISOString().slice(0, 10);
      if (saved?.day !== day) this.add('daily', ECONOMY.dailyBonus, ECONOMY.dailyBonus);
      this.save(day);
    } catch { if (!this.rows.length) { this.add('welcome', ECONOMY.startCoins, ECONOMY.startCoins); } }
  }
  /** Moves `change` coins and writes a history line showing `shown`. */
  add(kind: string, change: number, shown: number, extra: { fee?: number; stake?: number; mode?: string } = {}) {
    this.coins += change;
    this.rows.unshift({ ts: Date.now(), kind, delta: shown, fee: extra.fee ?? 0, stake: extra.stake ?? 0, mode: extra.mode ?? null, balance: this.coins });
    this.rows = this.rows.slice(0, 60);
    this.save();
  }
  adsToday() { const d = new Date(); d.setUTCHours(0, 0, 0, 0); return this.rows.filter(r => r.kind === 'ad' && r.ts >= d.getTime()).length; }
  setAdFree(until: number) { this.adFreeUntil = until; this.save(); }
  /** Skin shop: a bought skin is worn straight away, as online. */
  addSkin(id: string) { if (!this.skins.includes(id)) this.skins.push(id); this.equipSkin(id); }
  equipSkin(id: string) { const s = skinById(id); if (s) { this.equip[s.cat] = id; this.save(); } }
  take(stake: number) { if (this.coins < stake) return false; this.coins -= stake; this.save(); return true; }
  private save(day?: string) {
    try {
      const prev = JSON.parse(localStorage.getItem('dom.demoWallet') || '{}') as { day?: string };
      localStorage.setItem('dom.demoWallet', JSON.stringify({ coins: this.coins, rows: this.rows, adFreeUntil: this.adFreeUntil, skins: this.skins, equip: this.equip, day: day ?? prev.day ?? '' }));
    } catch { /* storage blocked: the wallet lasts for this visit */ }
  }
  user(name: string, ch: number): PublicUser {
    return { id: 'demo', name, ch, email: null, rating: 1000, games: 0, wins: 0, coins: this.coins, gifts: 0, ratings: {}, phone: null, google: null, adsLeft: Math.max(0, 10 - this.adsToday()), adFreeUntil: this.adFreeUntil, skins: this.skins.slice(), equip: { ...this.equip } };
  }
}
