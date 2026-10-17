// Keeps the SQLite database safe on hosts whose disk is wiped on every restart (free plans such as Render's).
//
// The database stays a fast local file. Every few minutes (only when something changed) and when the server
// shuts down, a compressed snapshot is uploaded as an asset of a hidden release ("db-backup") in a private
// GitHub repository; on start, when there is no database file yet, the newest snapshot is downloaded first.
// Uses only fetch and zlib: no extra packages.
//
//   BACKUP_GITHUB_REPO=owner/repo   a PRIVATE repository (the game's own repo is fine)
//   BACKUP_GITHUB_TOKEN=…           a fine-grained token for that repo with "Contents: read and write"
//   BACKUP_MINUTES=3                how often to save when something changed
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const TAG = 'db-backup';
const PREFIX = 'kings-battle-db-';
const KEEP = 4;   // a few older snapshots stay as a safety net

interface Asset { id: number; name: string; created_at: string }
interface Release { id: number; assets: Asset[] }

export interface BackupOptions { api?: string; uploads?: string; tag?: string; prefix?: string; log?: (m: string) => void }

export class GitHubBackup {
  private api0: string; private up0: string; private tag: string; private prefix: string; private log: (m: string) => void;
  constructor(private repo: string, private token: string, o: BackupOptions = {}) {
    this.api0 = o.api ?? 'https://api.github.com'; this.up0 = o.uploads ?? 'https://uploads.github.com';
    this.tag = o.tag ?? TAG; this.prefix = o.prefix ?? PREFIX;
    this.log = o.log ?? (m => console.log(`[backup] ${m}`));
  }

  private headers(extra: Record<string, string> = {}) {
    return { Authorization: `Bearer ${this.token}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'kings-battle-server', ...extra };
  }
  private async api(path: string, init: RequestInit = {}) {
    const r = await fetch(`${this.api0}/repos/${this.repo}${path}`, { ...init, headers: this.headers(init.headers as Record<string, string>) });
    if (!r.ok && r.status !== 404) throw new Error(`GitHub ${init.method || 'GET'} ${path}: ${r.status} ${(await r.text()).slice(0, 200)}`);
    return r;
  }

  /** The backup release, created on first use. */
  private async release(): Promise<Release> {
    const r = await this.api(`/releases/tags/${this.tag}`);
    if (r.status !== 404) return await r.json() as Release;
    const c = await this.api('/releases', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tag_name: this.tag, name: 'Game server database backups', prerelease: true,
        body: 'Automatic snapshots of the game server database (players, coins, ratings). Do not delete: the server restores from the newest one when it restarts.' }),
    });
    return await c.json() as Release;
  }

  private snapshots(rel: Release) {
    return rel.assets.filter(a => a.name.startsWith(this.prefix)).sort((a, b) => b.created_at.localeCompare(a.created_at) || b.id - a.id);
  }

  /** Download the newest snapshot into `file`. Returns false when there is none yet. Throws if GitHub can't be reached. */
  async restore(file: string): Promise<boolean> {
    const latest = this.snapshots(await this.release())[0];
    if (!latest) { this.log('no snapshot yet: starting with a new database'); return false; }
    const r = await fetch(`${this.api0}/repos/${this.repo}/releases/assets/${latest.id}`, { headers: this.headers({ Accept: 'application/octet-stream' }) });
    if (!r.ok) throw new Error(`download ${latest.name}: ${r.status}`);
    const raw = Buffer.from(await r.arrayBuffer());
    const data = raw[0] === 0x1f && raw[1] === 0x8b ? zlib.gunzipSync(raw) : raw;
    const tmp = file + '.restore';
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(tmp, data); fs.renameSync(tmp, file);
    this.log(`restored ${latest.name} (${Math.round(data.length / 1024)} KB)`);
    return true;
  }

  /** Upload the database snapshot at `snapshotFile`, then prune old ones. */
  async save(snapshotFile: string) {
    const rel = await this.release();
    const body = zlib.gzipSync(fs.readFileSync(snapshotFile), { level: 9 });
    const name = `${this.prefix}${new Date().toISOString().replace(/[:.]/g, '-')}.sqlite.gz`;
    const up = await fetch(`${this.up0}/repos/${this.repo}/releases/${rel.id}/assets?name=${encodeURIComponent(name)}`, {
      method: 'POST', headers: this.headers({ 'Content-Type': 'application/gzip' }), body,
    });
    if (!up.ok) throw new Error(`upload ${name}: ${up.status} ${(await up.text()).slice(0, 200)}`);
    const fresh = await this.release();
    for (const old of this.snapshots(fresh).slice(KEEP)) await this.api(`/releases/assets/${old.id}`, { method: 'DELETE' });
    this.log(`saved ${name} (${Math.round(body.length / 1024)} KB)`);
  }
}
