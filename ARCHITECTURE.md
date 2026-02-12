# meatSpeak — Architecture Reference

## Quick Start

```bash
dotnet build MeatSpeak.Server.sln
dotnet test MeatSpeak.Server.sln --verbosity minimal
cd src/MeatSpeak.Server && dotnet run
```

- **Language**: C# 12 / .NET 9
- **Testing**: xUnit (991 tests across 8 test projects)
- **Database**: EF Core 9 (SQLite default, PostgreSQL, MariaDB)
- **Config**: `src/MeatSpeak.Server/Config/server-config.json`

## Solution Structure

```
src/
  MeatSpeak.Protocol          Zero-alloc IRC + voice protocol parsing (Span<byte>)
  MeatSpeak.Server.Core       Interfaces, registries, event bus, config, flood limiter
  MeatSpeak.Server.Transport  TCP/TLS/WebSocket/UDP transports, connection pooling
  MeatSpeak.Server.Permissions  Bitfield permissions (ulong), roles, channel overrides
  MeatSpeak.Server.Data       EF Core persistence, repositories, background write queue
  MeatSpeak.Server.Voice      SFU voice router, SSRC, encryption stubs
  MeatSpeak.Server.AdminApi   JSON-RPC 2.0 admin API, API key auth (Argon2id)
  MeatSpeak.Server            Main app — all IRC handlers, server host, Program.cs DI
tests/
  One test project per source project (e.g. MeatSpeak.Protocol.Tests)
tools/
  MeatSpeak.Benchmark         Performance benchmarking harness
```

### Dependency Graph

```
MeatSpeak.Server (executable)
├── Protocol
├── Server.Core → Protocol, Permissions
├── Server.Transport
├── Server.Permissions
├── Server.Data → Server.Core, Permissions
├── Server.AdminApi → Server.Core, Permissions, Server.Data
└── Server.Voice → Protocol, Server.Core, Server.Transport
```

## Protocol Layer (MeatSpeak.Protocol)

All parsing is **zero-allocation** using `ReadOnlySpan<byte>` / `Span<byte>`.

| File | Purpose |
|------|---------|
| `IrcLine.cs` | `TryParse()` — parses raw bytes into `IrcLineParts` (tags, prefix, command, params, trailing) |
| `MessageBuilder.cs` | `Write()` / `WriteNumeric()` — builds IRC messages into stack-allocated buffers |
| `IrcMessage.cs` | Higher-level parsed message (string-based, allocated from IrcLineParts) |
| `IrcTags.cs` | IRCv3 message tag parsing |
| `VoicePacket.cs` | 13-byte header: version(1) type(1) flags(1) ssrc(4) seq(2) timestamp(4) + payload |
| `IrcConstants.cs` | `MaxLineLength=512`, `MaxLineLengthWithTags=4608`, byte constants |
| `Numerics.cs` | 70+ IRC numeric constants (RPL_*, ERR_*) |

## Core Interfaces (MeatSpeak.Server.Core)

**IServer** — Central state: sessions, channels, nick index, registries, event bus, WHOWAS history
**ISession** — Connected client: state machine, send methods, SessionInfo, cached permissions
**IChannel** — Channel: members (ConcurrentDictionary), bans/excepts/invites, modes, topic
**ICommandHandler** — `Command` name, `MinimumState`, `HandleAsync(session, message, ct)`

### Session State Machine

```
Connecting → CapNegotiating → Registering → Registered → Authenticated → Disconnecting
```

## Message Flow

```
TCP/TLS/WebSocket bytes
  → LineFramer scans for CRLF
  → IrcLine.TryParse() (zero-alloc)
  → CommandRegistry.Resolve(command)
  → Check session state >= handler.MinimumState
  → Check [RequiresServerPermission] / [RequiresChannelPermission]
  → FloodLimiter token bucket (+ [FloodPenalty] per command)
  → Task.Run + per-session SemaphoreSlim (one command at a time per session)
  → handler.HandleAsync()
  → Response via MessageBuilder.Write() to stack buffer → connection.Send()
```

Key file: `IrcConnectionHandler.cs` (307 lines) — OnConnected, OnData, OnDisconnected

## Transport Layer (MeatSpeak.Server.Transport)

**IConnection** — abstraction: `Send(ReadOnlySpan<byte>)`, `SendAsync`, `Disconnect`
**IConnectionHandler** — callbacks: `OnConnected`, `OnData(line)`, `OnDisconnected`

| Transport | Key Files | Notes |
|-----------|-----------|-------|
| TCP | `TcpServer.cs`, `TcpConnection.cs` | SocketAsyncEventArgs, sync continuation loop, NoDelay |
| TLS | `TlsTcpServer.cs`, `TlsTcpConnection.cs` | SslStream, SemaphoreSlim for write serialization, TLS 1.2/1.3 |
| WebSocket | `WebSocketConnection.cs`, `WebSocketMiddleware.cs` | "irc" subprotocol, frame-per-line |
| UDP | `UdpListener.cs`, `UdpSender.cs` | Voice packets, 1500 MTU, 4 concurrent receives |

**Pooling**: `BufferPool` (ArrayPool), `SocketEventArgsPool` (pre-allocated send args)
**Buffer size**: 4610 bytes (4096 tags + 512 message + 2 CRLF)

## IRC Handlers (MeatSpeak.Server/Handlers/)

37 handlers organized by category:

| Category | Handlers |
|----------|----------|
| Connection | PASS, NICK, USER, CAP, QUIT, PING, PONG, AWAY, MONITOR |
| Channels | JOIN, PART, MODE, TOPIC, KICK, INVITE, NAMES, LIST, WHO, WHOIS, WHOWAS |
| Messaging | PRIVMSG, NOTICE, TAGMSG, REDACT |
| Server Info | MOTD, LUSERS, VERSION, ISUPPORT |
| Operator | OPER, KILL, REHASH |
| Auth | AUTHENTICATE (SASL PLAIN) |
| Voice | VOICE (+ stubs: VJOIN, VLEAVE, VKEY, VMUTE) |
| History | CHATHISTORY (IRCv3 draft/chathistory) |

Registered in `Program.cs` (lines ~426-470). Dispatch in `IrcConnectionHandler.OnData()`.

## State Management (MeatSpeak.Server/State/)

**ServerState.cs** — `ConcurrentDictionary` for sessions, nick index (case-insensitive), channels. WHOWAS circular buffer (100/nick).

**SessionImpl.cs** — Wraps IConnection. `SessionInfo` struct holds: nickname, username, hostname, realname, channels set, away msg, account, capabilities, flood limiter, monitor list, activity timestamps.

**ChannelImpl.cs** — `ConcurrentDictionary<string, ChannelMembership>` for members. Lock-protected ban/except/invite lists. Wildcard pattern matching for bans.

## Permissions (MeatSpeak.Server.Permissions)

Bitfield-based (`ulong` flags) for performance.

- **ServerPermission**: 14 flags (ManageServer, KillUsers, ManageBans, BypassThrottle, Owner=bit63, etc.)
- **ChannelPermission**: 22 flags (ViewChannel, SendMessages, KickMembers, VoiceConnect, etc.)
- **Role**: Id, Name, Position (hierarchy), ServerPermissions, DefaultChannelPermissions
- **ChannelOverride**: Per-role per-channel Allow/Deny overrides
- **BuiltInRoles**: @everyone (Guid.Empty), Moderator, Admin

Resolution: Owner check → OR all role perms → apply channel overrides (deny then allow). Cached in `session.CachedServerPermissions`.

## Data Layer (MeatSpeak.Server.Data)

**MeatSpeakDbContext** — 11 entities: Role, UserRole, ChannelOverride, ServerBan, AuditLog, Channel, TopicHistory, UserHistory, ChatLog (ULID msgId), Reaction, UserAccount

**DbWriteQueue** — Bounded channel (10k items, drop oldest). Background `DbWriteService` drains queue. Fire-and-forget for: chat logs, user history, channel upserts, topic history, message redaction.

**Repository pattern**: IBanRepository, IRoleRepository, IAuditLogRepository, IChannelRepository, etc.

## Admin API (MeatSpeak.Server.AdminApi)

JSON-RPC 2.0 at `POST /api`. Auth via Argon2id-hashed API keys (`CryptographicOperations.FixedTimeEquals`).

Methods: server.stats, user.list, user.kick, channel.list, channel.info, channel.topic, ban.add, ban.remove, role.create, role.assign, audit.query, chatlog.query, account.create, etc.

## Voice System (MeatSpeak.Server.Voice)

**SfuRouter** — Selective Forwarding Unit, routes packets between participants. Silence detection, deafened-user filtering. No server-side mixing.

**VoicePacketRouter** → `IUdpPacketHandler` — maps UDP endpoints to VoiceSessions, validates SSRC, routes to SFU.

Components: VoiceSession, VoiceChannel, SsrcManager, TransportEncryption (XChaCha20-Poly1305 stub), SilenceDetector, VoiceTokenManager.

## IRCv3 Capabilities

server-time, multi-prefix, echo-message, away-notify, userhost-in-names, extended-join, batch, sasl, message-tags, draft/chathistory, draft/message-redaction, draft/event-playback, labeled-response

## Ports (defaults)

| Port | Protocol |
|------|----------|
| 6667 | IRC (TCP) |
| 6697 | IRC (TLS) |
| 6668 | Voice (UDP) |
| 6669 | WebSocket IRC |
| 443 | WebSocket IRC (TLS) |
| 6670 | Admin API + static files |

## Key Performance Patterns

- **Zero-alloc parsing**: IrcLine/MessageBuilder on Span<byte>, VoicePacket ref struct
- **Object pooling**: BufferPool, SocketEventArgsPool
- **Sync continuation loops**: TcpConnection processes all buffered data without context switches
- **ConcurrentDictionary**: Lock-free reads for sessions/channels/nicks
- **Per-session SemaphoreSlim**: Serializes commands per session, full concurrency across sessions
- **Background DB writes**: DbWriteQueue decouples persistence from request path
- **Flood protection**: Token bucket with per-command penalties, configurable burst/refill/disconnect thresholds
