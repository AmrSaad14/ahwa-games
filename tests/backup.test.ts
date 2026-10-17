// The database backup to a GitHub release, against a small stand-in for GitHub's release API.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { GitHubBackup } from '../server/backup';
import { Store } from '../server/db';

function fakeGitHub() {
  const rel = { id: 7, assets: [] as { id: number; name: string; created_at: string }[] };
  let made = false, next = 100; const blobs = new Map<number, Buffer>();
  const server = http.createServer((req, res) => {
    const u = new URL(req.url || '/', 'http://x'), p = u.pathname;
    const json = (code: number, o: unknown) => { res.writeHead(code, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(o)); };
    if (p.startsWith('/download/')) { res.writeHead(200); return res.end(blobs.get(Number(p.split('/')[2]))); }
    if (req.headers.authorization !== 'Bearer t0ken') return json(401, {});
    if (req.method === 'GET' && p === '/repos/o/r/releases/tags/db-backup') return made ? json(200, rel) : json(404, {});
    if (req.method === 'POST' && p === '/repos/o/r/releases') { made = true; return json(201, rel); }
    if (req.method === 'POST' && p === '/repos/o/r/releases/7/assets') {
      const c: Buffer[] = []; req.on('data', d => c.push(d)); req.on('end', () => {
        const id = next++; blobs.set(id, Buffer.concat(c));
        rel.assets.push({ id, name: String(u.searchParams.get('name')), created_at: new Date(1e12 + id * 1000).toISOString() }); json(201, {});
      }); return;
    }
    const m = p.match(/^\/repos\/o\/r\/releases\/assets\/(\d+)$/);
    if (m && req.method === 'GET') { res.writeHead(302, { Location: `http://127.0.0.1:${(server.address() as { port: number }).port}/download/${m[1]}` }); return res.end(); }
    if (m && req.method === 'DELETE') { rel.assets = rel.assets.filter(a => a.id !== Number(m[1])); res.writeHead(204); return res.end(); }
    json(404, {});
  });
  return { server, rel };
}

test('backup: snapshots go up, old ones are pruned, and a fresh disk gets the newest back', async () => {
  const { server, rel } = fakeGitHub();
  await new Promise<void>(r => server.listen(0, '127.0.0.1', () => r()));
  const base = `http://127.0.0.1:${(server.address() as { port: number }).port}`;
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'kb-backup-'));
  const b = new GitHubBackup('o/r', 't0ken', { api: base, uploads: base, log: () => {} });
  try {
    assert.equal(await b.restore(path.join(dir, 'none.db')), false, 'nothing to restore at first');
    const store = new Store(path.join(dir, 'a.db'));
    const u = store.createGuest('Amr', 0);
    for (let i = 0; i < 6; i++) { store.addCoins(u.id, 1000 * (i + 1)); store.snapshot(path.join(dir, 'snap.db')); await b.save(path.join(dir, 'snap.db')); }
    const coins = store.coinsOf(u.id); store.close();
    assert.equal(rel.assets.length, 4, 'only the newest four snapshots are kept');
    const fresh = path.join(dir, 'restored', 'b.db');
    assert.equal(await b.restore(fresh), true);
    const back = new Store(fresh);
    assert.equal(back.coinsOf(u.id), coins, 'the player comes back with the same coins');
    back.close();
  } finally { server.close(); fs.rmSync(dir, { recursive: true, force: true }); }
});
