# ADR-0015 — Online play runs on an authoritative .NET server that hosts the core

- **Status:** Accepted 2026-09-30 for decisions 1–6. The designer agreed to the shape in chat. Decisions 7–9 are Claude's proposals, **pending designer review**. Nothing is built yet.
- **Date:** 2026-09-30
- **Location:** `docs/decisions/0015-online-authoritative-server.md` · Project copy: `claude/0015-online-authoritative-server.md`
- **Completes:** ADR-0004 ("full netcode is its own future ADR"). **Relates to:** `docs/design/ONLINE.md` (the stage doc for building this), `REPLAY.md` (command log, codec, rules fingerprint), `BOTS.md`, `DRAFT.md`, `MOBILE.md`, `GDD.md` §6 (online is out of MVP scope).

## Context

ADR-0004 left a seam for online play: commands are plain serializable data, the core is deterministic, and the view only renders events. It promised that online play would mean swapping the transport, not re-architecting the game. The replay stage then proved more than that:

- the core runs outside Unity (the CLI harness passes on .NET 8, with C# 9);
- `CommandCodec` gives every command a strict JSON wire form;
- `RulesFingerprint` identifies the rules version with an 8-hex-digit hash;
- a seed plus the accepted commands reproduces a whole match.

This ADR decides where the authority runs, what it is written in, where state lives, and how randomness stays fair once players can see each other's dice from different machines.

Three constraints shaped it:

- **The game is turn-based.** A few small messages go out per action, and a few hundred milliseconds of latency is invisible. Nothing needs real-time transform sync, lag compensation or client-side prediction.
- **There is exactly one rulebook, and it is C#.** A server in any other language would be a second implementation that has to be kept in step with the first by hand.
- **The designer is solo and knows Go and JS better than .NET.** The web front end is React with Tailwind and shadcn/ui, and that is non-negotiable. Postgres is preferred.

## Decision

1. **A dedicated authoritative server, which we build ourselves.** Clients send intentions (commands); the server validates them against the one real `GameEngine` and announces what happened. Platform services (Steam, Unity Gaming Services) are not the authority. If we use them later, it will be for lobbies, identity or relays around the game.
2. **The server is ASP.NET Core on .NET 10 (the current LTS), and it references `NonaRoyale.Core` directly.** The rules are never ported. The core is compiled from the same source files Unity compiles, pinned to C# 9 so that the server build cannot accept syntax that Unity would reject.
3. **Transport: raw WebSockets carrying JSON text frames**, encoded with the core's own `JsonWriter`/`JsonReader` and `CommandCodec`. SignalR is not used: its Unity client is awkward under IL2CPP on mobile. Room creation and identity go over plain HTTPS.
4. **Postgres holds the durable truth; a match's live state is in memory.** Every accepted command is appended to a `match_commands` table under a unique `(match_id, seq)` key **before** it is broadcast. After a restart, a match is rebuilt by replaying its log. Npgsql with Dapper; migrations are plain SQL files.
5. **Hosting on Render**: a Docker web service with Render Postgres, in an EU region (Frankfurt). The service runs as one instance. Render was chosen over Hetzner for developer experience, and over AWS because AWS's setup cost only pays off at a scale this game won't reach soon. AWS stays the planned place to migrate if it's ever needed.
6. **Portability is a standing rule, so that moving to AWS stays cheap:**
   - the server ships as a Docker image;
   - it uses standard Postgres and no Render-only services (Key Value, cron jobs, workers) in the architecture;
   - all configuration comes from environment variables;
   - the server lives on our own domain (`play.<domain>`), never `*.onrender.com`, because a hostname baked into a shipped mobile build can't be changed;
   - the server sends heartbeats every 20–30 s, which AWS's 60 s load-balancer idle timeout will need.
7. **(Proposed) Online matches draw their randomness from a server-side cryptographic RNG, never from `SeededRandom`.**
   - `SeededRandom` wraps `System.Random(int)`, which has a 32-bit seed. The client code is public, so a player who records the first few dice can try every possible seed offline, find the one that matches, and predict every future roll. Keeping the seed secret doesn't prevent this; it only means the attacker has to search for it.
   - For online matches the server builds a `KeyedRandom`: HMAC-SHA256 in counter mode, keyed with 32 bytes from the operating system's cryptographic RNG. It is still deterministic given the key, which is why decision 4's crash recovery still works. The key never leaves the server.
   - Bot and draft streams are derived from the same key under different labels, replacing the `seed ^ salt` scheme online.
   - Local, hot-seat and CPU matches keep `SeededRandom`. The replay format and the golden hash are unaffected.
8. **(Proposed) Every client runs a mirror of the engine, driven by a draw tape.**
   - When the server accepts a command, it broadcasts the command **plus every random value the command consumed** (its "draws"). The draws the engine makes while dealing the match are sent the same way, as entry 0.
   - Each client applies the command to its own `GameEngine`, built on a `TapeRandom` that returns exactly those values. The client gets an identical state and the same events as the server, and the existing view, presentation queue, hints and previews keep working unchanged. The server never sends events and never answers preview queries.
   - Tapes only reveal draws that have already happened. The key isn't in them, so future rolls stay unpredictable.
   - A mirror that rejects a command the server accepted has desynced, and it resyncs from the log.
9. **(Proposed) The game has no hidden information online, and this design depends on it.**
   - Every client holds the full state, just as every player sees the whole table at hot-seat. The one secret is **future randomness**, and decision 7 protects it.
   - If a future mechanic hides anything from a seat (a concealed trap, hidden draft picks, a hidden hand), this ADR must be revisited. The server would then send each seat its own view of the state instead of broadcasting the command stream.

## Consequences

- **The core gains small seams, and nothing about the rules changes:**
  - `MatchRecipe`/`MatchFactory` accept an injected `IRandom`;
  - the draft and bot streams accept injected `IRandom`s;
  - two pure decorators are added, `RecordingRandom` and `TapeRandom`.
  - `KeyedRandom` lives in the server, because the client never needs it.
  - The seams come with tests, and a tape replay must produce the same events as a direct run.
- **Only the server runs clocks and acts on a player's behalf.** Online, anything the view currently does automatically for a player moves to the server: the 15 s roll timer, auto end-turn, draft timeouts and CPU seats. `ONLINE.md` ON0 audits where each of these lives today.
- **Rules changes become coordinated releases.** A client whose `RulesFingerprint` doesn't match the server's is refused with "update required". Mobile store review adds lag to every rules patch, so balance changes are batched. Running several rules versions at once on one server is possible later, but not planned.
- **One instance, and state is in memory.** No horizontal scaling. During a deploy, old and new instances overlap briefly; the `(match_id, seq)` unique key and a seq-checked append keep two instances from both writing a match. Clients reconnect through the drain protocol in `ONLINE.md`.
- **The reconnect path is required, not a nice-to-have.** Wi-Fi to 4G handoffs, a backgrounded app and host restarts all go through it.
- **The designer has new ground to learn:** ASP.NET Core hosting, dependency injection, `System.Threading.Channels`, Npgsql. `ONLINE.md` maps each of these to its Go equivalent.
- **The React front end talks to the same HTTP API.** It covers account, history, leaderboards and admin, and it never runs rules.

## Open

Settle these with the designer before `ONLINE.md` ON5. Recommendations are in `ONLINE.md` §9.

- [ ] Designer review of decisions 7–9.
- [ ] Turn clock length online, and what happens on a timeout.
- [ ] Disconnect grace period, and whether a CPU takes the seat or the player forfeits.
- [ ] Identity for v1: guest tokens now, Steam and Google sign-in later.
- [ ] Whether online is part of the Steam early-access build or comes after it.

## Options considered

- **A listen server (one player hosts), over a relay.** Close to free. But the host can cheat, can read the seed, and ends the match for everyone when their phone locks.
- **A server in Go or Node with the core ported.** It fits the designer's strongest stack, but it creates two rulebooks. The first time they drift, a legal click is refused, which undoes what determinism was protecting.
- **A hybrid: a Go platform plus a .NET match service,** over gRPC or NATS. A legitimate architecture, but for a solo developer it means two services and two deploy pipelines before a single match has been played online. Revisit it if the platform side grows.
- **Unity networking libraries** (Netcode for GameObjects, Mirror, Fish-Net). These are built to sync transforms in real time. They would fight a command/event architecture and pull rules into a Unity build.
- **Lockstep: clients run the core and only commands travel.** This is decision 8 without an authority and with a shared seed, so every client could predict the dice. Decision 8 keeps what makes lockstep cheap and moves the randomness to the server.
- **The server answers queries, and clients don't run the engine.** Every hover and landing preview would become a round trip, and the protocol would have to grow a message for every engine query.
- **Hetzner** was rejected for its operations burden, AWS for its setup cost, and Fly.io and Railway for their reliability records. DigitalOcean (App Platform plus Managed Postgres) is the close second.
