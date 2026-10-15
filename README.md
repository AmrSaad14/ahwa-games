# Ahwa Games

Four coffeehouse games, played online around a 3D table in an Egyptian ahwa: **Domino, Tawla (backgammon), Chess and Ludo**. In Arabic (right-to-left) and English.
(The working name stays until the branding step.)
One TypeScript codebase gives you:

- **Game server**: Node.js, WebSockets, built-in SQLite. It is authoritative: it shuffles, deals, rolls the dice, validates every move and only sends each player what they may see.
- **Web app**: works in any modern mobile or desktop browser and can be installed to the home screen. Practice mode works offline.
- **Android and iOS apps**: the same client packaged with Capacitor, with haptics, native sharing and the Android back button.

## What players get

| | |
|---|---|
| **Four games, Egyptian style** | Pick a game, then a mode. **Domino**: فردي (1 vs 1), 3 players, 4 players or زوجي (2 vs 2). **Tawla**: عادة, محبوسة, ٣١ and جلبهار. **Chess**: bullet 1+0, blitz 3+2, classic 5+2 or rapid 10+5. **Ludo**: classic (2–4), quick and master. Each game has its own 3D board, its own rating and leaderboard, and a how-to-play page with a short recording of the game being played. |
| **Quick match** | Pick a game and mode and tap Play. People searching at the same time are matched together. If nobody else shows up within 12 seconds, computer players fill the empty seats. |
| **«جلا» on a pass** | When a domino player passes, a «جلا!» speech bubble pops up over them. |
| **Landscape** | The games are played with the phone sideways. The Android and iOS apps are locked to landscape (either direction) and the installed web app asks for it; a portrait phone browser shows a «turn your phone» screen. Board games keep their controls in a side panel next to the board. |
| **Tap or drag** | Tap a piece and then where it goes, or drag it there: domino tiles from your hand onto a glowing end of the line, chess pieces onto a square, tawla checkers onto a point or off the board. Allowed places light up as you pick a piece up. |
| **Private rooms** | Create a room, share the 5-letter code (native share sheet on phones) and friends join with it. The host picks the game mode and target (domino 51, 101 or 151 points; tawla 3, 5 or 7) and can fill seats with computer players. |
| **Practice** | Play against the computer on the device itself, no connection needed. |
| **3D ahwa baladi** | A small marble table in an Egyptian street coffeehouse: patterned cement-tile floor, green-painted walls, mirror and pictures, a tea station, a sunlit doorway, neighbouring tables with shisha smoke and backgammon, and regulars sipping tea. You sit at the table yourself; your opponents are six original characters who lean in on their turn, reach for tiles, shake their head on a pass and jump when they win. |
| **Real dominoes** | Small ivory tiles with rounded edges, engraved pips and a brass centre pin. Opponents stand their tiles on edge facing themselves, and the boneyard lies face down at the corner. |
| **Correct tile layout** | Tiles join end to end on the matching number, doubles sit crosswise, and the line turns an L-shaped corner at the table edge and snakes back. The tests check this on hundreds of random games for overlaps and gaps. |
| **10-second turns** | In domino, tawla and ludo every player, online and in practice, has 10 seconds per move with a countdown. When time runs out the game makes the best move for them and tells them. Chess uses clocks instead: run out of time and you lose. A player who is at the table always gets their full 10 seconds; the computer only takes over a seat whose player disconnected or left. Reconnecting drops you straight back into your seat. |
| **Voice chat** | Tap 🎙 at the table to talk with the other players. Audio goes directly between phones (WebRTC), with a TURN relay for networks that block direct connections. Mute your microphone, mute a player, or leave voice at any time. Players with the 🎙 badge are in voice, and their name glows while they speak. |
| **Gifts and coins** | Tap a player's name to send a gift: tea, coffee, a rose, sweets, a crown, a trophy or a diamond. The gift flies across the table. Gifts cost coins, which players earn: 500 to start, 100 every day, 10 per match and 50 for a win. Coins are earned only and can't be bought yet. |
| **Safety** | Players can block someone, which mutes them and hides their reactions and gifts in every game, or report them for abuse, cheating, spam or an offensive name. You read reports on a private admin page. Google Play and the App Store expect these tools in apps with voice chat between strangers. |
| **Reactions** | Twelve quick reactions shown as speech bubbles over the characters. |
| **Accounts and ranking** | Everyone starts as a guest instantly. They can add an email and password to keep their profile on any device. Quick matches are rated (Elo, separately for each game; a draw counts half), and each game's leaderboard lists players with at least 3 games. |
| **Polish** | Synthesized sounds (no audio files), vibration, turn alert, round results that reveal every hand, rating change at the end, reduced-motion support. |

### Rules used

The rules follow how these games are played in Egyptian coffeehouses (sources: [Egyptian Streets on tawla](https://egyptianstreets.com/2022/09/10/sheesh-beesh-egypts-5-most-common-tawla-games-and-how-theyre-played/), [Domono's Egyptian domino rules](https://domono.net/blog/egyptian-dominoes-rules), [Jawaker's ludo rules](https://blog.jawaker.com/ludo-rules/), and the Fevga, Plakoto and Gul bara references for details the Egyptian sources leave out).

**Domino**

- Double-six set (28 tiles). فردي (1 vs 1): 7 each, the rest is the bazaar. 3 players: the 0|0 is taken out and all 27 are dealt, 9 each, no bazaar. 4 players and زوجي (2 vs 2): all 28 dealt, 7 each.
- The first round is opened by the heaviest double, which must be played first (in 2 vs 2 always the double six). Later rounds are opened by the previous winner with any tile.
- Can't play? Draw from the bazaar until a tile fits; with no bazaar, pass.
- Going out scores the pips left in the opponents' hands (never your partner's). A closed game (لخمة): the lowest pip count, per team in 2 vs 2, wins the others' pips; a tie scores nothing and the heaviest double opens again.
- Targets: 101 for 1 vs 1 and free-for-all, 151 for partners (51, 101 or 151 in rooms).

**Tawla** (all: 15 checkers each, doubles play four times, use both dice when you can or else the larger one, bear off once all 15 are home; a game is 1 point or 2 for a mars, sets to 3, 5 or 7)

- **عادة** (regular): backgammon start and moves; a lone checker that is hit goes to the bar and must re-enter first.
- **محبوسة**: all 15 start on the point farthest from home and the sides move toward each other. No hitting: landing on a lone opposing checker traps it underneath until you leave; two checkers or a trapping checker close a point.
- **٣١**: all 15 start in diagonally opposite corners and both sides go the same way round. One checker holds a point. The first checker must pass the opponent's starting point before a second leaves the start, and nobody may wall in the opponent with six points in a row. The winner scores one point per opposing checker still on the board; the set is to 31.
- **جلبهار**: same start and direction as ٣١, one checker holds a point. After each player's first three rolls, a double climbs (3-3: four 3s, four 4s, four 5s, four 6s); if the player gets stuck, the opponent plays the rest of the run.
- Left out on purpose: اليهودية (Yahudiyya), a small half-board game whose rules the sources don't describe fully enough to implement.

**Chess**: all FIDE move rules (castling, en passant, promotion). Checkmate or the opponent's flag falling wins; stalemate, threefold repetition, the 50-move rule and insufficient material are draws. The move generator is checked against the standard perft counts.

**Ludo**: a 6 brings a pawn out; a 6, a capture or reaching home gives another roll, and three 6s in a row lose the turn. Landing on an opponent sends it home, except on the star squares or onto a block of two. Pawns reach home only with the exact number.

- **Quick (سريع)**: each player starts with one pawn on the board; the first pawn home wins.
- **Master (ماستر)**: a player's pawns can't enter the home column until that player has captured; until then they keep going round.

## Project layout

```
shared/      game rules (domino; games/chess, tawla, ludo), computer players, table logic, message types (used by server AND client)
server/      HTTP + WebSocket server, matchmaking, accounts, ratings, SQLite storage
client/      the game app: index.html, src/ (three.js scene, UI, networking, sounds, i18n), public/ (icons, manifest)
tests/       rule and layout tests, full server tests with simulated players, a headless browser walk-through
scripts/     build scripts and a database backup script
resources/   app icon and splash source images for the store apps
```

## Run it locally

Requires Node.js 22.13 or newer.

```bash
npm install
npm run build          # bundles client/dist and dist/server.cjs
npm start              # http://localhost:8080
```

While developing, run these two in separate terminals and reload the browser after changes:

```bash
npm run dev:client     # rebuilds the client on every save
npm run dev:server     # restarts the server on every save
```

To try multiplayer on one computer, open the page in two browser windows (one of them private, so each gets its own guest).

### Tests

```bash
npm test               # rules, layout, computer player, full matches over real WebSockets
npm run typecheck
```

The how-to-play GIFs are recorded from the real game (practice against the computer, driven by real taps and drags, captioned in Arabic and English). Re-record them after changing the look of a game; it needs Playwright's Chromium and ffmpeg and takes a few minutes per game:

```bash
npm run build && npm run howto            # all games, both languages → client/public/howto/
```

There is also a browser walk-through that clicks through home, practice in all four games, a private room with two players, a quick match and the leaderboard on a phone-sized screen:

```bash
npm install --no-save playwright && npx playwright install chromium
CLIENT_STUBS=1 npm run build && node tests/ui-smoke.mjs ./screenshots
npm run build          # rebuild afterwards: CLIENT_STUBS swaps out three.js for the test
```

## Host it (recommended: any Linux server with Docker)

A small server is enough to start: 1 vCPU and 1 GB RAM handles hundreds of players at once.

1. Create a server (Hetzner, DigitalOcean, AWS Lightsail…) and install Docker.
2. Point a domain at it: an `A` record such as `domino.yourdomain.com` → the server's IP.
3. Copy this folder to the server, then:

   ```bash
   cp .env.example .env
   nano .env                      # set DOMAIN, TURN_SECRET (openssl rand -hex 32) and ADMIN_KEY
   docker compose up -d --build
   ```

   Open these ports in the server's firewall: **80** and **443** (TCP) for the game, **3478** (TCP and UDP) and **49160-49200** (UDP) for the voice relay.

4. Open `https://domino.yourdomain.com`. Caddy fetches the HTTPS certificate automatically on first start, which can take up to a minute.

Useful commands:

```bash
docker compose logs -f domino          # server logs
docker compose up -d --build           # deploy a new version
./scripts/backup.sh                    # copy the database to ./backups
curl https://domino.yourdomain.com/healthz   # {"ok":true,"connections":…,"tables":…,"queued":…}
```

All data (accounts, ratings, match history) is one SQLite file in the `domino-data` Docker volume. Back it up with `scripts/backup.sh`, for example from a daily cron job.

### Other hosts

The app is a single container listening on port 8080 that needs a persistent folder mounted at `/data`.

- **Fly.io**: `fly launch`, then `fly volumes create data` and mount it at `/data`.
- **Render or Railway**: deploy from the Dockerfile and attach a persistent disk at `/data`.

These platforms terminate HTTPS for you, so you don't need Caddy there. Set `TRUST_PROXY=1` so rate limits see real client IPs.

### Configuration

| Variable | Default | Meaning |
|---|---|---|
| `PORT` | `8080` | HTTP/WebSocket port |
| `DB_PATH` | `/data/domino.db` (Docker) | SQLite database file |
| `SECRET` | generated | Signs session tokens. If empty, one is generated and stored in the database |
| `TURN_SECONDS` | `10` | Time a player has for each move; when it runs out a move is made for them |
| `BOT_FILL_SECONDS` | `12` | How long quick match waits before computer players fill seats |
| `CHESS_MINUTES` | (per mode) | Forces one chess clock length for every chess mode. Normally each mode has its own: 1+0, 3+2, 5+2, 10+5 |
| `NEXT_ROUND_SECONDS` | `7` | Pause between rounds |
| `TRUST_PROXY` | off | Set to `1` behind Caddy, nginx or a platform proxy |
| `MAX_CONNECTIONS_PER_IP` | `20` | Connection cap per IP address |
| `TURN_URLS`, `TURN_SECRET` | set by Docker Compose | Voice relay address and shared secret. Without them voice still works on most Wi-Fi networks but often fails on mobile data |
| `STUN_URLS` | Google public STUN | Servers phones use to find a direct audio route |
| `ADMIN_KEY` | empty | Key for reading player reports at `/api/admin/reports?key=…` |

### Scaling later

Live games are held in the server's memory, and accounts are in SQLite. That is the simplest setup to run and comfortably serves one busy server. Note that **restarting the server ends games in progress**; accounts and ratings are kept. Deploy updates when it is quiet, or add a "maintenance soon" notice first.

If you outgrow one machine, move storage to PostgreSQL (only `server/db.ts` touches the database) and route each room to one server instance with sticky sessions.

## Build the Android and iOS apps

The store apps load the game from the device and talk to your server, so build them **after** your server has a working HTTPS domain.

```bash
# 1. Bundle the client pointing at your server
SERVER_URL=https://domino.yourdomain.com npm run build:client

# 2. First time only: create the native projects and app icons/splash screens
npx cap add android
npx cap add ios                 # macOS with Xcode only
npm run cap:assets              # generates icons and splash from resources/
npm run cap:permissions         # adds the microphone permission voice chat needs (Android and iOS)

# 3. Copy the web build in and open the IDE
npx cap sync
npx cap open android            # Android Studio → Build → Generate Signed Bundle (AAB) for Google Play
npx cap open ios                # Xcode → Product → Archive for the App Store
```

Repeat steps 1 and 3 whenever the client changes. App id and name are in `capacitor.config.json` (`com.ahwa.domino`, "Ahwa Domino"); change the id to a domain you own before publishing.

Store checklist:

- **Google Play**: developer account (one-time fee), privacy policy URL, content rating questionnaire, signed AAB.
- **App Store**: Apple Developer Program membership, a Mac with Xcode, privacy "nutrition label", screenshots for each required size. In-app account deletion, which Apple requires, is already in Settings.
- Both stores need a privacy policy. The app stores a nickname, an optional email and a hashed password, game results, coins, gifts and reports. Voice audio goes directly between players and is never recorded or stored.
- Declare the microphone use in both stores (Google Play: data safety form; App Store: privacy label). Both stores will ask about user-generated content and communication between players: answer that voice chat exists, with block and report tools.
- Coins can't be bought. If you later sell coins, use Google Play Billing and Apple In-App Purchase; both stores require their own payment systems for digital items.

## How online play works

1. The app opens a WebSocket to `/ws` and sends its saved session token. A new device gets a guest account and a token.
2. Quick match puts the player in a queue per mode. A table forms when enough people are waiting, or computer players fill in after `BOT_FILL_SECONDS`.
3. During a game the client sends only intents (`play tile 17 on the left end`, `draw`, `pass`). The server checks them against the rules and sends every player a personal view: their own hand, everyone's tile counts, the line, scores and timers.
4. If a connection drops, the client reconnects with backoff and the server puts the player back in their seat.

Security notes: passwords are hashed with scrypt; tokens are HMAC-signed; messages are size-limited and rate-limited; login and registration attempts are rate-limited per IP; names are cleaned before they are shown to others.
