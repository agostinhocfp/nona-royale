# Nona Royale — Online Play

> Location in repo: `docs/design/ONLINE.md` · Project copy: `claude/ONLINE.md`
> Status: **Planned, 2026-09-30.** No code has been written. Increments ON0–ON6 below.
> Related: ADR-0015 (the decision this doc builds), ADR-0004 (commands in, events out), `REPLAY.md` (codec, command log, rules fingerprint), `BOTS.md`, `DRAFT.md`, `MOBILE.md`, `CONVENTIONS.md`

## 1. Goal

Two to four people on different devices (PC and Android) play one match against each other, with or without CPU seats, through a server we run. A dropped connection, a backgrounded app or a server deploy costs a player a few seconds of "Reconnecting…", and never the match. Nobody can cheat by editing their client or by predicting the dice.

Out of scope for this stage: public matchmaking, ratings, the React web front end, and Steam or Google sign-in. Each comes later, and nothing built here blocks them.

## 2. The shape at a glance

```
 Unity client (PC / Android)                     Render (Frankfurt)
 ┌──────────────────────────────┐                ┌────────────────────────────────────┐
 │ View (unchanged)             │                │ NonaRoyale.Server (ASP.NET Core)   │
 │   ▲ events                   │                │                                    │
 │ Mirror GameEngine            │   wss://       │  SocketEndpoint ── MatchRegistry   │
 │   on TapeRandom  ◄───────────┼── ok{cmd,draws}┤                     │              │
 │                              │                │               MatchHost (one per   │
 │ OnlineSession ───────────────┼── cmd{n,...} ─►│               match, one loop)     │
 └──────────────────────────────┘                │                 │ GameEngine       │
                                                 │                 │ on KeyedRandom   │
 React web (later) ── https ──── /rooms, /guests │                 ▼                  │
                                                 │         Postgres: append-only log  │
                                                 └────────────────────────────────────┘
```

**The core idea:**

- The server holds the one authoritative `GameEngine`, and it is the only place randomness is decided.
- Each client holds a mirror engine.
- When the server accepts a command, it broadcasts the command and the random values that command consumed.
- Every mirror replays the same command against those values and arrives at the same state, producing the same events.
- The existing view, presentation queue, landing previews and target hints all read the mirror exactly as they read the local engine today.

## 3. Randomness

### 3.1 Why `SeededRandom` can't be used online

`SeededRandom(int seed)` wraps `System.Random(int)`. That gives 2³² possible seeds, and the generator is known because the client code is public.

The attack:

1. Record the first eight or so dice of a match.
2. Loop over every seed, build `System.Random(seed)`, and check whether the first draws match.
3. On one PC this finds the seed in minutes to hours.
4. From then on, the attacker knows every roll and every evasion check for the rest of the match.

Hiding the seed doesn't stop any of this. The only fix is a generator whose output doesn't reveal its state.

### 3.2 The three `IRandom` implementations this stage adds

| Type | Lives in | What it does |
|---|---|---|
| `KeyedRandom` | Server (`Rng/`) | HMAC-SHA256 over a 64-bit counter, keyed with 32 bytes from `RandomNumberGenerator`. It is deterministic given the key and unpredictable without it. `NextInt` uses rejection sampling so it has no modulo bias; `NextDouble` takes 53 bits and divides by 2⁵³. |
| `RecordingRandom` | Core (`Rng/`) | Wraps any `IRandom` and writes down every draw between `Begin()` and `End()`. The server wraps `KeyedRandom` with it. |
| `TapeRandom` | Core (`Rng/`) | Returns the draws of a tape, in order. It throws `TapeDesyncException` if the call's kind or range doesn't match the next entry, if the tape runs out, or if draws are left over when `AssertConsumed()` runs. The mirror runs on it. |

**Derived streams.** The server derives separate streams from one match key by using different HMAC labels, which replaces `seed ^ salt` online:

- `"match"` for the engine;
- `"draft"` for draft timeout picks;
- `"bots:<seat>"` for each CPU seat.

Bot and draft draws never go on the tape. Their results travel as ordinary commands and picks.

### 3.3 Tape format

The tape is a JSON array with one entry per draw, in call order:

```
[["i",1,7,4],["i",1,7,6],["d","3FB999999999999A"]]
```

- `["i", min, maxExclusive, value]` records a `NextInt`. The range is written down so that a mirror asking a different question fails loudly, instead of reading a value that happens to fit.
- `["d", "<16 hex digits>"]` records a `NextDouble` as its exact IEEE-754 bits. The core's JSON reader only accepts whole numbers (`REPLAY.md`), so doubles never appear as fractions.
- **Tape 0 is the deal:** every draw `MatchRecipe.Build` and the engine constructor make, such as random squads or the first seat. Tape *n* belongs to accepted command *n*.
- **A rejected command must consume no draws.** ON0 proves this with a test. If it's ever false, a refusal would move the stream forward on the server while the mirror stayed where it was.

## 4. Protocol

**Version:** `Protocol.Version = 1` in `NonaRoyale.Net`. Bump it on any change to a message's shape. A client on another version is refused with close code `4001`.

### 4.1 Connection

1. Open `wss://play.<domain>/ws`.
2. The client's first frame is `hello{proto, rules, token}`, where `rules` is `RulesFingerprint.Current`. If no hello arrives within 10 s, the server closes the socket (`4005`).
3. The server replies `welcome{player, heartbeat}`, or `refused{reason}` followed by a close.
4. The client sends `join{room}`. The server replies with the room's current state: `room`, `draft`, or `deal` followed by a `log`, depending on where the match is.

Every frame is one JSON object with a `t` field. Frames are written with the core's `JsonWriter` and read with its `JsonReader` (pure ASCII, strict). Commands inside `cmd` and `ok` use `CommandCodec`'s existing wire forms (`Roll`, `Deploy{op}`, `Move{op, die?}`, `Cast{…}`, `Cash{op, die}`, `End`).

### 4.2 Messages

**Client → server**

| `t` | Fields | When |
|---|---|---|
| `hello` | `proto`, `rules`, `token` | First frame |
| `join` | `room` | After `welcome` |
| `seat` | `seat` | Lobby: take an empty seat |
| `setseat` | `seat`, `kind` (`cpu` / `open`), `personality?` | Lobby, host only |
| `ready` | `ready` | Lobby |
| `start` | — | Host, once there are ≥ 2 seats and every human is ready |
| `draft` | `action` (`pick` / `clear` / `random` / `start`), `op?`, `slot?` | Draft (ON5) |
| `cmd` | `n` plus the `CommandCodec` fields | Your turn |
| `sync` | `have` (the last `seq` the client applied, −1 for none) | After a reconnect, or after a mirror desync |
| `leave` | — | Quitting |

**Server → client**

| `t` | Fields | Sent to |
|---|---|---|
| `welcome` | `player`, `heartbeat` (seconds) | Sender |
| `refused` | `reason` (`update` / `auth` / `full` / `noroom` / `proto`) | Sender, then close |
| `room` | `code`, `host`, `mode`, `seats[{seat, kind, name, ready, connected}]` | Everyone in the room |
| `draft` | A full `DraftState` snapshot: mode, clock ms, current seat, and per-seat slots with `op` and `random` | Everyone |
| `deal` | `recipe` (the `RecipeCodec` object **with no seed**), `tape` (tape 0), `seats` | Everyone |
| `ok` | `n`, `seat`, the command fields, `draws`, `origin` (`human` / `cpu` / `timeout`) | Everyone |
| `no` | `n`, `reason` | Sender only |
| `clock` | `seat`, `kind` (`roll` / `turn` / `draft`), `ms` | Everyone |
| `presence` | `seat`, `connected`, `cpu` | Everyone |
| `log` | `from`, `entries[ok…]` | Sender, in reply to `sync` |
| `end` | `reason` (`won` / `abandoned`), `winner?` | Everyone |
| `drain` | — | Everyone, just before a close with code `4000` |

**`n` makes a command idempotent.** The client sends the `seq` it expects the command to receive, which is the last `seq` it applied plus one. If the server's next `seq` is different, the command is stale: the server answers `no{reason: "stale"}`, and the client sends `sync`. Clients never resend a command after a reconnect. If it was accepted, it shows up in the `log`; if not, the player clicks again.

### 4.3 Close codes

| Code | Meaning | What the client does |
|---|---|---|
| 1000 | Normal close | Nothing |
| 4000 | Server draining (deploy or restart) | Reconnect immediately, then back off: 0.5 s, 1 s, 2 s, 4 s, then 5 s |
| 4001 | Update required (protocol or rules mismatch) | Show the update screen. **Don't retry.** |
| 4002 | Auth failed | Clear the token and re-register as a guest |
| 4003 | Replaced: the same player connected from somewhere else | Show "Opened on another device". Don't retry. |
| 4004 | Room closed | Return to the menu |
| 4005 | Protocol violation (bad frame, no hello, flooding) | Log it and don't retry. Seeing this means there's a bug. |

Any other drop (a network error with no close frame) is treated like 4000.

### 4.4 Limits and heartbeat

- A client frame can be at most 4 KiB, and a connection can send at most 20 frames a second. Crossing either limit closes the socket with `4005`.
- The server sets `WebSocketOptions.KeepAliveInterval = 20 s`, so a quiet turn never reaches an idle timeout on Render or on an AWS load balancer. A peer that stops answering is dropped, using `KeepAliveTimeout` on .NET 9+.
- Every limit is a field in `ServerOptions`, not a literal.

## 5. The server's match loop

### 5.1 One loop per match

`MatchHost` owns everything about one match: the `GameEngine`, its `RecordingRandom`, the `DraftState`, the seats, the clocks and the CPU brains. It runs as **one async loop reading one `Channel<IMatchInput>`**, the same pattern as a goroutine reading a channel in Go.

Socket frames, timer ticks, connects, drops and the drain signal are all messages posted to that channel. **Only that loop ever touches the engine**, so the engine never needs a lock.

`MatchRegistry` maps a room code to its `MatchHost`, and loads a match from Postgres the first time anyone asks for it.

### 5.2 Handling a `cmd`

1. The sender must hold the seat that `engine.CurrentPlayer` names, and that seat must not be under CPU takeover. Otherwise reply `no{reason: "not-your-turn"}`. Commands carry no seat (`REPLAY.md` seam 2); the server supplies it.
2. Check that `n` equals the next `seq`. Otherwise reply `no{reason: "stale"}`.
3. The frame was decoded at the socket edge with `CommandCodec`. A bad frame never reaches the loop.
4. Call `recording.Begin()`, then `events = engine.Execute(command)`, then `draws = recording.End()`.
5. If `events` contains a `CommandRejected`, reply `no` with its reason. Nothing is persisted, and `draws` must be empty; the server asserts this.
6. **Append before broadcasting:** insert `(match_id, seq, seat, command, draws, origin)`.
   - On a unique-key violation, another instance has written this match. Drop the in-memory host, reload it from Postgres, and reply `no{reason: "stale"}`.
   - On any other database error, do the same.
   - In both cases the in-memory engine is never allowed to be ahead of the log.
7. Broadcast `ok`.
8. Reset the clocks. If the match is won, set `status = finished` and send `end`.
9. If the next seat is a CPU (a CPU seat, or a seat under takeover), schedule a bot step after the think delay.

**The server never restates a rule.** Legality is the engine's answer, and a server that "knows" a move is illegal before asking the engine is a bug.

### 5.3 CPU seats and timeouts

- **CPU steps** call `BotBrain` on the authoritative engine and go through the same path, with `origin: "cpu"`.
- **Pacing:** the server spaces CPU actions using `BotDriver`'s Normal delays (0.9 s before a turn's roll, 0.55 s between actions), moved into `ServerOptions`. The client's CPU speed setting doesn't apply online.
- **Roll clock:** the existing 15 s rule. When it expires, the server sends `RollDiceCommand` with `origin: "timeout"`.
- **Turn clock** (§9 decision 2): when it expires, a `BotBrain` finishes the rest of the turn with `origin: "timeout"`. Movement is compulsory, so something has to choose the moves. Simply ending the turn isn't a legal option.
- **Takeover:** after K consecutive timeouts (§9 decision 4), or after a disconnect grace period, the seat goes under CPU takeover (`presence{cpu: true}`). The player takes the seat back at the start of their next turn by sending any command.

### 5.4 Match lifecycle

`lobby` → `draft` (ALL PICK and SNAKE only) → `playing` → `finished`, or `abandoned`.

- **Lobby:**
  - A room is created with `POST /rooms` and gets a 6-character code. The alphabet leaves out `0 O 1 I`, and codes are drawn from `RandomNumberGenerator`.
  - The host can set empty seats to CPU.
  - At `start`, the server generates the key, builds the match through `MatchRecipe` with a `KeyedRandom`, persists tape 0, and sends `deal`.
- **Draft (ON5):** the server owns the draft and its clock, and calls `DraftState.Tick` from the loop. Every pick or clear is persisted to `draft_picks` and broadcast as a full `draft` snapshot, which stays small.
- **Abandoned:** when every human seat has been disconnected for 10 minutes, the match is closed. The time is set in `ServerOptions`.

## 6. Persistence

### 6.1 Schema

```sql
-- server/src/NonaRoyale.Server/Persistence/Migrations/0001_init.sql
create table players (
  id           uuid primary key,
  display_name text not null,
  token_hash   bytea not null unique,      -- SHA-256 of the guest token; the token is never stored
  created_at   timestamptz not null default now(),
  last_seen_at timestamptz
);

create table matches (
  id             uuid primary key,
  room_code      text not null,
  status         text not null check (status in ('lobby','draft','playing','finished','abandoned')),
  proto          int  not null,
  rules_hash     text not null,            -- RulesFingerprint.Current when the room was made
  squad_mode     text not null,            -- Random | Alpha | AllPick | Snake
  recipe         text,                     -- RecipeCodec object text, no seed; set at deal
  rng_key        bytea,                    -- 32 bytes, set at deal; never leaves the server
  clock_seat     text,
  clock_kind     text,
  clock_deadline timestamptz,              -- restored with a grace period after a restart
  created_at     timestamptz not null default now(),
  started_at     timestamptz,
  finished_at    timestamptz,
  winner_side    int
);
create unique index matches_open_code on matches (room_code)
  where status in ('lobby','draft','playing');

create table match_seats (
  match_id     uuid not null references matches(id),
  seat         text not null,              -- Red | Blue | Green | Violet
  kind         text not null check (kind in ('human','cpu')),
  player_id    uuid references players(id),
  personality  text,                       -- CPU seats, and takeover style
  cpu_takeover boolean not null default false,
  primary key (match_id, seat)
);

create table draft_picks (
  match_id uuid not null references matches(id),
  seq      int  not null,
  seat     text not null,
  action   text not null check (action in ('pick','clear')),
  operator text,
  slot     int,
  random   boolean not null default false,
  at       timestamptz not null default now(),
  primary key (match_id, seq)
);

create table match_commands (
  match_id uuid not null references matches(id),
  seq      int  not null,                  -- 0 = the deal (tape only); 1.. = accepted commands
  seat     text,
  command  text,                           -- CommandCodec line; null for seq 0
  draws    text not null,                  -- the tape (§3.3)
  origin   text not null check (origin in ('deal','human','cpu','timeout')),
  at       timestamptz not null default now(),
  primary key (match_id, seq)
);
```

**`recipe` and `command` are stored as `text`, not `jsonb`.** `jsonb` re-orders keys and normalises whitespace, while the codec's output is canonical and strict. Stored as text, a row can be fed straight back to the codec.

### 6.2 Rebuilding a match after a restart

This runs lazily, the first time anyone joins the match after a restart:

1. Load the match, its seats, the recipe and the key.
2. Build `KeyedRandom(key)` wrapped in a `RecordingRandom`, and call `MatchRecipe.Build(random)`. The construction draws must equal tape 0.
3. For each command from seq 1 on: execute it. It must be accepted, and its draws must equal the stored tape.
4. Restore the clock as `max(clock_deadline, now) + RestartGrace` (10 s, config).

If any check fails, the match is marked `abandoned` with a logged reason, and every client gets `end{reason: "abandoned"}`. That should never happen. If it does, the stored log and tapes are the bug report: they can be replayed offline to find the command where the engine and the log disagreed.

Replay speed is not a concern. The replay stage's 60-match determinism test runs in about a second.

### 6.3 An online match as a replay file

The log is already a replay, just with tapes. ON6 adds `.nrr` **envelope format 2**:

- the header carries `"rng": "tape"` and has no seed;
- each line gains a `draws` field;
- tape 0 goes in the header.

`ReplayPlayer` plays format 2 on a `TapeRandom`. Format-1 files and the golden hash `f7820c60` are unaffected.

## 7. Reconnects, restarts and deploys

### 7.1 Client reconnect

1. The socket drops (Wi-Fi to 4G, a tunnel, the app backgrounded) or closes with 4000.
2. The client shows "Reconnecting…" on its seat tag and every other client sees `presence{connected: false}`. The seat's clocks keep running.
3. The client reconnects with backoff (§4.3), then sends `hello`, then `join`, then `sync{have}`.
4. The server replies `log{from, entries}`. The mirror applies the entries **without walking** (the path Instant CPU speed already uses), then presentation carries on as normal.
5. If the app was killed and has no mirror, it sends `have: -1` and gets `deal` plus the whole log.

### 7.2 Server drain on a deploy

Render starts the new instance, waits for its health check, sends new connections to it, and gives the old instance `SIGTERM` plus a grace period (`maxShutdownDelaySeconds`; set it to 30–60 s).

On `SIGTERM` (`IHostApplicationLifetime.ApplicationStopping`), every `MatchHost` on the old instance:

1. stops taking new input;
2. finishes the input it's handling, and with it the append;
3. sends `drain`;
4. closes every socket with `4000`.

The clients reconnect and land on the new instance, which rebuilds the match from the log (§6.2). **The new instance never loads a match on its own while the old one is still draining.** Loading is lazy and triggered by a join, and clients only rejoin after the old instance has closed their sockets. If the timing ever overlaps anyway, the seq-checked append (§5.2 step 6) turns it into a harmless reload.

### 7.3 Mobile

On `OnApplicationPause(true)`, leave the socket alone; the operating system will kill it or it will survive. On resume, reconnect and sync if the socket is closed. The turn clock keeps running while the app is in the background, so a long absence turns into timeouts and then a takeover, which is the rule working as designed.

## 8. Repo layout, build and hosting

### 8.1 Layout

The server lives in the game repo. Unity only imports what is under `Assets/`, so it ignores `server/`.

```
Assets/_Project/Scripts/Core/                 # unchanged; gains Rng/RecordingRandom, Rng/TapeRandom
Assets/_Project/Scripts/Net/                  # NEW asmdef NonaRoyale.Net: noEngineReferences, references Core
    Protocol.cs  Messages/*.cs  MessageCodec.cs  CloseCodes.cs
Assets/_Project/Scripts/Unity/Online/         # NEW: OnlineSession, OnlineSink, MirrorMatch, LobbyScreen
server/
  NonaRoyale.Server.sln
  Dockerfile  .dockerignore  docker-compose.yml
  src/
    NonaRoyale.Core/NonaRoyale.Core.csproj    # links ../../../Assets/_Project/Scripts/Core/**/*.cs
    NonaRoyale.Net/NonaRoyale.Net.csproj      # links ../../../Assets/_Project/Scripts/Net/**/*.cs
    NonaRoyale.Server/
      Program.cs                              # the composition root: options, DI, endpoints
      ServerOptions.cs
      Http/        GuestEndpoints.cs  RoomEndpoints.cs  HealthEndpoints.cs
      Sockets/     SocketEndpoint.cs  Connection.cs
      Matches/     MatchRegistry.cs  MatchHost.cs  IMatchInput.cs  SeatSlot.cs  TurnTimers.cs
      Rng/         KeyedRandom.cs
      Persistence/ Database.cs  MatchStore.cs  Migrations/0001_init.sql
  tests/
    NonaRoyale.Core.Tests/                    # links Assets/Tests/EditMode/**, minus Unity/
    NonaRoyale.Server.Tests/                  # KeyedRandom, MatchHost on an in-memory store, codec
    NonaRoyale.Soak/                          # console app: N sockets play bot seats through a real server
```

`TurnTimers` is deliberately **not** called `MatchClock`. The core already has a `MatchClock`.

When the core moves to a local UPM package for NR3D, only the two linking globs change.

### 8.2 The linked core project

```xml
<!-- server/src/NonaRoyale.Core/NonaRoyale.Core.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>  <!-- Unity's API level -->
    <LangVersion>9.0</LangVersion>                      <!-- what Unity compiles; blocks newer syntax -->
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <RootNamespace>NonaRoyale.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../../Assets/_Project/Scripts/Core/**/*.cs" />
  </ItemGroup>
</Project>
```

`NonaRoyale.Net.csproj` is the same, with its own glob and a `ProjectReference` to Core. The server itself targets `net10.0` with the latest C#.

**Test project gotcha: pin NUnit 3.x (for example 3.14), not 4.x.** The EditMode tests use classic asserts (`Assert.AreEqual`), which NUnit 4 moved to `ClassicAssert`. Add `NUnit3TestAdapter` and `Microsoft.NET.Test.Sdk`. The existing CLI harness (`tools/tests-verify/Runner.cs`) stays; this project is how the server's CI runs the same suite.

### 8.3 Local development (Windows)

```
docker compose -f server\docker-compose.yml up -d
dotnet test server\NonaRoyale.Server.sln
dotnet run --project server\src\NonaRoyale.Server
```

- **Editor:** connects to `ws://localhost:8080/ws`.
- **Phone on the same Wi-Fi:** connects to `ws://<pc-lan-ip>:8080/ws`. Allow port 8080 through Windows Firewall once.
- **In production:** `wss://play.<domain>/ws`. Render terminates TLS.
- **The server URL is a setting**, with a dev override in the editor and in development builds. It is never a literal.

### 8.4 Dockerfile

```dockerfile
# server/Dockerfile — build context is the repo root
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY server/ server/
COPY Assets/_Project/Scripts/Core/ Assets/_Project/Scripts/Core/
COPY Assets/_Project/Scripts/Net/  Assets/_Project/Scripts/Net/
RUN dotnet publish server/src/NonaRoyale.Server -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "NonaRoyale.Server.dll"]
```

`.dockerignore` excludes everything except `server/` and those two script folders. The repo carries Git LFS art that the build must never pull.

### 8.5 Render setup checklist

1. **Postgres:** Frankfurt, on a plan with backups.
2. **Web service:** Docker runtime, Dockerfile path `server/Dockerfile`, build context the repo root, region Frankfurt (the same as the database).
3. **Auto-deploy: off.** Deploy by hand, at quiet hours.
4. **Health check path:** `/healthz`.
5. **Environment:** `DATABASE_URL` (the database's *internal* URL), `NR_ENV=production`.
6. **Port:** read `PORT` at startup (Render sets it). Default to 8080 locally.
7. **Graceful shutdown:** `maxShutdownDelaySeconds` set to 30–60. Confirm the setting's current name in the dashboard at setup.
8. **Custom domain:** `play.<domain>`, a CNAME to the service.
9. **Scaling:** one instance, autoscaling off (ADR-0015).
10. **Check at setup:** that Render's clone of the repo doesn't choke on Git LFS objects. The build doesn't need them. If it's slow or fails, the fallback is to build the image elsewhere and deploy it from a registry.

**Npgsql gotcha:** Render hands out a `postgres://user:pass@host/db` URI, and Npgsql expects a `Host=…;Username=…` connection string. Convert it once at startup with `NpgsqlConnectionStringBuilder`.

**Migrations:** plain SQL files in `Persistence/Migrations/`, applied at startup by DbUp. They are forward-only, and the file name is the version.

## 9. Decisions to settle before the increment that needs them

| # | Decision | Recommended | Alternatives | Needed by |
|---|---|---|---|---|
| 1 | ADR-0015 decisions 7–9 (keyed RNG, tape mirror, no hidden info) | **Accept** | A per-seat state projection (heavier; only needed once something is hidden) | ON0 |
| 2 | Turn clock online | **15 s to roll (today's rule), then 45 s for the rest of the turn**, as config | One flat per-turn budget; a chess-style bank per player | ON5 |
| 3 | When the turn clock runs out | **A CPU plays out the rest of the turn** | End the turn (not legal while dice can still move, because movement is compulsory) | ON5 |
| 4 | Repeated timeouts | **After 2 in a row, the CPU takes the seat until the player acts again** | Kick after N; never take over | ON5 |
| 5 | Disconnects | **Clocks keep running; after 90 s the CPU takes the seat; the player takes it back at their next turn** | Forfeit after a grace period | ON5 |
| 6 | Pause online | **None** | A pause vote with a cap | ON3 |
| 7 | CPU seats in online rooms | **Allowed, set by the host** | Humans only | ON2 |
| 8 | Draft helpers online | **SNAKE undo off; ALL PICK clears only your own slots** | Keep undo (lets one player grief the others) | ON5 |
| 9 | Identity for v1 | **Guest token** (32 random bytes, kept in `PlayerPrefs`, stored hashed) | Steam or Google sign-in from day one | ON1 |
| 10 | Online in the Steam early-access build or after it | **Decide after ON4**, once the real cost is known | — | Before the EA scope freeze |
| 11 | Online replays | **Format 2 with tapes** | Reveal the key after the match, so a seed-style replay works | ON6 |
| 12 | Display names | **3–16 characters, no moderation in v1** (rooms are joined by code, so it's friends only) | A filter list | ON1 |

## 10. Increments

| # | Increment | Delivers | Done when |
|---|---|---|---|
| **ON0** | **Core seams** (Unity repo; EditMode tests; no server yet) | `MatchRecipe.Build(IRandom)` and a `MatchFactory` overload that takes an `IRandom`. Draft and bot streams that accept an `IRandom`. `RecordingRandom`, `TapeRandom`, `TapeDesyncException`. **An audit** of everything the view does on a player's behalf: the 15 s roll timer, auto end-turn, draft timeouts, first-seat pick, pity deploy. Each one is either reachable from the core or listed as server work. | Every one of the 60 seeded bot matches (the `ReplayDeterminismTests` sweep) replays on tapes to an identical event stream. Every refusal records an empty tape. `Build(new SeededRandom(s))` equals `Build` with seed `s`. The whole existing suite still passes. The golden hash doesn't move. |
| **ON1** | **Server skeleton** | The solution, the linked Core and Net projects, and the core suite running under `dotnet test` on .NET 10. Minimal host with `/healthz`, `ServerOptions`, `KeyedRandom`, DbUp and `0001_init.sql`, the guest endpoint, Dockerfile and compose. Deployed to Render at `play.<domain>`. | The core suite passes under .NET 10 with the same count as the CLI harness (core plus EditMode, excluding `Tests/EditMode/Unity/`). `KeyedRandom` passes its tests: known-answer HMAC vectors, a chi-square check on 10⁶ d6 rolls, and rejection-sampling edge cases. `/healthz` returns 200 over `https://play.<domain>`, and a socket opens over `wss`. Migrations have run on Render Postgres. |
| **ON2** | **Protocol and match host** (Random squads, bot seats allowed) | `NonaRoyale.Net` (messages, codec, close codes), `SocketEndpoint`, `MatchRegistry`, `MatchHost` (§5), rooms, append-before-broadcast, rebuild (§6.2), and the `NonaRoyale.Soak` headless client. | 100 soak matches over real sockets, at 2–4 seats, finish with **zero desyncs** and zero stuck turns. Killing the server mid-match locally (`docker kill`) and restarting it: every match rebuilds and finishes. |
| **ON3** | **Unity client** | A seam in `MatchBootstrap.Send` (`LocalSink` for today's behaviour, `OnlineSink` for online), `OnlineSession` (`ClientWebSocket`, a receive loop into a main-thread queue), `MirrorMatch` on `TapeRandom`, and a minimal lobby (create a room, join by code, seats, ready, start). Input waits for `ok`/`no` after a send. Connection status in the HUD. | A PC and the S26 Ultra play a full match through Render. Mirror desyncs: 0. An illegal click is refused locally with no round trip. **Check that `ClientWebSocket` works under IL2CPP on Android.** If it doesn't, fall back to the NativeWebSocket package. |
| **ON4** | **Reconnect and drain** | Backoff, `sync`/`log`, catching up without walking, presence tags, SIGTERM drain (§7.2), clock restore. | Airplane mode on the phone for 30 s mid-turn, then back: same state, no desync. A manual Render deploy mid-match: both clients show "Reconnecting…" for under 5 s and play on. The app backgrounded for 2 minutes resumes. |
| **ON5** | **Clocks, absence and the online draft** | Server-owned roll, turn and draft clocks; timeout CPU; takeover and taking the seat back; CPU seats in rooms; ALL PICK and SNAKE online (§9 decisions 2–5, 7, 8). | A player who walks away gets CPU turns and can take the seat back. ALL PICK at 4 seats online, timeout fill included. SNAKE with a CPU seat. |
| **ON6** | **Records** | `.nrr` format 2 export (§6.3), match history endpoints, and the first Steam sign-in design (a separate decision). The React front end is its own stage after this one. | A finished online match downloads as a `.nrr` and plays in `ReplayPlayer`. |

ON0 changes shared code, so it follows the Unity-repo rules below. ON1 and ON2 can proceed while the game is being tightened, because they only touch `server/` and the new `Net/` assembly.

## 11. .NET for a Go developer

| Go | .NET here |
|---|---|
| goroutine reading a `chan` | `Task` reading a `System.Threading.Channels.Channel<T>` (the `MatchHost` loop) |
| `net/http` mux and handlers | Minimal APIs: `app.MapGet("/healthz", () => Results.Ok())` |
| `context.Context` | `CancellationToken`, passed down explicitly |
| A long-running goroutine started from `main` | `BackgroundService` / `IHostedService` |
| Wiring dependencies by hand in `main` | `builder.Services.AddSingleton<T>()`. `Program.cs` is the composition root. |
| `database/sql` + `sqlx` | Npgsql + Dapper |
| goose migrations | SQL files + DbUp |
| `go test` | `dotnet test` (NUnit 3.x in this repo) |
| `go.mod` | `<PackageReference>` in the `.csproj` |
| `defer x.Close()` | `using var x = …;` / `await using` |
| `signal.Notify(SIGTERM)` | `IHostApplicationLifetime.ApplicationStopping` |
| `errgroup` | `Task.WhenAll` |

Traps worth knowing:

- Never write `async void` except in event handlers. Exceptions escape it and crash the process.
- Never block on `.Result` or `.Wait()`; use `await`.
- The engine is not thread-safe. It is touched only inside its `MatchHost` loop (§5.1).

## 12. Standing rules for this stage

**Carried from `NEXT_PHASES.md` and `REPLAY.md`:**

1. Don't trust the project snapshot; read the repo. Check HEAD first.
2. The core has zero Unity types and no LINQ. The view computes nothing. Constants are config, not literals. No singletons.
3. Every core rule ships with EditMode tests.
4. Deliver whole files, each starting with a comment giving its path.
5. Log in this doc **before** the commit. Hand over `git add <explicit paths>` plus `git commit -m "type(scope): subject" -m "body"`, with no AI trailers. Include the `.meta` files of new scripts.
6. Preserve each file's line endings. New `.cs` files are CRLF with no final newline. Docs are LF with a final newline.
7. Play Mode on Windows, plus the device, is the acceptance test for anything in Unity.

**New for online:**

8. **The server is the authority.** A client never executes a command on its mirror before the server's `ok`.
9. **The server never restates a rule.** Legality is always the engine's answer.
10. **Only the owning `MatchHost` loop touches an engine.**
11. **Append before broadcast.** The in-memory engine is never ahead of the log.
12. **No Render-only services, all config from the environment, no hosting hostname in a client build** (ADR-0015 decision 6).
13. **Any change to a message's shape bumps `Protocol.Version`.** Any rules change moves the golden hash, and old clients get "update required".

## 13. Log

- **2026-09-30: stage planned.** In chat, the designer settled:
  - a dedicated authoritative server;
  - ASP.NET Core on .NET 10 hosting the core;
  - raw WebSockets with JSON;
  - a Postgres command log;
  - Render for hosting, chosen over Hetzner for developer experience, with AWS as the later migration path;
  - the portability rules.

  Reading the tree turned up two things:
  - **The core already runs outside Unity** (`REPLAY.md`: 859/0 under .NET 8), so the planned "does the core compile" spike is already answered.
  - **`SeededRandom` has a 32-bit seed**, so its dice can be predicted by searching every seed. That led Claude to propose ADR-0015 decisions 7–9 (the keyed RNG, the tape mirror, no hidden information), pending the designer's review.

  No code written.

## Start prompt for the online chat

> Online play from `claude/ONLINE.md` and ADR-0015 (`claude/0015-online-authoritative-server.md`). Read both, request folder access, check HEAD, then settle §9 decision 1 before ON0.
