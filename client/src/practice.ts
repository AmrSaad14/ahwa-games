import { Mode, defaultTarget, gameOf } from '../../shared/engine';
import type { Action, AnyView, BoardAction, Emote } from '../../shared/protocol';
import { Table } from '../../shared/table';
import { BoardTable } from '../../shared/boardtable';

/** Offline game against computer players, using the same table logic as the server. */
export class Practice {
  table: Table | BoardTable;
  static readonly ME = 'me';

  constructor(mode: Mode, name: string, ch: number, onView: (v: AnyView) => void, onEmote: (seat: number, k: Emote) => void, pace?: { botDelay: [number, number]; turnMs: number }) {
    const game = gameOf(mode);
    const opts = {
      id: 'practice-' + Date.now(), code: null, mode, target: defaultTarget(mode), private: true,
      turnMs: pace?.turnMs ?? 10_000, nextRoundMs: game === 'tawla' ? 4000 : 7000, botDelay: pace?.botDelay ?? [800, 1500] as [number, number], offlineGraceMs: 8000,
      lobbyAfterMatchMs: 10 * 60_000,
    };
    const hooks = {
      changed: (t: Table | BoardTable) => onView(t.view(Practice.ME)),
      emote: (_t: unknown, seat: number, k: Emote) => onEmote(seat, k),
      matchEnd: () => {},
      closed: () => {},
    };
    this.table = game === 'domino' ? new Table(opts, hooks) : new BoardTable(opts, hooks);
    this.table.join({ uid: Practice.ME, name, ch, bot: false, rating: 1000 });
    for (let i = 1; i < this.table.n; i++) this.table.addBot(Practice.ME, i);
  }
  /** Deals and starts; called once the caller holds a reference (the first view arrives synchronously). */
  begin() { this.table.start(Practice.ME); }

  act(a: Action | BoardAction) {
    return this.table instanceof Table ? this.table.act(Practice.ME, a as Action) : this.table.act(Practice.ME, a as BoardAction);
  }
  emote(k: Emote) { this.table.emote(Practice.ME, k); }
  restart() { const t = this.table; t.phase = 'lobby'; t.start(Practice.ME); }
  stop() { this.table.close(); }
}
