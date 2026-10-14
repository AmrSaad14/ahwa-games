// Test-only stand-ins for the Capacitor plugins (the real packages give native apps haptics, sharing and the back button).
export const Capacitor = { isNativePlatform: () => false };
export const App = { addListener: () => Promise.resolve({ remove() {} }), minimizeApp: () => Promise.resolve() };
export const Share = { share: async (_o: unknown) => { throw new Error('not available'); } };
export const ImpactStyle = { Light: 'LIGHT', Medium: 'MEDIUM', Heavy: 'HEAVY' } as const;
export type ImpactStyle = typeof ImpactStyle[keyof typeof ImpactStyle];
export const Haptics = { impact: (_o: unknown) => Promise.reject(new Error('n/a')) };
