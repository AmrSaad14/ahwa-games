# Board Battles

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
| **3D ahwa baladi** | A small marble table in an Egyptian street coffeehouse: patterned cement-tile floor, green-painted walls, mirror and pictures, a tea station, a sunlit doorway, neighbouring tables with shisha smoke and backgammon, and regulars sipping tea. You sit at the table yourself; your opponents are six Egyptian regulars, sculpted in code with real faces, hands and clothes: عم فتحي in a tarboosh and suit, كابتن سامي, زيكو, منى in a hijab, ندى and الحاج محمود in a galabeya and emma with prayer beads. They lean in on their turn, reach for tiles, shake their head on a pass and cheer when they win, and their faces are used as the player pictures in lists. |
| **Real dominoes** | Small ivory tiles with rounded edges, engraved pips and a brass centre pin. Opponents stand their tiles on edge facing themselves, and the boneyard lies face down at the corner. |
| **Correct tile layout** | Tiles join end to end on the matching number, doubles sit crosswise, and the line turns an L-shaped corner at the table edge and snakes back. The tests check this on hundreds of random games for overlaps and gaps. |
| **10-second turns** | In domino, tawla and ludo every player, online and in practice, has 10 seconds per move with a countdown. When time runs out the game makes the best move for them and tells them. Chess uses clocks instead: run out of time and you lose. A player who is at the table always gets their full 10 seconds; the computer only takes over a seat whose player disconnected or left. Reconnecting drops you straight back into your seat. |
| **Voice chat** | Tap 🎙 at the table to talk with the other players. Audio goes directly between phones (WebRTC), with a TURN relay for networks that block direct connections. Mute your microphone, mute a player, or leave voice at any time. Players with the 🎙 badge are in voice, and their name glows while they speak. |
| **Gifts and coins** | Tap a player's name to send a gift: tea, coffee, a rose, sweets, a crown, a trophy or a diamond. The gift flies across the table. Gifts cost coins, which players earn: 500 to start, 100 every day, 10 per match and 50 for a win. Coins are earned only and can't be bought yet. |
| **Safety** | Players can block someone, which mutes them and hides their reactions and gifts in every game, or report them for abuse, cheating, spam or an offensive name. You read reports on a private admin page. Google Play and the App Store expect these tools in apps with voice chat between strangers. |
| **Reactions** | Twelve quick reactions shown as speech bubbles over the characters. |
| **Coins and stakes** | Every online game is played for coins. Players pick a stake (5K, 20K, 50K, 100K, 250K, 500K or 1M); everyone puts it in when the match starts, the winners take the losers' stakes and the game keeps 10% of what the winners take (1 vs 1 at 20K: +18K or −20K). Walking out of a match loses the stake; a server restart gives stakes back. New players start with 100K and everyone gets 10K free a day. The Coins page shows the balance and every win, loss, bonus and gift. |
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
- **محبوسة**: all 15 start on the point farthest from home and the sides move toward each other. No hitting: landing on a lone opposing checker traps it underneath until you leave; two checkers or a trapping checker close a point. Trapping the opponent's last checker on their starting point (the mother, الأم) wins a double game.
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

## Shop: coin packs, the season pass and the ad-free plan (Google Play Billing)

| Product ID (Play Console) | Type | Price | Gives |
|---|---|---|---|
| `coins_50k` | In-app product (consumable) | 200 EGP | 50,000 coins |
| `coins_100k` | In-app product (consumable) | 379 EGP | 100,000 coins |
| `coins_500k` | In-app product (consumable) | 599 EGP | 500,000 coins |
| `coins_1m` | In-app product (consumable) | 899 EGP | 1,000,000 coins |
| `season_pass` | In-app product (consumable) | 299 EGP | This month's premium season track (also 750,000 coins in the game) |
| `no_ads_monthly` | Subscription, monthly | 199 EGP | No full-screen ads after games |

The Unity app buys through Google Play Billing 7 (`unity/Assets/Plugins/Android/BBBilling.java`, `Scripts/Billing.cs`)
and sends each purchase to the server, which checks it with the Google Play Developer API before crediting it,
once (`server/billing.ts`); then the app consumes it (or acknowledges the subscription). A purchase that was paid
but not credited (the app closed mid-way) is sent again the next time the app connects.
To switch it on after the app is on Play:
1. Create the six products above in Play Console → Monetize, with those IDs and prices (the season pass is a
   one-time product: a player can buy it again next season).
2. In Google Cloud, create a service account and a JSON key; in Play Console → Users and permissions, invite its
   email with "View financial data" and "Manage orders and subscriptions".
3. Put the JSON key in the server's `GOOGLE_PLAY_SERVICE_ACCOUNT` setting (Render → Environment).
Until then buying says the product isn't on Google Play yet. Coins can't be cashed out.

A full-screen ad (video when AdMob has one; AdMob decides the length, usually up to 30 seconds) shows after
every finished game, never during play, and never on the ad-free plan.

## Push reminders (Firebase Cloud Messaging)

Twice a day (14:00 and 20:00 Cairo time by default) every phone whose player hasn't played in the last two hours gets
a "come and play" notification, in the player's language.

1. In the [Firebase console](https://console.firebase.google.com) create a project and add an **Android app** with the
   package name `com.ahwa.boardbattles`. Download its `google-services.json`.
2. GitHub → this repo → Settings → Secrets and variables → Actions → New secret `GOOGLE_SERVICES_JSON`: paste the
   whole file. The next Unity build has push turned on (without it the app builds with push off).
3. Firebase console → Project settings → Service accounts → **Generate new private key**. On the server (Render →
   Environment) add `FIREBASE_SERVICE_ACCOUNT` with the whole JSON file as its value.
4. Optional: `PUSH_TIMES` (default `14:00,20:00`), `PUSH_TZ` (default `Africa/Cairo`).
5. Free hosts sleep when nobody plays, so their clock stops: at [cron-job.org](https://cron-job.org) (free) add a job
   that opens `https://YOUR-SERVER/api/cron/push?key=ADMIN_KEY` every 15 minutes. Each reminder still goes out once.
   Add `&now=1` once to send a reminder right away (a test).

## Ads (Google AdMob)

The Unity app shows an **interstitial** when a player taps "Play again" after a game, and a **rewarded video** for
the "+5K" coins button (players on the ad-free plan see no interstitials). Until you set your own units it shows
the Board Battles app's own AdMob units (app `ca-app-pub-3492998296780013~6952389253`, interstitial
`…/2334487395`, rewarded `…/1021405722`; these ids are not secret, they ship inside every app). To use other units,
set the GitHub secrets `ADMOB_APP_ID`, `ADMOB_INTERSTITIAL_ID` and `ADMOB_REWARDED_ID`. Ads serve fully once the app
is published on Google Play and AdMob has reviewed it; never tap your own ads (add your phone as a test device in
AdMob → Settings → Test devices).
3. Optional, recommended: in the rewarded unit turn on **server-side verification** with the callback
   `https://YOUR-SERVER/ads/ssv`, then set `ADS_SSV=1` on the server, so coins are only paid for ads Google confirms.

## Contact support

Settings → Contact support sends a message to the server, which keeps it (read them at
`https://YOUR-SERVER/admin/support?key=ADMIN_KEY`) and e-mails it to `SUPPORT_EMAIL` (default amrsaadamer14@gmail.com)
when SMTP is set up. The web app or anything else can send one with `POST /api/support` and a JSON body
`{"text": "...", "contact": "...", "name": "..."}`.

E-mail through Gmail: turn on 2-step verification for the sending Google account, make an **app password**
(myaccount.google.com → Security → App passwords), then set on the server `SMTP_HOST=smtp.gmail.com`,
`SMTP_PORT=465`, `SMTP_USER=that address`, `SMTP_PASS=the app password`.

## Cash out to Vodafone Cash (switched off)

Not offered for now: the app has no cash-out button and the server refuses requests unless `CASHOUT_ENABLED=1`.

Players can turn every **1,000,000 coins into 10 EGP**, paid to a Vodafone Cash number (010…) within one working day.
The coins are taken when the request is made; one request at a time, up to 10 million coins each, signed-in players
only (Google, mobile number or e-mail).

Pay them by hand from the admin page: `https://YOUR-SERVER/admin/cashouts?key=ADMIN_KEY` (set `ADMIN_KEY` on the
server to a long random password). Send the money from your Vodafone Cash wallet, then press **تم الدفع** (you can add
the transaction number); **رفض** turns a request down and gives the coins back. The player gets a notification
either way.

## Free hosting for testing (Render)

Until you rent a server, the game runs on Render's free plan, with no card needed. `render.yaml` describes everything:

1. Sign in at render.com with GitHub and allow it to see this repository.
2. Create a fine-grained GitHub token (GitHub → Settings → Developer settings → Fine-grained tokens) for **this repository only**,
   with **Contents: Read and write**. The server uses it to back up the database.
3. Render → **New → Blueprint** → pick this repository. Fill in `BACKUP_GITHUB_TOKEN` (the token) and
   `GOOGLE_CLIENT_SECRET` (from `ahwa-signin.env`), then **Apply**.
4. The server is at `https://kings-battle.onrender.com` (if Render gives a different address, set the repository
   variable `SERVER_URL` to it so the APK connects there).
5. For Google sign-in, add `https://<that address>/auth/google/callback` to the OAuth client's redirect URIs in Google Cloud.

What to expect on the free plan:
- It sleeps after 15 minutes with no players; the first player after that waits about a minute while it wakes.
- Its disk is wiped on every restart, so the database is saved to a hidden `db-backup` release in this repository
  every 3 minutes (when something changed) and when it shuts down, and restored when it starts (`server/backup.ts`).
  At worst the last few minutes of play are lost if it crashes.
- Ads: the app shows Google's test ads until it is published on Google Play. AdMob only serves real ads to apps
  that are live in a store and approved.

## Moving to a paid server on Render (what changes, step by step)

Everything already runs on the same server; paying only keeps it awake and gives it a real disk. On the paid plan:
the app connects instantly (no minute-long "Connecting…"), push reminders go out at 14:00 and 20:00 Cairo time by
themselves (no cron job needed), the weekly tournament starts every Thursday at 9 pm Cairo time by itself, the weekly
leaderboard prizes are paid every Monday, and the database lives on a disk instead of being restored from GitHub.

1. Render → your service **kings-battle** → **Settings → Instance type** → **Starter** (or higher). Save.
2. **Disks → Add disk**: name `data`, mount path `/data`, size 1 GB. Render restarts the service once.
3. Nothing to change for the database: the server already keeps it at `/data/domino.db` (set in the Dockerfile). On the
   first start with the new, empty disk it restores the latest backup from GitHub, so no player loses anything. After
   that start, check the leaderboard; then you may delete `BACKUP_GITHUB_TOKEN` (the GitHub backup is only needed on the
   free plan; keeping it as an extra copy does no harm).
4. In `render.yaml`, change `plan: free` to `plan: starter` so the Blueprint matches (otherwise a Blueprint sync would put it back).
5. Check: `https://YOUR-SERVER/api/v1/health` answers `{"ok":true,…}` and `https://YOUR-SERVER/api/docs` shows the API.

Settings worth knowing (Render → Environment):

| Variable | What it does |
|---|---|
| `ADMIN_KEY` | Password for the admin pages: `/admin/support`, `/admin/tourney`, `/api/admin/reports` |
| `FIREBASE_SERVICE_ACCOUNT` | Push reminders and notifications (tournament, referrals, prizes) |
| `SMTP_HOST`, `SMTP_USER`, `SMTP_PASS` | Support messages by e-mail |
| `PUSH_TIMES` | Reminder times, default `14:00,20:00` (Cairo) |
| `TOURNEY_WEEKDAY`, `TOURNEY_HOUR` | Tournament day (0 Sunday … 4 Thursday) and hour, Cairo time; default Thursday 21 |
| `BOT_FILL_SECONDS` | How long matchmaking waits for people before computer players join (default 8) |

Test the tournament without waiting for Thursday: `https://YOUR-SERVER/admin/tourney?key=ADMIN_KEY&start=1` starts this
week's tournament now with whoever signed up.

## Sign in with Apple

Players can sign in with Google or with Apple (the Apple button shows once the server has these settings). Apple's
sign-in runs in the browser like Google's, so it works in the Android app too. You need an Apple Developer account
(developer.apple.com, $99 a year):

1. **Certificates, Identifiers & Profiles → Identifiers → +** → **App IDs** → an App ID (e.g. `com.ahwa.boardbattles`)
   with **Sign in with Apple** ticked.
2. **Identifiers → + → Services IDs** → e.g. `com.ahwa.boardbattles.signin`; tick **Sign in with Apple** → **Configure**:
   primary App ID the one above; **Domains**: your server's domain (e.g. `kings-battle.onrender.com`);
   **Return URLs**: `https://YOUR-SERVER/auth/apple/callback`.
3. **Keys → +** → tick **Sign in with Apple** (configure it with the App ID) → download the `.p8` file (only once).
4. On the server (Render → Environment): `APPLE_SERVICE_ID` (the Services ID), `APPLE_TEAM_ID` (top right of the
   developer site), `APPLE_KEY_ID` (the key's id) and `APPLE_PRIVATE_KEY` (the whole `.p8` file, pasted as it is).
   Redeploy; the app shows **Continue with Apple** next to Google.

## The HTTP API

Everything outside a live game is also available over HTTP at `/api/v1`, documented at **`/api/docs`** (and as an
OpenAPI file at `/api/v1/openapi.json` for Postman or Swagger): accounts (guest, e-mail sign-in), your profile and coin
history, **player cards** with stats (matches, wins, losses, win rate, coins won and lost, net gains, biggest win,
per-game ratings), friends (add, accept, decline, remove), the daily reward, missions, the season pass, the weekly
tournament, the shop, leaderboards, referral codes, reports and support. Requests carry the player's token as
`Authorization: Bearer <token>` (the same token the app uses). Live games run over the WebSocket at `/ws`; its
messages are listed on the same page and typed in `shared/protocol.ts`.

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
| `PUBLIC_URL` | `https://DOMAIN` (Docker) | The address players reach the server on; Google sends players back to `PUBLIC_URL/auth/google/callback` |
| `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` | empty | Google sign-in (see below). The Google button only shows when both are set |
| `SMS_PROVIDER` | empty | `twilio`, `http` or `console`. The mobile-number sign-in only shows when this is set up |
| `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_VERIFY_SID` | empty | For `SMS_PROVIDER=twilio` |
| `SMS_HTTP_URL`, `SMS_HTTP_TOKEN` | empty | For `SMS_PROVIDER=http`: your own SMS gateway |
| `ADS_SSV` | off | `1`: rewarded-ad coins are only paid when AdMob confirms the view to `/ads/ssv` (see Ads) |
| `DEFAULT_COUNTRY_CODE` | `20` | Calling code for numbers typed without one (`01012345678` → `+201012345678`) |

### Sign-in with Google and a mobile number

Players start as guests. From the first-launch card or **Settings → Account** they can sign in with Google or with their mobile number (a 6-digit code by SMS). Signing in keeps the guest's coins, ratings and name, and lets them open the same account on another phone. Email and password still work too.

**Google** (free). In the [Google Cloud console](https://console.cloud.google.com/):

1. Create a project (e.g. "Ahwa Games").
2. **APIs & Services → OAuth consent screen**: user type *External*; app name, support email, the app logo; add your domain under *Authorized domains*; then **Publish app** so anyone can sign in (the `openid`, `email` and `profile` scopes need no Google review).
3. **APIs & Services → Credentials → Create credentials → OAuth client ID**: type **Web application**. Add the redirect URI `https://YOUR-DOMAIN/auth/google/callback`.
4. Put the client ID and secret in `.env` as `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET`, then `docker compose up -d`.

The same web client serves the website and both phone apps: the app opens Google in the phone's browser, Google returns to your server, and the server opens the app again through the `ahwagames://` link (added by `scripts/native-permissions.mjs`). No Android key fingerprints or iOS client IDs are needed, so it keeps working whichever key signs the APK.

**Mobile number** (each SMS costs money; pick one):

- **Twilio Verify** (`SMS_PROVIDER=twilio`): create a Twilio account, buy nothing extra, and under *Verify → Services* create a service; set `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN` and the service's `TWILIO_VERIFY_SID`. Twilio makes and checks the codes and blocks SMS fraud. Turn on the countries you need under *Verify → Settings → Geo permissions* (Egypt is off by default).
- **Your own SMS gateway** (`SMS_PROVIDER=http`), e.g. an Egyptian bulk-SMS company with a sender name: the server POSTs `{"to": "+2010…", "text": "123456 is your Ahwa Games code…", "code": "123456"}` as JSON to `SMS_HTTP_URL` (with `Authorization: Bearer SMS_HTTP_TOKEN`). Point it at the gateway directly if it accepts that, or at a small adapter.
- **Testing** (`SMS_PROVIDER=console`): no SMS is sent; codes are printed in the server log (`docker compose logs domino`).

Limits against abuse: one code a minute and five an hour per number, ten an hour per IP, five tries per code, codes expire after 10 minutes.

**App Store note:** Apple requires apps that offer Google sign-in to also offer an equally private option, in practice Sign in with Apple (App Review Guideline 4.8). Add it before submitting the iOS app; Android has no such rule.

### Ads (AdMob)

The phone apps show Google AdMob ads; the website shows none.

- **Rewarded video:** the 🎬 +5K button (top bar and Coins page) plays a video; watching to the end adds **5,000 coins**, up to **10 a day** per player. It also appears when a player is short of coins for a stake.
- **Between matches:** a full-screen ad after every third finished match, at most once every four minutes, only when leaving the result card. Never during play.
- In the EU/UK the app first shows Google's consent form (set up a GDPR message under *AdMob → Privacy & messaging*); on iPhone it asks the iOS tracking question.

Until you add your own IDs the apps show **Google's test ads**, which pay nothing. To earn:

1. Create an [AdMob](https://admob.google.com) account and add the app (Android, and iOS later). Create two ad units per platform: **Rewarded** (reward: 5000 coins) and **Interstitial**.
2. GitHub → *Settings → Secrets and variables → Actions → Variables*: set `ADMOB_APP_ID_ANDROID` (`ca-app-pub-…~…`), `ADMOB_ANDROID_REWARDED` and `ADMOB_ANDROID_INTERSTITIAL` (`ca-app-pub-…/…`). The next APK build uses them. For iOS builds set `ADMOB_APP_ID_IOS`, `ADMOB_IOS_REWARDED` and `ADMOB_IOS_INTERSTITIAL` when building on a Mac.
3. **Make the reward cheat-proof:** in AdMob open the Rewarded ad unit → *Server-side verification* → callback URL `https://YOUR-DOMAIN/ads/ssv`, then set `ADS_SSV=1` in `.env`. Google then tells your server directly that the video was watched (signed with Google's key) and only then are coins added. Without it the server takes the app's word, which a modified app could fake (still capped at 10 a day).
4. Host an `app-ads.txt` file on your website domain as AdMob shows you (put it in `client/public/app-ads.txt`), and link the app to its store listing once it's published.

On Google Play, declare in *App content* that the app contains ads. Because players stake coins on games, answer the content-rating questionnaire's simulated gambling question with *yes*; coins can't be bought or cashed out.

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
