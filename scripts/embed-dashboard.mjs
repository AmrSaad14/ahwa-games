// Turns the dashboard page (server/dashboard/index.html) into server/dashboard.ts, so the server bundle carries it.
import fs from 'node:fs';
import path from 'node:path';
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), '..');
const html = fs.readFileSync(path.join(root, 'server/dashboard/index.html'), 'utf8');
const out = `// Made by scripts/embed-dashboard.mjs from server/dashboard/index.html. Edit that file, not this one.\nconst HTML = ${JSON.stringify(html)};\n/** The dashboard page served at /admin. */\nexport const dashboardPage = (): string => HTML;\n`;
const file = path.join(root, 'server/dashboard.ts');
if (!fs.existsSync(file) || fs.readFileSync(file, 'utf8') !== out) fs.writeFileSync(file, out);
