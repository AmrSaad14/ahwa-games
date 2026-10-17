// The shop in the phone app: Google Play Billing through cordova-plugin-purchase (loaded by Capacitor as the
// global CdvPurchase). Every purchase is sent to the game server, which checks it with Google and credits it;
// only then is it finished (consumed / acknowledged) here, so nothing is lost if the app closes half-way.
import { Capacitor } from '@capacitor/core';
import { COIN_PACKS, NO_ADS } from '../../shared/gifts';

/* The few parts of the plugin's API used here. */
interface CdvTransaction { purchaseId?: string; transactionId?: string; products: { id: string }[]; state?: string; finish(): Promise<unknown> }
interface CdvOffer { order(): Promise<{ message?: string } | undefined> }
interface CdvProduct { id: string; owned?: boolean; pricing?: { price?: string }; getOffer(): CdvOffer | undefined }
interface CdvStore {
  applicationUsername?: string | (() => string);
  register(p: { id: string; type: string; platform: string }[]): void;
  when(): { approved(cb: (t: CdvTransaction) => void): ReturnType<CdvStore['when']>; productUpdated(cb: () => void): ReturnType<CdvStore['when']> };
  initialize(p: string[]): Promise<unknown>;
  ready(cb: () => void): void;
  get(id: string): CdvProduct | undefined;
  localReceipts: { transactions: CdvTransaction[] }[];
  restorePurchases?(): Promise<unknown>;
}
interface Cdv { store: CdvStore; ProductType: Record<string, string>; Platform: Record<string, string> }
const cdv = () => (globalThis as unknown as { CdvPurchase?: Cdv }).CdvPurchase;

export const billingSupported = () => Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android' && !!cdv();

/** Waiting for the server: purchase token → the transaction to finish once it is credited. */
const waiting = new Map<string, CdvTransaction>();
let started = false;
let onChange: () => void = () => {};
let sendToServer: (product: string, token: string) => void = () => {};

const tokenOf = (t: CdvTransaction) => t.purchaseId || t.transactionId || '';

/**
 * Starts the store once the player is known. `send` hands a purchase to the game server ({ t: 'buy' }),
 * `changed` re-draws the shop when prices arrive or a purchase completes.
 */
export function initBilling(uid: string, send: (product: string, token: string) => void, changed: () => void) {
  sendToServer = send; onChange = changed;
  const c = cdv();
  if (!billingSupported() || !c) return;
  // the account tag Google keeps with each purchase, so it can only be credited to this player
  c.store.applicationUsername = () => uid;
  if (started) return;
  started = true;
  const { store, ProductType, Platform } = c;
  store.register([
    ...COIN_PACKS.map(p => ({ id: p.id, type: ProductType.CONSUMABLE, platform: Platform.GOOGLE_PLAY })),
    { id: NO_ADS.id, type: ProductType.PAID_SUBSCRIPTION, platform: Platform.GOOGLE_PLAY },
  ]);
  store.when()
    .approved(t => {
      const token = tokenOf(t);
      if (!token) return;
      waiting.set(token, t);
      for (const p of t.products) sendToServer(p.id, token);
    })
    .productUpdated(() => onChange());
  store.initialize([Platform.GOOGLE_PLAY]).catch(() => {});
  // an ad-free plan bought earlier (or renewed since): let the server refresh its paid-until date
  store.ready(() => {
    for (const r of store.localReceipts || []) for (const t of r.transactions || []) {
      if (t.products?.some(p => p.id === NO_ADS.id) && tokenOf(t)) sendToServer(NO_ADS.id, tokenOf(t));
    }
    onChange();
  });
}

/** The server credited (or had already credited) this purchase: finish it in Google Play. */
export function purchaseDone(token: string) {
  const t = waiting.get(token);
  if (t) { waiting.delete(token); t.finish().catch(() => {}); }
}
/** The server refused it: leave it unfinished (Google refunds unfinished purchases after 3 days). */
export function purchaseRefused(token: string) { waiting.delete(token); }

/** The store's own localized price for a product (e.g. "EGP 200.00"), once it has loaded. */
export function storePrice(id: string): string | null { return cdv()?.store.get(id)?.pricing?.price ?? null; }

/** Opens Google Play's payment sheet. Resolves false if it couldn't start. */
export async function buy(id: string): Promise<boolean> {
  const offer = cdv()?.store.get(id)?.getOffer();
  if (!offer) return false;
  const err = await offer.order().catch(e => ({ message: String(e) }));
  return !err;
}
