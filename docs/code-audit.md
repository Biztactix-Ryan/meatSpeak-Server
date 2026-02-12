# MeatSpeak IRC Server - Code Audit Report

Audit performed: 2026-02-12
Scope: Full codebase (192 source files, 74 test files across 8 projects)

---

## Executive Summary

67 issues identified: 7 Critical, 16 High, 30 Medium, 14 Low.

The most systemic problem is the **fire-and-forget `Task.Run` command dispatch** in `IrcConnectionHandler`, which creates pervasive data races across all session state. The second most impactful class of issues is **buffer size mismatches** between the protocol layer (4608 bytes max) and transport layer (4096 byte buffers).

**Estimated test coverage: ~61% of source files have tests, ~39% have none.**

---

## Category 1: Buffer Overflows & Memory Safety

### C3. MessageBuilder Writes Without Bounds Checks [CRITICAL]
**Files:** `MessageBuilder.cs:7-89`, `SessionImpl.cs:42-66`

`Write`, `WriteNumeric`, and `WriteWithTags` all write to a `stackalloc byte[4608]` span without validating accumulated data fits. Long NAMES lists with `userhost-in-names`, or messages with many tags, can overflow the buffer, crashing the handler task via `IndexOutOfRangeException`.

### C4. TcpConnection Send Buffer Overflow [CRITICAL]
**File:** `TcpConnection.cs:153`

Send pool uses `bufferSize: 4096` but IRC messages with tags can be 4608 bytes. `data.CopyTo(sendArgs.Buffer.AsSpan())` throws for any tagged message sent over plain TCP. Since most sessions negotiate `server-time`, this is a very common code path.

### C5. UdpSender Buffer Overflow [CRITICAL]
**File:** `UdpSender.cs:27`

Send pool has `bufferSize: 1500`. A crafted large UDP voice packet exceeding ~1487 bytes of payload crashes the SFU routing for all voice channel members.

### H7. TlsTcpConnection Undersized Receive Buffer [HIGH]
**File:** `TlsTcpConnection.cs:42`

Receive buffer is `BufferPool.Rent(4096)` but IRC lines can be 4608 bytes. TLS clients with large IRCv3 tags get disconnected. Plain TCP correctly uses `BufferPool.Rent(4610)`.

### H8. VoiceTokenManager Unhandled FormatException [HIGH]
**File:** `VoiceTokenManager.cs:30`

`Convert.FromBase64String(parts[3])` with unvalidated input throws `FormatException` uncaught. Channel names containing `:` also break the HMAC validation due to the token format using `:` as delimiter.

### H9. SsrcManager Counter Wrap-Around [HIGH]
**File:** `SsrcManager.cs:12`

`uint` SSRC counter wraps to 0 after `uint.MaxValue` increments. No collision detection. SSRC 0 may conflict with default/unset values.

### M9. IPv6 Hostname Extraction Broken [MEDIUM]
**File:** `SessionImpl.cs:24`

`connection.RemoteEndPoint?.ToString()?.Split(':')[0]` produces garbage for IPv6 addresses. Affects hostname-based bans and WHOIS.

### M10. ChatHistoryHandler KICK Replay Null Risk [MEDIUM]
**File:** `ChatHistoryHandler.cs:366`

KICK message replay assumes non-null `Message` from DB. Corrupt records cause `NullReferenceException`.

---

## Category 2: Race Conditions & Concurrency

### C1. Fire-and-Forget Handler Dispatch [CRITICAL]
**File:** `IrcConnectionHandler.cs:158`

Every command is dispatched via `_ = Task.Run(...)`. Multiple commands from the same client execute concurrently, creating data races on all mutable session state: `Nickname`, `Channels` (`HashSet`), `UserModes`, `MonitorList`, `CurrentLabel`, `LabeledMessageCount`, and `SessionState`.

**Fix:** Replace with per-session `Channel<T>` or `ActionBlock<T>` to serialize command processing per session. This single fix eliminates multiple downstream issues.

### C2. Nick Registration TOCTOU Race [CRITICAL]
**File:** `NickHandler.cs:45-55`

Two sessions can concurrently `NICK` the same name. Both check `FindSessionByNick`, both see it available, both set it. Creates ghost nicks and misrouted messages.

**Fix:** Use `ConcurrentDictionary.TryAdd` for atomic nick registration.

### H1. Non-Thread-Safe HashSets on SessionInfo [HIGH]
**File:** `SessionInfo.cs:16`

`HashSet<string> Channels`, `UserModes`, `MonitorList` are mutated by handlers and read during disconnect -- all potentially concurrent via Task.Run dispatch.

### H2. Unsynchronized Channel State [HIGH]
**File:** `ChannelImpl.cs:20`

`HashSet<char> Modes` and `Topic`/`Key`/`UserLimit` properties are read/written concurrently by different session handlers without synchronization.

### H3. SfuRouter Non-Thread-Safe Dictionary [HIGH]
**File:** `SfuRouter.cs:14`

Plain `Dictionary<string, VoiceChannel>` called from concurrent UDP receive threads. Can corrupt internal state.

### H4. TcpConnection Double Dispose Race [HIGH]
**File:** `TcpConnection.cs:182-200`

`_disposed` is plain `bool`, not atomic. Two concurrent calls to `Disconnect()` can both pass the check, causing double `OnDisconnected`. Same issue in H5 (`TlsTcpConnection.cs:139-156`).

### H6. Channel Join TOCTOU [HIGH]
**File:** `JoinHandler.cs:85-87`

`isNew` determined before `GetOrCreateChannel`. Two users joining the same new channel simultaneously both see `isNew=true`, both get operator status.

### M1. WebSocket No Write Serialization [MEDIUM]
**File:** `WebSocketConnection.cs:178`

No `_writeLock` unlike TLS path. Concurrent `SendAsync` calls can interleave frames.

### M2. Labeled-Response State Races [MEDIUM]
**File:** `IrcConnectionHandler.cs:164`

`CurrentLabel`/`LabeledMessageCount` are shared mutable state overwritten by concurrent labeled commands.

### M5. NickHandler Non-Atomic Membership Update [MEDIUM]
**File:** `NickHandler.cs:78-83`

`RemoveMember(oldNick)` then `AddMember(newNick)` is not atomic. User invisible to broadcasts between calls. Concurrent KICK can cause re-add under new nick.

### M8. DisconnectAsync `.Wait()` Deadlock Risk [MEDIUM]
**File:** `SessionImpl.cs:74`

`SendLineAsync().AsTask().Wait()` blocks synchronously. Can deadlock with TLS/WebSocket transports under thread pool saturation.

---

## Category 3: Injection & Authentication

### H10. PASS Command Never Validated [HIGH]
**Files:** `PassHandler.cs:30`, `RegistrationPipeline.cs:30-47`

`PASS` stores the server password but `RegistrationPipeline.TryCompleteRegistrationAsync()` never checks it. Server password is completely unimplemented.

### H11. Channel Key Exposed in MODE Query [HIGH]
**File:** `ModeHandler.cs:107-122`

Any registered user can query a channel's modes and the response includes the plaintext channel key, defeating `+k` protection. Should only be visible to channel operators.

### H12. Ban Matching Uses Exact String Comparison [HIGH]
**File:** `ChannelImpl.cs:73-76`

`IsBanned()` uses `string.Equals()` instead of IRC wildcard matching. Bans like `*!*@evil.host` never match against `nick!user@evil.host`. The ban system is functionally broken. Same issue affects `IsExcepted()`.

### H16. Voice Transport Encryption is a No-Op [HIGH]
**File:** `TransportEncryption.cs:6-14`

`Encrypt()` and `Decrypt()` return data unmodified. All voice traffic is plaintext.

### M11. Channel Mode +m Not Enforced [MEDIUM]
**File:** `PrivmsgHandler.cs:59-64`

PRIVMSG/NOTICE/TAGMSG check channel membership but never check `+m` (moderated). Non-voiced users can speak in moderated channels.

### M12. Username/Realname Zero Validation [MEDIUM]
**File:** `UserHandler.cs:37-38`

Unlike nicknames (validated in `IsValidNick()`), usernames and realnames accept spaces, control characters, null bytes, and arbitrary length.

### M13. Voice Token No Expiration Check [MEDIUM]
**File:** `VoiceTokenManager.cs:22-31`

Token includes a Unix timestamp but validation never checks expiration. Tokens are valid forever.

### M14. MONITOR Reveals Full Hostmask [MEDIUM]
**File:** `MonitorHandler.cs:75-78`

Any user can discover the full `nick!user@host` of any other user via MONITOR, bypassing future host cloaking.

---

## Category 4: Denial of Service & Resource Exhaustion

### C6. No Connection Limit or Per-IP Limit [CRITICAL]
**Files:** `TcpServer.cs`, `TlsTcpServer.cs`

`ServerConfig.MaxConnections` (default 1024) is defined but never checked. No per-IP limiting. Attacker can exhaust file descriptors.

### C7. No Registration Timeout [CRITICAL]
**File:** `PingTimeoutService.cs:57-89`

PINGs only sent to `Registered` sessions. Pre-registration connections survive for `PingTimeout` (180s) and can be kept alive indefinitely.

### H13. No Channel Limit Per User [HIGH]
**File:** `JoinHandler.cs:30-79`

No `CHANLIMIT` enforced. A user can join unlimited channels, creating unbounded memory growth via `GetOrCreateChannel()`.

### H14. Missing Flood Penalties on Expensive Commands [HIGH]
**Files:** `ListHandler.cs`, `NamesHandler.cs`, `WhoHandler.cs`, `WhoisHandler.cs`, `WhowasHandler.cs`, `ChatHistoryHandler.cs`, `AuthenticateHandler.cs`, `MotdHandler.cs`, `LusersHandler.cs`, `VersionHandler.cs`

These handlers have no `[FloodPenalty]` attribute (default cost 1) despite generating large responses or hitting the database.

### H15. No SASL Brute-Force Protection [HIGH]
**File:** `AuthenticateHandler.cs`

No `[FloodPenalty]`, no attempt counter, no lockout, no per-IP auth rate limiting. ~30 attempts/minute per connection.

### M18. Unbounded Channel Ban/Exception Lists [MEDIUM]
**File:** `ChannelImpl.cs:58-61`

No limit on `+b`/`+e` entries per channel. Standard IRC servers cap at ~60-100.

### M21. Unlimited Mode Changes Per Command [MEDIUM]
**File:** `ModeHandler.cs:139-181`

No limit on mode changes per MODE command (RFC 2812 specifies max 3). One command can add hundreds of bans.

### M23. CAP Handler FloodPenalty(0) [MEDIUM]
**File:** `CapHandler.cs:9`

CAP commands completely exempt from flood protection. Unlimited CAP LS/REQ/LIST during registration.

### M25. WHOWAS Dictionary Unbounded by Unique Nicks [MEDIUM]
**File:** `ServerState.cs:92-101`

Per-nick history capped at 100, but the dictionary of unique nicknames grows without limit.

---

## Category 5: Test Coverage Gaps

### Critical files with NO tests:
| File | Risk |
|------|------|
| `IrcConnectionHandler.cs` | Central command dispatch, session lifecycle, flood protection |
| `ServerState.cs` | Core session/channel/nick state management |
| `SessionImpl.cs` | Real `ISession` (all tests use mocks) |
| `ChannelImpl.cs` | Thread-safety, ban/invite management |
| `VoiceTokenManager.cs` | Security-critical HMAC token validation |
| `VoicePacketRouter.cs` / `SfuRouter.cs` | Voice packet routing |
| `DbWriteService.cs` | Background DB write processing (148 lines) |
| `RegistrationPipeline.cs` | Registration completion logic |
| `ServerMetrics.cs` | Metrics system (321 lines) |
| `InMemoryEventBus.cs` | Pub/sub event system |

### Files with insufficient coverage:
- `PrivmsgHandler` -- missing echo-message, `+m` enforcement, STATUSMSG tests
- `ModeHandler` -- missing concurrent multi-mode changes
- `CapHandler` -- missing `CAP NEW`/`CAP DEL`
- `ChannelMethods` (admin) -- missing `ChannelDeleteMethod` tests
- `AccountMethods` (admin) -- no tests at all

---

## Category 6: Code Duplication (DRY Violations)

### Top patterns to unify:

| Pattern | Occurrences | Est. Lines Saved |
|---------|------------|-----------------|
| `ERR_NEEDMOREPARAMS` validation | 15+ handlers | ~60 |
| Channel broadcast loop (find members, iterate, send) | 10+ locations | ~80 |
| PRIVMSG/NOTICE/TAGMSG near-identical handlers | 3 handlers | ~100 |
| Channel existence + membership check | 7 handlers | ~70 |
| Admin API `parameters == null` check | 20+ methods | ~60 |
| Channel operator permission check | 5 handlers | ~30 |
| ChatLogEntity construction | 7 locations | ~50 |
| Test setup / `CreateSession` helper | 6+ test files | ~60 |
| Deduplicated cross-channel broadcast | 4 locations | ~50 |
| Mode string `+/-` parsing | 4 locations | ~30 |

### Recommended utilities to create:
1. **`HandlerGuards`** -- RequireParams, RequireChannelMembership, RequireChannelOp, RequireNotRegistered
2. **`ChannelBroadcaster`** -- Single-channel and cross-channel deduplicated broadcasts
3. **`MessageDispatcher`** -- Unify PRIVMSG/NOTICE/TAGMSG routing
4. **`JsonParamHelper`** -- RequireParams, RequireString, RequireGuid for admin API
5. **`ChatLogHelper`** -- Unified ChatLogEntity construction
6. **`HandlerTestFixture`** -- Shared test setup and CreateSession

---

## Recommended Fix Priority

1. Serialize per-session command processing (fixes C1, H1, M2, M6)
2. Fix buffer size mismatches (fixes C3, C4, C5, H7)
3. Atomic nick registration (fixes C2)
4. Add connection limits and registration timeout (fixes C6, C7)
5. Fix ban matching to use wildcard/mask matching (fixes H12)
6. Hide channel key from non-operators in MODE query (fixes H11)
7. Add flood penalties to info-gathering commands (fixes H14, H15, M23)
8. Fix double-dispose races with Interlocked.CompareExchange (fixes H4, H5)
9. Implement PASS validation or remove the command (fixes H10)
10. Add CHANLIMIT enforcement (fixes H13)
