// Google Play Games sign-in (Android app only). A small native plugin in the Android project (added by
// scripts/native-permissions.mjs) signs the player in with Play Games and asks it for a one-time code for the
// game server, which trades it with Google for the player's Play Games id.
import { Capacitor, registerPlugin } from '@capacitor/core';

interface PlayGamesPlugin {
  /** Whether Play Games is set up in this build (needs the game's Play Games app id). */
  available(): Promise<{ available: boolean }>;
  /** Signs in (silently first; `interactive` shows Google's sign-in if needed) and returns a server code. */
  signIn(o: { clientId: string; interactive: boolean }): Promise<{ code: string; name?: string }>;
}
const PlayGames = registerPlugin<PlayGamesPlugin>('PlayGames');

export const playGamesPossible = () => Capacitor.isNativePlatform() && Capacitor.getPlatform() === 'android';

/** Resolves the one-time server code, or null if Play Games is not available or the player said no. */
export async function playGamesCode(clientId: string, interactive: boolean): Promise<string | null> {
  if (!playGamesPossible() || !clientId) return null;
  try {
    const { available } = await PlayGames.available();
    if (!available) return null;
    const r = await PlayGames.signIn({ clientId, interactive });
    return r.code || null;
  } catch { return null; }
}
