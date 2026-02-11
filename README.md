# meatSpeak

A modern IRC server with an integrated voice SFU (Selective Forwarding Unit), built in C# on .NET 9. meatSpeak combines traditional IRC text communication with real-time voice channels, offering a self-hostable communication platform that speaks a protocol people already know.

## What is this?

meatSpeak is an IRC server that extends the familiar IRC protocol with voice support. Instead of bolting a proprietary protocol onto a chat server, it builds on top of RFC 1459 and adds voice as a first-class feature alongside text channels, direct messaging, and all the IRC commands you'd expect.

The voice system uses a Selective Forwarding Unit architecture — the server receives voice packets from participants and selectively forwards them to other members of the voice channel, rather than mixing audio server-side. This keeps latency low and allows features like end-to-end encryption, spatial audio, and per-stream priority without the server needing to decode anything.

## What we're trying to do

The goal is to build a communication server that:

- **Uses an open, well-understood protocol** — IRC has decades of client support. Any IRC client can connect for text chat. Voice-capable clients get the full experience.
- **Gives you ownership of your infrastructure** — no SaaS dependency, no telemetry, no account requirements beyond what you configure. Run it on your own hardware.
- **Treats voice as a peer to text** — voice channels live alongside text channels with the same permissions model, the same admin tools, and the same server.
- **Performs well without bloat** — zero-allocation protocol parsing, object pooling, async I/O throughout. Designed to handle real load on modest hardware.

This is currently an **initial scaffold**. The architecture and all major subsystems are in place, with many command handlers stubbed out and ready for implementation.

## Architecture

The server is split into focused projects:

| Project | Role |
|---|---|
| `MeatSpeak.Protocol` | Zero-alloc IRC line parser, message builder, voice packet format (13-byte header) |
| `MeatSpeak.Server.Core` | Interfaces, registries (commands, modes, capabilities), event bus |
| `MeatSpeak.Server.Transport` | TCP server, UDP listener/sender, line framing, socket/buffer pooling |
| `MeatSpeak.Server.Permissions` | Bitfield-based permission system — 16 server-level and 16 channel-level flags, role hierarchy |
| `MeatSpeak.Server.Data` | Entity Framework Core persistence — PostgreSQL and MariaDB support, repositories, audit logging |
| `MeatSpeak.Server.Voice` | SFU router, SSRC management, voice sessions/channels, transport encryption (libsodium), silence detection |
| `MeatSpeak.Server.AdminApi` | JSON-RPC 2.0 admin interface with API key auth, HTTP and WebSocket transports |
| `MeatSpeak.Server` | Main application — connection handler, registration pipeline, all IRC command handlers, server state |

## Current status

**Working:**
- TCP server with async connection handling
- IRC registration flow (NICK/USER/CAP negotiation)
- PING/PONG keepalive
- PRIVMSG and NOTICE
- Permission system with role hierarchy and per-channel overrides
- Voice infrastructure (UDP transport, SFU routing, SSRC management, encryption)
- Admin API framework (JSON-RPC 2.0, API key auth)
- Database layer (PostgreSQL/MariaDB, EF Core repositories, audit log)
- MOTD, LUSERS, VERSION, ISUPPORT
- 112 passing tests across all subsystems

**Stubbed / In Progress:**
- Channel operations (JOIN, PART, KICK, MODE, TOPIC, NAMES, LIST, INVITE)
- Voice join/leave flow and VOICE command
- SASL authentication
- Admin API method implementations
- WHO/WHOIS queries
- Operator commands (OPER, KILL, REHASH)

## Configuration

Default config lives at `src/MeatSpeak.Server/Config/server-config.json`:

- **IRC (TCP):** `0.0.0.0:6667`
- **Voice (UDP):** `0.0.0.0:6668`
- **Admin API:** port `6670`
- **Max connections:** 1024
- **Database:** PostgreSQL or MariaDB

## Building

```
dotnet build MeatSpeak.Server.sln
```

## Running tests

```
dotnet test MeatSpeak.Server.sln
```

## Tech stack

- C# 12 / .NET 9
- Entity Framework Core 9 (PostgreSQL via Npgsql, MariaDB via Pomelo)
- Sodium.Core (libsodium) for voice encryption
- Microsoft.Extensions.Hosting for DI and lifecycle
- xUnit for testing
