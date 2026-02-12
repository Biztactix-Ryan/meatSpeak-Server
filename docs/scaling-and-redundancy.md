# Scaling & Redundancy Plan

## Status: Proposal

This document outlines the planned approach for horizontal scaling and redundancy of MeatSpeak Server using Postgres-backed federation.

---

## Overview

The current server operates as a single instance with all state held in-memory. To support horizontal scaling and eliminate single points of failure, we propose a **full federation model** where multiple identical MeatSpeak server instances share coordination state through a single Postgres backend.

Each server is a full IRC server capable of handling client connections, processing commands, and managing local channel membership. Cross-server communication (message relay, nickname coordination, presence tracking) is handled via Postgres `LISTEN/NOTIFY` and shared tables.

### Design Principles

- **Postgres is the single source of truth** for shared state (nicknames, channel metadata, presence)
- **In-memory state remains local** for performance (connections, local channel membership, flood limiters)
- **No traditional netsplits** — a server that loses Postgres connectivity sheds its clients cleanly rather than operating independently
- **Existing command handlers remain unchanged** — federation is handled at the transport and event bus layers

---

## Architecture

```
                    ┌──────────┐
 [Clients] → [LB] →│ Server 1 │──→ [Postgres] ←── Server 3 ← [LB] ← [Clients]
                    └──────────┘         ↑
                                    Server 2
                                   ← [LB] ← [Clients]
```

- **Load balancer** distributes new client connections across servers (round-robin or least-connections)
- **Sticky sessions** ensure a client stays on the same server for the lifetime of its TCP connection
- **All servers connect to one shared Postgres instance** (or Postgres HA cluster)
- **Each server has a unique `server_id`** assigned at startup via configuration

---

## Shared State: New Postgres Tables

### `servers` — Server Registry & Health

```sql
CREATE TABLE servers (
    server_id   TEXT PRIMARY KEY,
    hostname    TEXT NOT NULL,
    listen_port INT NOT NULL,
    started_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen   TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Each server inserts a row on startup and updates `last_seen` on a heartbeat interval (e.g. every 5 seconds). Servers that miss heartbeats beyond a threshold (e.g. 15 seconds) are considered dead and their resources are cleaned up by surviving servers.

### `active_nicks` — Global Nickname Registry

```sql
CREATE TABLE active_nicks (
    nick        TEXT PRIMARY KEY,
    server_id   TEXT NOT NULL REFERENCES servers(server_id),
    session_id  TEXT NOT NULL,
    registered_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Nickname acquisition uses `INSERT ... ON CONFLICT DO NOTHING RETURNING nick` for atomic, race-free registration. A successful insert means the nick is yours; an empty result means someone else has it.

When a user disconnects or changes nick, their row is deleted. When a server is detected as dead, all its `active_nicks` rows are purged by a surviving server.

### `channel_presence` — Cross-Server Channel Membership

```sql
CREATE TABLE channel_presence (
    channel_name TEXT NOT NULL,
    server_id    TEXT NOT NULL REFERENCES servers(server_id),
    member_count INT NOT NULL DEFAULT 0,
    PRIMARY KEY (channel_name, server_id)
);
```

Tracks which servers have local members in which channels. Used to determine which servers to NOTIFY when a message is sent to a channel. Updated on JOIN/PART/QUIT.

A server with `member_count = 0` removes its row. When a server dies, its rows are purged.

---

## Cross-Server Communication: Postgres LISTEN/NOTIFY

Each server listens on a dedicated Postgres notification channel named after its `server_id`:

```sql
LISTEN "srv:server-1";
```

### Inbound Message Flow

When Server 1 receives a PRIVMSG to `#general` from a local client:

1. Deliver to all **local** members of `#general` directly (existing code path, no Postgres involved)
2. Query `channel_presence` to find other servers with members in `#general` (cacheable locally, invalidated on JOIN/PART notifications)
3. For each remote server, issue:
   ```sql
   NOTIFY "srv:server-2", '{"type":"PRIVMSG","channel":"#general","line":":alice PRIVMSG #general :hello"}';
   ```
4. Server 2 receives the notification, parses the IRC line, and delivers to its local members of `#general`

### Private Messages to Remote Users

When Server 1 receives a PRIVMSG to a user not found locally:

1. Look up the target nick in `active_nicks` to find which server owns it
2. NOTIFY that server directly with the message
3. If the nick doesn't exist in `active_nicks`, return `ERR_NOSUCHNICK` as usual

### Federated Events

The following events are broadcast via NOTIFY to relevant servers:

| Event | Payload | Notified Servers |
|---|---|---|
| `PRIVMSG` / `NOTICE` to channel | Raw IRC line | All servers in `channel_presence` for that channel |
| `PRIVMSG` / `NOTICE` to user | Raw IRC line | Server owning target nick |
| `JOIN` | Nick, channel, server_id | All servers in `channel_presence` for that channel |
| `PART` / `KICK` | Nick, channel, reason | All servers in `channel_presence` for that channel |
| `QUIT` | Nick, reason, channel list | All servers in `channel_presence` for affected channels |
| `NICK` change | Old nick, new nick | All servers in `channel_presence` for any shared channel |
| `TOPIC` | Channel, new topic, setter | All servers in `channel_presence` for that channel |
| `MODE` (channel) | Channel, mode string | All servers in `channel_presence` for that channel |
| `KILL` | Target nick, reason | Server owning target nick |

---

## Server Lifecycle

### Startup

1. Generate or load `server_id` from configuration
2. Insert row into `servers` table
3. Open dedicated Postgres connection for `LISTEN "srv:{server_id}"`
4. Start heartbeat background service (updates `last_seen` every 5s)
5. Start dead server reaper background service (checks for stale peers every 15s)
6. Begin accepting client connections

### Graceful Shutdown

1. Stop accepting new connections
2. Broadcast `QUIT` on behalf of all local clients to relevant servers via NOTIFY
3. Delete all local entries from `active_nicks` and `channel_presence`
4. Send `ERROR :Server shutting down` to all local clients (existing behavior)
5. Delete row from `servers` table
6. Close Postgres connections

### Ungraceful Failure (Server Dies)

1. Heartbeat stops updating `last_seen`
2. A surviving server's reaper service detects `last_seen` exceeds threshold
3. Reaper acquires `pg_advisory_lock` on the dead server's ID to prevent duplicate cleanup
4. Reaper deletes dead server's rows from `active_nicks` and `channel_presence`
5. Reaper notifies all servers: `{"type":"SERVER_DEAD","server_id":"srv-3"}`
6. Each server removes any cached references to the dead server's users
7. Clients from the dead server reconnect via load balancer to surviving servers

---

## Why This Avoids Netsplits

Traditional IRC netsplits occur because there is no central authority — when two servers lose their link, both halves continue operating independently and state diverges.

With Postgres as the arbiter, this cannot happen:

- A server that **loses Postgres connectivity** knows it is the disconnected party (Postgres is the authority, not a peer)
- That server stops accepting state mutations (no new JOINs, NICKs, channel creates)
- It disconnects its clients with `ERROR :Lost backend connectivity`
- Clients reconnect to a healthy server via the load balancer
- There is **no divergent state** because the disconnected server never operates independently

The failure mode is "server goes offline" (clean, recoverable) rather than "network splits into two conflicting halves" (messy, requires reconciliation).

---

## Load Balancer Configuration

### Requirements

- **TCP passthrough** (layer 4) — the LB does not need to understand IRC protocol
- **Sticky sessions** — once a client connects to a server, it stays there for the lifetime of the connection
- **Health checks** — TCP connect on the IRC port, or HTTP health endpoint on the WebSocket/admin port
- **TLS termination** — can be handled at the LB or at each server (server already supports TLS)

### Recommended Setup

HAProxy or Envoy with:
- Frontend: ports 6667 (plaintext), 6697 (TLS), 6669 (WebSocket)
- Backend: pool of MeatSpeak server instances
- Balance mode: `leastconn` (IRC connections are long-lived, so least-connections distributes better than round-robin)
- Health: TCP check every 5s with 2 failures to mark down

---

## Postgres High Availability

Since Postgres is the single shared dependency, it must be highly available:

- **Patroni + Streaming Replication** — automatic failover with a standby promoted to primary
- **PgBouncer** — connection pooling in front of Postgres (each MeatSpeak server needs a dedicated connection for LISTEN plus a pool for queries)
- **Monitoring** — track replication lag, connection count, NOTIFY queue depth

Note: `LISTEN/NOTIFY` only works on the **primary**. Read replicas cannot receive notifications. All MeatSpeak servers must connect to the primary for pub/sub. This is acceptable because the pub/sub load is lightweight (small payloads, moderate frequency).

---

## Implementation Phases

### Phase 1: Postgres Shared State

- Require Postgres as the database provider (remove SQLite/MySQL for multi-server deployments)
- Add `servers`, `active_nicks`, and `channel_presence` tables with migrations
- Add `server_id` to `ServerConfig`
- Implement heartbeat and dead server reaper background services

### Phase 2: Distributed Nickname Registry

- Modify NICK handler to acquire nicks via `active_nicks` table instead of (or in addition to) local `_nickIndex`
- Modify QUIT/disconnect to release nicks from `active_nicks`
- Handle nick collisions across servers (return `ERR_NICKNAMEINUSE` if nick exists in Postgres)

### Phase 3: Postgres Event Bus

- Implement `PostgresEventBus` as an alternative to `InMemoryEventBus` behind the existing `IEventBus` interface
- Dedicated Npgsql connection for `LISTEN` with async notification handler
- NOTIFY helper that publishes to target server channels
- Per-server channel presence cache with invalidation on remote JOIN/PART

### Phase 4: Cross-Server Message Routing

- Modify `PrivmsgHandler`, `NoticeHandler`, `TagmsgHandler` to check for remote recipients
- Channel messages: deliver locally + NOTIFY remote servers with members
- Private messages: look up `active_nicks` for target server, NOTIFY directly
- Handle `JOIN`, `PART`, `QUIT`, `NICK`, `TOPIC`, `MODE`, `KICK` federation

### Phase 5: Failure Handling

- Detect Postgres connectivity loss and enter degraded mode (reject new state changes, optionally disconnect clients)
- Dead server cleanup with advisory locking to prevent races
- Graceful shutdown sequence (clean up shared state before exiting)
- Client reconnection support (clients reconnect to a new server via LB and re-register)

---

## Performance Considerations

- **Postgres NOTIFY payload limit:** 8,000 bytes. IRC messages max at 512 bytes (8,191 with message-tags), well within limits.
- **NOTIFY throughput:** Postgres comfortably handles thousands of notifications per second. For very high traffic, batch multiple target deliveries into fewer NOTIFYs.
- **Channel presence cache:** Each server caches `channel_presence` locally and invalidates on JOIN/PART events to avoid per-message queries.
- **Nick lookups for private messages:** Cache `active_nicks` with a short TTL or invalidate on NICK/QUIT events.
- **Postgres connection count:** Each server needs 1 dedicated LISTEN connection + a connection pool for queries/writes. Use PgBouncer to manage total connections.

---

## Capacity Estimates

| Metric | Single Server (Current) | Federated (4 Servers) |
|---|---|---|
| Concurrent users | ~5,000-10,000 | ~20,000-40,000 |
| Channels | Unlimited (memory-bound) | Unlimited (shared across servers) |
| Messages/sec throughput | Limited by single CPU | Distributed across servers |
| Redundancy | None | Lose any 3 of 4 servers |
| Postgres NOTIFY load | N/A | ~10,000-50,000/sec (within limits) |

Actual capacity depends on hardware, message rates, and channel sizes. These are rough estimates for planning purposes.
