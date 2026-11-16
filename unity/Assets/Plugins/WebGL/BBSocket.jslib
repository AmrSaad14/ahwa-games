// The web version's connection to the game server (Scripts/Net.cs): the browser's WebSocket, with what arrives
// kept in a queue that the game reads every frame. Also the page's own address, and leaving to sign in.
mergeInto(LibraryManager.library, {
  BBSock_Open: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var S = window.bbSock || (window.bbSock = { q: [], ws: null });
    if (S.ws) { try { S.ws.onclose = null; S.ws.close(); } catch (e) {} }
    var ws;
    try { ws = new WebSocket(url); } catch (e) { S.ws = null; S.q.push('\u0001closed:0'); return; }
    S.ws = ws;
    ws.onopen = function () { if (S.ws === ws) S.q.push('\u0001open'); };
    ws.onmessage = function (e) { if (S.ws === ws && typeof e.data === 'string') S.q.push(e.data); };
    ws.onclose = function (e) { if (S.ws === ws) { S.ws = null; S.q.push('\u0001closed:' + e.code); } };
    ws.onerror = function () {};
  },
  // 0 connecting, 1 open, 3 closed (or none yet)
  BBSock_State: function () {
    var S = window.bbSock;
    if (!S || !S.ws) return 3;
    var r = S.ws.readyState;
    return r === 0 ? 0 : r === 1 ? 1 : 3;
  },
  BBSock_Send: function (p) {
    var S = window.bbSock;
    if (S && S.ws && S.ws.readyState === 1) S.ws.send(UTF8ToString(p));
  },
  BBSock_Close: function () {
    var S = window.bbSock;
    if (S && S.ws) { var w = S.ws; S.ws = null; try { w.close(); } catch (e) {} }
  },
  BBSock_Next: function () {
    var S = window.bbSock;
    if (!S || !S.q.length) return 0;
    var s = S.q.shift();
    var n = lengthBytesUTF8(s) + 1, b = _malloc(n);
    stringToUTF8(s, b, n);
    return b;
  },
  // the page's folder, e.g. https://qimma-labs.com/boardbattles
  BBWeb_Base: function () {
    var s = location.origin + location.pathname.replace(/\/[^\/]*$/, '');
    var n = lengthBytesUTF8(s) + 1, b = _malloc(n);
    stringToUTF8(s, b, n);
    return b;
  },
  // open a page in this tab (signing in comes back to the game with ?ticket=…)
  BBWeb_Go: function (p) { location.href = UTF8ToString(p); },
  // load the page again (the update for the web version)
  BBWeb_Reload: function () { location.reload(); },
  // take ?ticket=… off the address once it is used
  BBWeb_CleanUrl: function () {
    try { history.replaceState(null, '', location.pathname + location.hash); } catch (e) {}
  }
});
