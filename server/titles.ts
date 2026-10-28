// The titles catalog: the designs the game ships with (shared/titles.ts) plus whatever the team changed in the
// dashboard (kept in the database, meta 'titles'). Players' titles live in the database (user_titles).
import { DEFAULT_TITLES, cleanTitle, type TitleDef } from '../shared/titles';

interface MetaStore { metaGet(k: string): string | null; metaSet(k: string, v: string): void }

export class Titles {
  list: TitleDef[];
  private listeners: (() => void)[] = [];
  constructor(private store: MetaStore) {
    let saved: unknown = null;
    try { saved = JSON.parse(store.metaGet('titles') || 'null'); } catch { /* none */ }
    this.list = Array.isArray(saved) ? saved.map(x => cleanTitle(x)).filter((x): x is TitleDef => !!x) : structuredClone(DEFAULT_TITLES);
    // monthly titles always exist (the game gives them by itself)
    for (const d of DEFAULT_TITLES) if (d.monthly && !this.list.some(t => t.id === d.id)) this.list.unshift(structuredClone(d));
  }
  onChange(f: () => void) { this.listeners.push(f); }
  get(id: string | null | undefined) { return id ? this.list.find(t => t.id === id) : undefined; }
  /** The title for a game's player of the month. */
  monthlyFor(game: string) { return this.list.find(t => t.monthly === game); }
  /** Adds or changes a title; returns it, or null if it isn't valid. */
  put(input: unknown): TitleDef | null {
    const id = String((input as { id?: unknown })?.id ?? '');
    const was = this.get(id);
    const t = cleanTitle(input, was);
    if (!t) return null;
    if (was?.monthly) t.monthly = was.monthly;   // a monthly title stays monthly
    const i = this.list.findIndex(x => x.id === t.id);
    if (i >= 0) this.list[i] = t; else this.list.push(t);
    this.save();
    return t;
  }
  /** Takes a title out (not the monthly ones). */
  remove(id: string): boolean {
    const t = this.get(id);
    if (!t || t.monthly) return false;
    this.list = this.list.filter(x => x.id !== id);
    this.save();
    return true;
  }
  private save() {
    this.store.metaSet('titles', JSON.stringify(this.list));
    for (const f of this.listeners) { try { f(); } catch (e) { console.error('[titles]', (e as Error).message); } }
  }
}
