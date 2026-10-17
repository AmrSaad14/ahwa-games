// Google AdMob in the phone apps: a rewarded video that pays coins, and a full-screen ad after every
// finished game (never during play; none on the ad-free plan). The website shows no ads.
import { Capacitor } from '@capacitor/core';
import { AdMob, AdmobConsentStatus, RewardAdPluginEvents } from '@capacitor-community/admob';

declare const __ADS__: { android: { rewarded: string; interstitial: string }; ios: { rewarded: string; interstitial: string } };
// Google's public test units, used until real ones are set at build time (ADMOB_* variables)
const TEST = {
  android: { rewarded: 'ca-app-pub-3940256099942544/5224354917', interstitial: 'ca-app-pub-3940256099942544/1033173712' },
  ios: { rewarded: 'ca-app-pub-3940256099942544/1712485313', interstitial: 'ca-app-pub-3940256099942544/4411468910' },
};

const platform = () => (Capacitor.getPlatform?.() === 'ios' ? 'ios' : 'android');
const units = () => {
  const cfg = typeof __ADS__ === 'object' ? __ADS__ : null;
  const p = platform();
  return { rewarded: cfg?.[p]?.rewarded || TEST[p].rewarded, interstitial: cfg?.[p]?.interstitial || TEST[p].interstitial };
};
const isTest = (id: string) => id.startsWith('ca-app-pub-3940256099942544/');

let ready: Promise<boolean> | null = null;
let lastShown = 0;

export const adsSupported = () => Capacitor.isNativePlatform();

/** Starts AdMob once, asking for consent first where the law needs it (EU/UK). */
export function initAds(): Promise<boolean> {
  if (!adsSupported()) return Promise.resolve(false);
  return ready ??= (async () => {
    try {
      const consent = await AdMob.requestConsentInfo();
      if (consent.status === AdmobConsentStatus.REQUIRED && consent.isConsentFormAvailable) await AdMob.showConsentForm();
      if (platform() === 'ios') await AdMob.requestTrackingAuthorization().catch(() => {});
      await AdMob.initialize({ initializeForTesting: isTest(units().rewarded) });
      return true;
    } catch { return false; }
  })();
}

/**
 * Shows a rewarded video. Resolves true if the player watched it to the end (AdMob's "rewarded" event).
 * `userId` and `ticket` go to AdMob's server-side verification, which tells the game server to pay.
 */
export async function showRewarded(userId: string, ticket: string): Promise<boolean> {
  if (!(await initAds())) return false;
  const id = units().rewarded;
  let rewarded = false;
  const h = await AdMob.addListener(RewardAdPluginEvents.Rewarded, () => { rewarded = true; });
  try {
    await AdMob.prepareRewardVideoAd({ adId: id, isTesting: isTest(id), ssv: { userId, customData: ticket } });
    await AdMob.showRewardVideoAd();
    lastShown = Date.now();
    return rewarded;
  } catch { return rewarded; }
  finally { h.remove(); }
}

/**
 * After every finished game: a full-screen ad (video when AdMob has one; Google decides the length, usually
 * 15-30 s, and lets players skip some of them after 5 s). Never during play, and never for players on the
 * ad-free plan. Resolves when the ad is closed (or straight away if there is none).
 */
export async function afterMatch(adFree = false): Promise<void> {
  if (!adsSupported() || adFree) return;
  if (Date.now() - lastShown < 20_000) return;   // two games ending back to back: one ad is enough
  if (!(await initAds())) return;
  const id = units().interstitial;
  try {
    await AdMob.prepareInterstitial({ adId: id, isTesting: isTest(id) });
    await AdMob.showInterstitial();
    lastShown = Date.now();
  } catch { /* no ad available: carry on */ }
}
