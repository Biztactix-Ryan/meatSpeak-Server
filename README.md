<p align="center">
  <img src="meatSpeakLogo.png" alt="meatSpeak logo" width="200">
</p>

<h1 align="center"><span style="color:#FF4444">meat</span>Speak</h1>

<p align="center"><strong>Free, open-source communication server — IRC with voice</strong></p>

---

meatSpeak is a self-hostable communication server that extends the IRC protocol with voice channels, modern permissions, and end-to-end encryption. It's a free, open alternative to Discord and Slack — built on IRCv3 so that hundreds of existing clients already work out of the box. Compiles to a single binary you can drop on any machine and run.

## Why meatSpeak?

Discord, Slack, and Teams use proprietary protocols, can't be self-hosted, and give you no ownership of your data. If the service shuts down or changes terms, you lose everything.

meatSpeak builds on **IRCv3** — an open standard with decades of client support. Any IRCv3 client can connect for text chat today. Voice-capable clients get the full experience with low-latency audio and encrypted channels.

**Your server, your data, your rules.** No SaaS dependency, no telemetry, no forced accounts, no ads. Run it on your own hardware or a $5/month VPS.

## Features

- **Full IRCv3 server** — 37 command handlers, CAP negotiation, SASL authentication, server-time, message-tags, echo-message, chat history, message redaction, and more
- **Voice chat** — SFU (Selective Forwarding Unit) architecture for low-latency audio without server-side mixing
- **E2E encryption** — XChaCha20-Poly1305 for voice traffic, designed for privacy from the ground up
- **Discord-style permissions** — Bitfield role system with hierarchical roles and per-channel overrides
- **Persistence** — SQLite (works out of the box), PostgreSQL, or MariaDB — chat history, audit logs, user accounts
- **Admin API** — JSON-RPC 2.0 with a web dashboard at `/admin`, secured with Argon2id-hashed API keys
- **TLS support** — IRC over TLS, WebSocket over TLS
- **WebSocket transport** — Browser-based clients via `ws://` and `wss://`
- **Performance** — Zero-allocation protocol parsing, object pooling, async I/O throughout — handles thousands of concurrent connections on modest hardware
- **994 tests** across 8 test projects

## Quick Start

Download a release binary or build one yourself:

```bash
# Build a self-contained single-file binary
dotnet publish src/MeatSpeak.Server -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

# The binary lands in src/MeatSpeak.Server/bin/Release/net9.0/linux-x64/publish/
```

Edit `src/MeatSpeak.Server/Config/server-config.json` if needed (defaults work fine), then run:

```bash
./MeatSpeak.Server
```

Connect with any IRC client pointed at `localhost:6667`.

## Building from Source

```bash
# Build everything
dotnet build MeatSpeak.Server.sln

# Run all tests
dotnet test MeatSpeak.Server.sln --verbosity minimal

# Publish single-file binary (replace linux-x64 with your platform)
dotnet publish src/MeatSpeak.Server -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

Other runtime identifiers: `linux-arm64`, `osx-x64`, `osx-arm64`, `win-x64`

## Configuration

Default ports:

| Port | Protocol |
|------|----------|
| 6667 | IRC (TCP) |
| 6697 | IRC (TLS) |
| 6668 | Voice (UDP) |
| 6669 | WebSocket IRC |
| 443  | WebSocket IRC (TLS) |
| 6670 | Admin API + web dashboard |

All configuration lives in `src/MeatSpeak.Server/Config/server-config.json`. The default SQLite database requires no setup — just run the binary.

Database options: **SQLite** (default, zero-config), **PostgreSQL**, **MariaDB**.

## Compatible Clients

Any IRCv3-compatible client works for text chat:

- [irssi](https://irssi.org/)
- [WeeChat](https://weechat.org/)
- [HexChat](https://hexchat.github.io/)
- [The Lounge](https://thelounge.chat/) (web-based)
- [Kiwi IRC](https://kiwiirc.com/) (web-based)
- [Revolution IRC](https://github.com/niccokunzmann/revolution-irc) (Android)

Voice requires a meatSpeak-aware client (coming soon).

## Roadmap

- **Server discovery** — directory for finding public meatSpeak servers (in progress)
- **IRCv3 voice extensions** — proposing voice protocol extensions to the IRCv3 specification
- **Native clients** — desktop and mobile apps with full voice support
- **Federation** — server-to-server linking

## Architecture

The server is split into focused projects:

| Project | Role |
|---------|------|
| `MeatSpeak.Protocol` | Zero-alloc IRC parser + message builder, voice packet format |
| `MeatSpeak.Server.Core` | Interfaces, registries, event bus, config, flood limiter |
| `MeatSpeak.Server.Transport` | TCP, TLS, WebSocket, and UDP transports with connection pooling |
| `MeatSpeak.Server.Permissions` | Bitfield permission system with role hierarchy and channel overrides |
| `MeatSpeak.Server.Data` | EF Core persistence, repositories, background write queue |
| `MeatSpeak.Server.Voice` | SFU voice router, SSRC management, encryption |
| `MeatSpeak.Server.AdminApi` | JSON-RPC 2.0 admin API with API key auth |
| `MeatSpeak.Server` | Main application — IRC handlers, server host, DI wiring |

For detailed architecture documentation, see [CLAUDE.md](CLAUDE.md).

## Contributing

Contributions are welcome! Check the [issues](../../issues) for open tasks or file a new one.

meatSpeak is licensed under the **GNU Affero General Public License v3.0** — any modifications to the server must be shared under the same terms, even when running as a network service.

## License

[AGPLv3](LICENSE) — see the [LICENSE](LICENSE) file for the full text.
