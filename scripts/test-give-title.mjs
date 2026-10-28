// CI only: gives every player on the local test server the "Café Legend" title (once each), the way the
// dashboard would, so the screenshots show titles. Runs for a few minutes.
const base = 'http://127.0.0.1:8080/api/admin', H = { 'x-admin-key': 'test', 'Content-Type': 'application/json' };
const done = new Set();
for (let i = 0; i < 120; i++) {
  try {
    const { rows } = await (await fetch(base + '/players', { headers: H })).json();
    for (const u of rows ?? []) {
      if (done.has(u.id)) continue;
      const r = await fetch(base + '/players/' + u.id, { method: 'POST', headers: H, body: JSON.stringify({ action: 'title', title: 'legend', notify: false }) });
      console.log(u.name, r.status); done.add(u.id);
    }
  } catch (e) { console.log('wait', e.message); }
  await new Promise(r => setTimeout(r, 4000));
}
