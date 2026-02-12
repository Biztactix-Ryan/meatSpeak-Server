# IRCv3 Specification Support

This document tracks meatSpeak's implementation status against the [IRCv3 specifications](https://github.com/ircv3/ircv3-specifications).

**Legend:** Supported | Partial | Not yet implemented | N/A (deprecated or not applicable)

---

## Ratified Specifications

These specs are finalized and stable.

### Core Framework

| Specification | Status | Notes |
|--------------|--------|-------|
| [Client Capability Negotiation](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/capability-negotiation.md) | Supported | CAP LS, REQ, END, LIST. CAP 302 version parameter not yet supported. |
| [Message Tags](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/message-tags.md) | Supported | Full tag parsing/serialization, client-only tag relay (`+` prefix), 4096-byte tag limit enforced. |
| [Standard Replies](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/standard-replies.md) | Partial | `FAIL` verb used for REDACT and CHATHISTORY errors. `WARN` and `NOTE` not yet used. |

### Authentication

| Specification | Status | Notes |
|--------------|--------|-------|
| [SASL 3.1](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/sasl-3.1.md) | Supported | PLAIN mechanism with Argon2id password verification. |
| [SASL 3.2](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/sasl-3.2.md) | Partial | Mechanism list advertised in CAP value (`sasl=PLAIN`). Reauthentication via `cap-notify` not yet supported. |

### Account Tracking

| Specification | Status | Notes |
|--------------|--------|-------|
| [account-notify](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/account-notify.md) | Not yet implemented | |
| [account-tag](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/account-tag.md) | Not yet implemented | |
| [extended-join](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/extended-join.md) | Supported | JOIN messages include account name and realname for clients with this cap. |

### User Notifications and Status

| Specification | Status | Notes |
|--------------|--------|-------|
| [away-notify](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/away-notify.md) | Supported | AWAY broadcast to shared channel members with de-duplication. |
| [Bot Mode](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/bot-mode.md) | Not yet implemented | |
| [chghost](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/chghost.md) | Not yet implemented | |
| [invite-notify](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/invite-notify.md) | Not yet implemented | |
| [setname](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/setname.md) | Not yet implemented | |

### Messaging and Batching

| Specification | Status | Notes |
|--------------|--------|-------|
| [batch](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/batch.md) | Supported | Used for chathistory and chathistory-targets batch types. |
| [echo-message](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/echo-message.md) | Supported | Echoes PRIVMSG, NOTICE, and TAGMSG back to sender with full tag support. |
| [Labeled Responses](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/labeled-response.md) | Supported | Label tracking per command, ACK for zero-response commands. |
| [Message IDs](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/message-ids.md) | Supported | ULID-based `msgid` tags on all messages. |

### User Listing and Information

| Specification | Status | Notes |
|--------------|--------|-------|
| [multi-prefix](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/multi-prefix.md) | Supported | NAMES replies include all prefix characters (`@+`) instead of just the highest. |
| [userhost-in-names](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/userhost-in-names.md) | Supported | NAMES replies include full `nick!user@host` form. |
| [WHOX](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/whox.md) | Not yet implemented | |
| [Monitor](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/monitor.md) | Supported | Full MONITOR +/-/C/L/S with max 100 targets. Online/offline notifications on registration, nick change, and disconnect. |
| [extended-monitor](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/extended-monitor.md) | Not yet implemented | |

### Server and Network Features

| Specification | Status | Notes |
|--------------|--------|-------|
| [server-time](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/server-time.md) | Supported | ISO 8601 `time` tag on all messages and events. |
| [UTF8ONLY](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/utf8-only.md) | Not yet implemented | |

### Security and Transport

| Specification | Status | Notes |
|--------------|--------|-------|
| [Strict Transport Security (STS)](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/sts.md) | Not yet implemented | TLS is supported on port 6697, but STS policy advertisement is not. |
| [WebIRC](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/webirc.md) | Not yet implemented | |

### Batch Types

| Specification | Status | Notes |
|--------------|--------|-------|
| [chathistory batch type](https://github.com/ircv3/ircv3-specifications/blob/master/batches/chathistory.md) | Supported | Used for message replay in CHATHISTORY responses. |
| [netsplit/netjoin batch types](https://github.com/ircv3/ircv3-specifications/blob/master/batches/netsplit.md) | Not yet implemented | Single-server architecture; relevant when federation is added. |

### Client-Only Tags

| Specification | Status | Notes |
|--------------|--------|-------|
| [typing](https://github.com/ircv3/ircv3-specifications/blob/master/client-tags/typing.md) | Supported | Relayed transparently to clients with `message-tags`. |

---

## Draft Specifications

These specs are work-in-progress. Capability names use the `draft/` prefix.

### Account and Authentication

| Specification | Status | Notes |
|--------------|--------|-------|
| [account-registration](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/account-registration.md) | Not yet implemented | Accounts are created via the Admin API. |
| [Account Extended Ban](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/account-extban.md) | Not yet implemented | |

### Channel Management

| Specification | Status | Notes |
|--------------|--------|-------|
| [Channel Renaming](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/channel-rename.md) | Not yet implemented | |

### History and Persistence

| Specification | Status | Notes |
|--------------|--------|-------|
| [chathistory](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/chathistory.md) | Supported | All 6 subcommands: LATEST, BEFORE, AFTER, AROUND, BETWEEN, TARGETS. Max 100 messages per request. Supports `timestamp` and `msgid` reference types. |
| [Message Redaction](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/message-redaction.md) | Supported | REDACT command with permission model: own messages or channel operator. Persisted to database. |
| [Read Marker](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/read-marker.md) | Not yet implemented | |
| [Event Playback](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/chathistory.md) | Supported | JOIN, PART, QUIT, TOPIC, KICK events replayed in chathistory batches for clients with `draft/event-playback`. |

### Messaging

| Specification | Status | Notes |
|--------------|--------|-------|
| [Client-Initiated Batch](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/client-batch.md) | Not yet implemented | |
| [Multiline Messages](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/multiline.md) | Not yet implemented | |

### Server and Network Features

| Specification | Status | Notes |
|--------------|--------|-------|
| [extended-isupport](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/extended-isupport.md) | Not yet implemented | |
| [Metadata](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/metadata.md) | Not yet implemented | |
| [Network Icon](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/network-icon.md) | Not yet implemented | |
| [no-implicit-names](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/no-implicit-names.md) | Not yet implemented | |

### Bouncer and Multi-Client Support

| Specification | Status | Notes |
|--------------|--------|-------|
| [pre-away](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/pre-away.md) | Not yet implemented | |

### Transport

| Specification | Status | Notes |
|--------------|--------|-------|
| [WebSocket](https://github.com/ircv3/ircv3-specifications/blob/master/extensions/websocket.md) | Supported | `irc` subprotocol, available on ports 6669 (ws) and 443 (wss). |

### Client-Only Tags (Draft)

| Specification | Status | Notes |
|--------------|--------|-------|
| [channel-context](https://github.com/ircv3/ircv3-specifications/blob/master/client-tags/channel-context.md) | Supported | Relayed transparently to clients with `message-tags`. |
| [react](https://github.com/ircv3/ircv3-specifications/blob/master/client-tags/react.md) | Supported | Relayed transparently to clients with `message-tags`. |
| [reply](https://github.com/ircv3/ircv3-specifications/blob/master/client-tags/reply.md) | Supported | Relayed transparently to clients with `message-tags`. |

---

## Deprecated Specifications

| Specification | Status | Notes |
|--------------|--------|-------|
| [STARTTLS](https://github.com/ircv3/ircv3-specifications/blob/master/deprecated/tls.md) | N/A | Deprecated in favor of direct TLS connections. meatSpeak supports TLS on port 6697. |
| [SASL DH-BLOWFISH](https://github.com/ircv3/ircv3-specifications/blob/master/deprecated/sasl-dh-blowfish.md) | N/A | Deprecated as insecure. |
| [SASL DH-AES](https://github.com/ircv3/ircv3-specifications/blob/master/deprecated/sasl-dh-aes.md) | N/A | Deprecated as insecure. |

---

## Summary

| Category | Supported | Partial | Not Yet | N/A | Total |
|----------|-----------|---------|---------|-----|-------|
| Ratified | 15 | 2 | 9 | 0 | 26 |
| Draft | 7 | 0 | 10 | 0 | 17 |
| Deprecated | 0 | 0 | 0 | 3 | 3 |
| **Total** | **22** | **2** | **19** | **3** | **46** |

## meatSpeak Extensions

meatSpeak extends IRC with features not covered by the IRCv3 specifications:

- **Voice channels** — SFU-based voice chat over UDP with XChaCha20-Poly1305 encryption
- **Bitfield permissions** — Discord-style role hierarchy with per-channel overrides
- **Admin API** — JSON-RPC 2.0 management interface

We intend to propose the voice protocol extensions to the IRCv3 working group for standardization.
