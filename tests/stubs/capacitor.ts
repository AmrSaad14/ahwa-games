// Test-only stand-ins for the Capacitor plugins (the real packages give native apps haptics, sharing and the back button).
export const Capacitor = { isNativePlatform: () => false, getPlatform: () => 'web' };
export const App = { addListener: () => Promise.resolve({ remove() {} }), minimizeApp: () => Promise.resolve() };
export const Share = { share: async (_o: unknown) => { throw new Error('not available'); } };
export const ImpactStyle = { Light: 'LIGHT', Medium: 'MEDIUM', Heavy: 'HEAVY' } as const;
export type ImpactStyle = typeof ImpactStyle[keyof typeof ImpactStyle];
export const Haptics = { impact: (_o: unknown) => Promise.reject(new Error('n/a')) };
export const Browser = { open: async (_o: { url: string }) => { location.href = _o.url; }, close: async () => {} };
export const AdmobConsentStatus = { NOT_REQUIRED: 'NOT_REQUIRED', OBTAINED: 'OBTAINED', REQUIRED: 'REQUIRED', UNKNOWN: 'UNKNOWN' } as const;
export const RewardAdPluginEvents = { Rewarded: 'onRewardedVideoAdReward' } as const;
const noAds = (..._a: unknown[]): Promise<any> => Promise.reject(new Error('no ads on the web'));
export const AdMob = { initialize: noAds, requestConsentInfo: noAds, showConsentForm: noAds, requestTrackingAuthorization: noAds, prepareRewardVideoAd: noAds, showRewardVideoAd: noAds,
  prepareInterstitial: noAds, showInterstitial: noAds, addListener: async () => ({ remove() {} }) };
