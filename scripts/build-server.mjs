// Bundles the server (and the shared game logic) into dist/server.cjs. `ws` stays an npm dependency.
import { build } from 'esbuild';
import path from 'node:path';

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), '..');
await build({
  entryPoints: [path.join(root, 'server/index.ts')],
  outfile: path.join(root, 'dist/server.cjs'),
  bundle: true,
  platform: 'node',
  format: 'cjs',
  target: 'node22',
  external: ['ws'],
  sourcemap: true,
  logLevel: 'info',
});
