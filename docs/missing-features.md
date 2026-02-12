# MeatSpeak IRC Server — Missing & Incomplete Features

Status audit as of 2026-02-12. Covers gaps relative to RFC 1459/2812 and modern IRCv3 expectations.

---

## Critical — Server Misbehaves Without These

### ~~Ping/Pong Timeout Enforcement~~ (FIXED)
`PingTimeoutService` is a registered `BackgroundService` that periodically iterates sessions, sends PING to idle clients, and disconnects any session that hasn't responded within `PingTimeout` seconds. Also enforces `RegistrationTimeout` for pre-registration sessions.

### ~~Database Context Not Wired Up~~ (FIXED)
Database is now wired up with SQLite as the zero-config default. PostgreSQL and MySQL are supported via the `Database.Provider` and `Database.ConnectionString` config fields. All repositories, `PermissionService`, and `DatabaseSeeder` are registered and run on startup.

### ~~Server-Wide Ban Enforcement~~ (FIXED)
`RegistrationPipeline` checks all active K-lines against the connecting user's hostmask (nick!user@host) using IRC wildcard matching during registration. Matching sessions receive `ERR_YOUREBANNEDCREEP` (465) and are disconnected with the ban reason.

### ~~Flood Protection / Rate Limiting~~ (FIXED)
Per-session token-bucket flood limiter with configurable burst/refill. Per-IP connection limits (`MaxConnectionsPerIp`, default 10) and global `MaxConnections` limit enforced at connection time. `ExemptIps` config bypasses limits for benchmarks/monitoring. `[FloodPenalty]` attributes on expensive commands (LIST, WHO, WHOIS, AUTHENTICATE, etc.). Slow-mode (`+S`) channel enforcement still TODO.

---

## Missing Standard IRC Commands

### AWAY
Set/clear away status. Clients expect WHOIS to show away reason and PRIVMSG to auto-reply when the target is away. Needs an `AwayMessage` field on `SessionInfo` and `RPL_AWAY` (301), `RPL_UNAWAY` (305), `RPL_NOWAWAY` (306) numerics.

### WALLOPS
Broadcast a message to all users with `+w` user mode. Typically operator-only. Infrastructure for `+w` mode already exists in ModeHandler.

### ~~WHOWAS~~ (DONE)
Fully implemented with bounded history ring buffer and flood penalty.

### USERHOST
Lightweight nick-to-host lookup (`USERHOST nick1 nick2 ...`). Many clients use this during connection to detect their own host. Returns `RPL_USERHOST` (302).

### TIME
Return the server's local time. Trivial to implement — returns `RPL_TIME` (391).

### ADMIN
Return server admin contact info from config. Returns `RPL_ADMINME` (256), `RPL_ADMINLOC1` (257), `RPL_ADMINLOC2` (258), `RPL_ADMINEMAIL` (259).

### INFO
Return server software info/credits. Returns `RPL_INFO` (371) and `RPL_ENDOFINFO` (374).

### STATS
Server statistics. Common queries: `STATS u` (uptime), `STATS o` (O-lines), `STATS l` (connections). Returns various `RPL_STATS*` numerics.

---

## Stubbed / Incomplete Subsystems

### ~~SASL Authentication~~ (PARTIALLY FIXED)
SASL PLAIN is implemented — `AuthenticateHandler` validates credentials against `UserAccountEntity` (Argon2id hashed passwords) in the database. `sasl` capability is registered. Admin API `account.create` method provisions accounts.

**Still TODO:**
- SCRAM-SHA-256 mechanism
- Ed25519 mutual auth via `MeatSpeak.Identity` (`MutualAuth.CreateServerHello()`, `VerifyClientHello()`)

### Voice Subsystem
The transport layer exists (`MeatSpeak.Server.Voice` — SFU router, SSRC manager, silence detection, transport encryption) but the five IRC command subhandlers are all empty TODO stubs:
- `VoiceJoinHandler` — join a voice channel
- `VoiceLeaveHandler` — leave a voice channel
- `VoiceMuteHandler` — mute/unmute
- `VoiceKeyHandler` — exchange encryption keys
- `VoiceHandler` — main dispatcher

The UDP listener in `ServerHost` is started but the packet handler is not connected to the voice subsystem.

### IRCv3 Capabilities
`sasl` and `labeled-response` capabilities are now registered and functional. MONITOR command implemented (with `MONITOR=100` ISUPPORT token). Still unregistered:
- `message-tags` — arbitrary key-value tags on messages
- `server-time` — server-side timestamps on messages
- `echo-message` — echo sent messages back to sender
- `batch` — group related messages
- `away-notify` — push AWAY status changes to channel members
- `account-notify` — push account login/logout to channel members
- `extended-join` — include account and realname in JOIN
- `multi-prefix` — show all prefix modes in NAMES/WHO (not just highest)
- `cap-notify` — notify clients of new/removed caps

---

## Not Blocking but Worth Noting

### No Persistence
All channels, memberships, topics, and bans are in-memory. A server restart loses everything. Channel and topic state could be persisted to the database.

### No Server-to-Server Linking
Single-server only. No SQUIT, SERVER, CONNECT, LINKS, or MAP commands. No spanning tree protocol. This is acceptable for most deployments.

### No Services Framework
No NickServ, ChanServ, or equivalent. No account registration, email verification, nick grouping, or channel ownership. The permission/role system exists but there's no way for users to self-register accounts — only the admin API can assign roles.

### ISUPPORT Gaps
Current tokens: `NETWORK`, `CHANMODES`, `PREFIX`, `CHANTYPES`, `CASEMAPPING`, `NICKLEN`, `CHANNELLEN`, `TOPICLEN`, `STATUSMSG`, `CHANLIMIT`, `MONITOR`. Missing common tokens:
- `MODES` — max mode changes per command
- `MAXLIST` — max entries in ban/invite/except lists
- `EXCEPTS` — ban exception mode letter
- `INVEX` — invite exception mode letter
- `MAXTARGETS` — max targets per PRIVMSG/NOTICE
- `AWAYLEN` — max away message length

### No TLS STARTTLS
Only implicit TLS on dedicated ports (6697 for IRC, 443 for WebSocket). No in-band STARTTLS upgrade. This matches modern best practice — STARTTLS is deprecated by most networks in favor of implicit TLS.

---

## What's Solid

These features are fully implemented and tested:

- Connection registration (NICK, USER, PASS, CAP negotiation)
- Messaging (PRIVMSG, NOTICE — channel and private)
- All channel operations (JOIN, PART, KICK, TOPIC, INVITE, NAMES, LIST)
- Comprehensive mode handling (user modes: i/w/o, channel modes: n/t/i/m/s/k/l/o/v/b/V/S/E)
- Channel queries (WHO, WHOIS, WHOWAS)
- Server info (MOTD, LUSERS, VERSION, ISUPPORT)
- Operator commands (OPER with Argon2id password hash, KILL, REHASH)
- Ping/pong timeout enforcement with registration timeout
- Flood protection (token bucket, per-IP limits, global limits, command penalties)
- SASL PLAIN authentication (Argon2id credential validation, user accounts)
- IRCv3: labeled-response, MONITOR
- WebSocket transport with IRC subprotocol
- TLS on IRC (port 6697) and WebSocket (port 443)
- ACME certificate provisioning (Let's Encrypt with HTTP-01 and DNS-01)
- Role-based permission system with server and channel scopes
- Admin API (JSON-RPC at `/api` with Argon2id API key auth, IP allowlist, all CRUD methods)
- Admin frontend (SPA at `/admin`)
- Event bus for internal pub/sub
