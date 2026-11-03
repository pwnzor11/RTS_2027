# ageServer (luskaner): LAN and Offline Lobby Emulation for Age of Empires DE

**Summary**

- ageServer is a Go program that impersonates the online services of Microsoft/Relic's Age of Empires games (lobbies, matchmaking, chat, leaderboard stubs, cloud files) so that the stock game can host and join LAN or offline lobbies. It is a substitute lobby server, not a netcode library.
- The game's own battle server (`BattleServer.exe`, EA-owned, not in this repo) carries the match traffic. The ageServer lobby hands out that battle server's address and ports. In practice the match is a star: every client talks to one relay on the host PC, not to each other.
- Discovery is a small UDP exchange on port 31978 (multicast 239.31.97.8 plus per-interface broadcast). The reply carries the server's UUID; the client then confirms it over HTTPS on port 443 using the `/test` endpoint, which returns version headers.
- Lobby state lives in memory on the server. The host is a stored user ID, changed only when the first peer in the lobby explicitly claims it. There is no automatic host election.
- Most of the hard work is not the protocol but the client-side plumbing: editing the hosts file, installing a self-signed root certificate, and reverting both on exit. This is the part we should avoid entirely.
- Licence is AGPL v3. Our GPL v3 project should not copy code from it. Protocol and design ideas are free to reuse; the only reusable artifacts are the discovery pattern and a few constants, which we can re-derive.

## 1. What it is and why it exists

Age of Empires II, III, IV and Mythology (and AoE I) are built so that multiplayer lobbies go through Microsoft/Relic's servers. When those servers are down, or when the PCs are not online, the lobby fails. ageServer replaces the lobby service with a copy running on a LAN PC, so players on the same network can still create and join games. The README says the server "reimplements the minimum required API surface to allow the game to work with lobbies" and proxies a few requests to the official CDN and API hosts (`README.md`, "How it works"; `server/README.md`, "Partially proxied": `api.ageofempires.com` and `cdn.ageofempires.com`, except the parts that must stay local).

Supported titles (README): AoE: DE, AoE II: DE, AoE III: DE, AoE IV: Anniversary Edition, and Age of Mythology: Retold (Steam only, per the README). The code keys games as `age1`, `age2`, `age3`, `age4` and `athens` (`common/game/game.go:10-17`).

Status in 2026: the repository is active. The most recent commit in the local clone is dated 2 November 2026 (commit `aff2476`), the README describes release archives for Windows 7 through 11, Linux and macOS, and the build uses Go 1.27 (`DEVELOPMENT.md`). The README also says the tool does not get you fully offline play by itself: a custom launcher or a Steam emulator (in a separate project by the same author) is needed to bypass the online-only checks. We should treat that as outside the scope of anything we would copy.

Licence: the `LICENSE` file is the full AGPL v3 text (`LICENSE:1-3`). The README then adds "Additional Terms of Use for the Downloadable Package" (personal use only, you must own the game). Those terms are not part of the AGPL and the README says they do not restrict AGPL rights. We should not adopt them, and we should not treat them as a model.

## 2. Architecture

The system has five moving parts. Only the first three matter for our design.

| Component | Path | Role |
|---|---|---|
| Server | `server/` | Go HTTPS server on port 443 per local address. Emulates the lobby, login, chat, and stub endpoints. Holds all lobby state in memory. |
| Launcher | `launcher/`, `launcher-common/` | Starts or finds a server, writes hosts entries, installs the certificate, starts the game, and reverts everything on exit. |
| Launcher agent | `launcher-agent/` | Detached process that outlives the launcher, waits for the game to exit, then reverts configuration. Shows a tray icon. |
| Battle server (external) | runs `BattleServer.exe` from the game install, or the embedded `github.com/luskaner/ageBattleServer` module | Carries match traffic over TCP and WebSocket ports. |
| Battle server manager | `battle-server-manager/` | Starts and stops battle servers and writes their config to a temp folder. |
| Battle-server broadcast | `battle-server-broadcast/` | Windows-only helper that copies the battle server's UDP presence packets from the primary NIC to other NICs. |

The flow for a player who wants to host:

1. The launcher probes the LAN for an existing server (section 3). If none answers, it starts one (`server/internal/cmd/root.go`, around lines 405-445, which creates one `http.Server` per local address and calls `ListenAndServeTLS` on `:443`).
2. The launcher writes hosts-file lines pointing the game's API domains at the server IP. Those domains are listed in `common/domain.go:34` (`*.worldsedgelink.com`, `aoe-api.reliclink.com`, `*.ageofempires.com`). It installs the server's self-signed certificate so the game's TLS stack accepts it (`launcher-common/cert/cert_windows.go`, `trustCertificatesInStore`, writing to the `ROOT` store).
3. The game calls `/game/advertisement/host`. The server creates an advertisement (lobby) and returns the battle server's address and ports (`server/internal/routes/game/advertisement/host.go:31-62`, `encodeHostResponse`). The battle server address is built by `EncodeAdvertisement` and `ResolveIPv4` (`server/internal/models/battleServer.go:254-`), which use the IP the request arrived on (`localIp(r)`), so the client gets an address it can reach.
4. Other players call `/join`. The server checks compatibility (section 4) and adds them as a peer (`server/internal/routes/game/advertisement/join.go:36-175`). The response carries the host's IP and the battle server's ports.
5. The game connects to the battle server's TCP `bsPort` (and WebSocket port). From there, match traffic flows through that process.

### What the battle server does

The server README and `BattleServers.md` describe the battle server as the component that the game connects to after the lobby. The ports are `bsPort` (TCP), `webSocketPort` (WebSocket) and `outOfBandPort` (TCP, observers), and `relayBroadcastPort` (UDP, presence announcements). The code gives the simulation tick per game (`common/battleconfig/config.go:19-25`): 25 ms for AoE I, 50 ms for AoE III and AoM, 125 ms for AoE II and AoE IV.

The relay logic itself is in the external module `github.com/luskaner/ageBattleServer` (`server/go.mod:11`), which is not in this repository. From this repo we can confirm the topology from the data flow: the lobby returns one battle server address per match, and every player connects to it. We can say the traffic is relayed through the battle server, not peer-to-peer. How the battle server orders and forwards commands is **unverified** from this repo.

Two consequences for us:

- Clients never need to reach each other directly. NAT and firewall problems on a single client are reduced to "can reach the relay".
- The battle server is a separate process with its own port set. This is why the server ships a manager that starts battle servers and writes TOML config files for them.

### Lobby, chat and content

- **Lobby.** Each lobby is an `advertisement` (`server/internal/models/advertisement.go`). It holds the host user ID, map name, description, password, state, observers, an ordered peer map, and tags. Fields are only changed under per-advertisement locks (`i.KeyRWMutex`, `WithReadLock`/`WithWriteLock`). The lobby list is a `SafeOrderedMap` in memory. No database is involved for lobbies. A restart drops all lobbies.
- **Joining.** Peers are added with `UnsafeNewPeer`. Leaving removes the peer; when the last peer leaves, the advertisement is deleted (`advertisement.go:457-469`, `UnsafeRemovePeer`; `server/internal/routes/game/advertisement/leave.go:11-37`).
- **Chat.** Routes are under `/game/chat/...` (channels, whispers, offline messages) and `/game/party/sendMatchChat` (`server/internal/routes/game/chat/`, `router/game.go:218-270`). Chat is a plain request/response API, not a socket push, plus a WebSocket channel in `server/internal/routes/wss/wss.go` used for server-to-client messages.
- **Map and save transfer.** The `/api/MatchContent/` endpoints let the host upload a file (a multiplayer save or a map used as custom content) and the other players download it (`server/README.md`, "Custom files"; `server/internal/routes/matchcontent/upload.go`, `download.go`). Files are stored under the temp folder with a per-game quota, least-recently-used eviction and a TTL. A `.part` file is used during upload. Normal request bodies are capped at 1 MB (`server/internal/cmd/root.go`, `MaxBodySizeMiddleware(1<<20, ...)`).
- **Mods and data.** The join path checks the app binary checksum, the data checksum, the mod DLL file and checksum, mod name and version, and version flags (`join.go` in the reject block). Mismatches are refused.

## 3. LAN discovery and lobby design

This is the part most useful to us.

**Discovery.** The launcher probes for servers over UDP:

- The probe payload is the ASCII string `ageServer` (`common/announce.go:3-5`: `AnnouncePort = 31978`, `AnnounceMulticastGroup = "239.31.97.8"`, `AnnounceHeader = Name`, and `Name = "ageServer"` in `common/common.go:3`).
- The launcher sends it to the multicast group and to each interface's directed broadcast address, from one socket per local IP. The broadcast address is computed as `ip | ^mask` (`launcher-common/launcher/server/server.go`, `calculateBroadcastIPv4`). It repeats a few rounds with a one-second read timeout, so a silent network costs a few seconds.
- The server joins the multicast group on every multicast-capable interface and answers each probe with `ageServer` followed by its own UUID (`server/internal/ip/announce.go:17-88`, `QueryConnections` and `ListenQueryConnections`). The UUID is generated at startup (`server/internal/runtime.go:10`).

**Verification.** A UDP reply only says "something answered". The launcher then calls `GET https://<ip>/test` on port 443 (`common/server/server.go:26`, `serverPort = "443"`; `LanServerIP` at lines 83-), reads the `X-Id` and `X-Version` headers and checks the ID against the UDP reply. The version is `AnnounceVersion2` (`common/announce.go`), which means the data about supported game titles is fetched over HTTPS rather than broadcast. The launcher then does three HEAD requests to measure latency (`LatencyMeasurementCount = 3`, `common/server/server.go:22`). The server's `/test` handler is `server/internal/routes/test/test.go`.

**Why this is a good pattern.** The design separates three questions: "is there a server?" (cheap UDP), "is it the right one and is it speaking the right version?" (HTTPS with a version header), and "is it fast enough?" (latency probes). The UDP part never carries any game state, so a stray or malicious packet cannot change anything.

**Lobby state and host.**

- The host is `hostId` on the advertisement. It is set when the host creates the lobby (`host.go`, from the request's `HostId`).
- Ownership moves only through `UpdateHost` (`server/internal/routes/game/party/updateHost.go:14-42`). The rule: the caller must be the *first peer* in the ordered peer map, and the current host must differ. The first peer can take over the host role. No one else can.
- When the host leaves, `UnsafeRemovePeer` does not change `hostId`. The lobby keeps the old host ID until the new first peer claims it. The code does not elect a new host automatically. Whether the game client reacts to this is **unverified** from this repo.
- Lobby removal happens when the last peer leaves, not when the host leaves.

**Battle server discovery (the other discovery).** The stock game also has its own LAN discovery for battle servers. `BattleServer.exe` can send UDP presence packets to port 9999 (AoE II and later) or 8888 (AoE I). The packet format is in `common/battleServer/broadcast.go:22-50`: a 3-byte header `0x21 0x24 0x00`, a 36-byte GUID, a 2-byte name length and name, then three 2-byte ports (game, websocket, out-of-band). ageServer starts battle servers with `-relayBroadcastPort 0` (`common/battleconfig/config.go:297`) so that the game does not find them directly and the lobby hands out the address instead. On Windows, `battle-server-broadcast/battleServer.go` copies packets from the primary NIC to the broadcast address of each other NIC (`RetrieveBsInterfaceAddresses` line 39, `CloneAnnouncements` line 103, `cloneAnnouncementsLoop` line 166). It only accepts packets whose source IP is the primary NIC's address (line 166, the `addr.IP.Equal(mostPriority.IP)` check). The whole layer exists because the game's broadcast went out on one NIC only.

## 4. Security and trust model

The trust model is the least attractive part of the project and the part we should not imitate.

- **Certificate.** Self-signed certificates are generated for the EA domains (`SelfSignedCertDomains`, `common/domain.go:34`). The certificate is installed into the Windows `ROOT` store by the launcher (`cert_windows.go`, `trustCertificatesInStore`), which makes it trusted for every program on the machine, not only the game. The private key sits in `server/resources/certificates/`. Anyone holding that key can impersonate those domains on that machine. The launcher reverts the change on exit through the agent.
- **Hosts file.** The launcher writes lines into the system hosts file with a marker (`common/hosts/line.go:12`, `const marking = common.Name`) so it can remove them later. It also keeps a backup (`hosts.bak`, `common/hosts/hosts.go`). Crashes that leave entries behind are a known support problem, which is why the repo carries `bin/config revert` and a revert-args store.
- **Authentication.** The server has a login and session layer (`server/internal/routes/router/authMiddleware.go`, 293 lines) with per-user sessions, but the lobby is effectively open to anyone who reaches the server. A password can be set per lobby (`join.go`, password check).
- **Outside traffic.** Some requests go to the real Microsoft and EA hosts (`api.ageofempires.com`, the CDN, and a public-IP lookup against `api.ipify.org` with a 3-second timeout; `server/internal/models/battleServer.go`, `externalIPTimeout`). These are optional and bounded.
- **Ports.** The server listens on 443, which needs admin rights on many systems (`server/README.md`, "Minimum system requirements"). Battle servers use high ports (30001-30014 in the examples in `server/BattleServers.md`).

Lessons: a LAN game needs none of the certificate or hosts-file work if we control the client. Those steps exist only because the stock client insists on talking to EA domains over TLS.

## 5. What this teaches us for RTS_2027's LAN mode

The priority for LAN is 3 (optional). The research below is about the *minimal* design that would work for a home network with 2 to 4 players.

**What ageServer shows that is worth keeping**

- A UDP discovery probe with a fixed port, a multicast group and per-interface subnet broadcast. Keep the probe payload tiny and include an identifier of the game and the protocol version in the reply.
- A two-step discovery: UDP to find candidates, then a request that carries version and identity over the reliable channel. This stops mismatched clients from joining.
- A per-server UUID, generated at startup, to tell two servers apart.
- Lobby state held in memory on one machine, with a clear rule for who may change the host.
- Compatibility checks on join: game build, data checksum, mod name and version. Refuse early and say why.

**What to avoid**

- Hosts-file edits, root certificates and any kind of traffic interception. None of this is needed for a game we ship.
- A separate battle-server process, a battle-server manager, and per-NIC rebroadcast. These solve EA's architecture, not ours. Our simulation can live in the same process as the lobby.
- Port 443 and admin rights. Use an unprivileged port above 1024.
- Assuming broadcast works. Many home routers and some Wi-Fi access points block broadcast or multicast between clients. Offer a manual "join by IP" field as a fallback.

**Recommended minimal LAN design**

- **Topology:** the host runs the lobby and the match. Clients connect to the host over TCP for lobby and commands. The match is a star with the host as relay, or a full mesh if you prefer to keep the C&C-style peer model. For 2 to 4 players, the star is simpler to debug and avoids N-squared connections. Lockstep commands are small, so the host's relay cost is low.
- **Discovery:** UDP probe to the multicast group and to each interface's directed broadcast, on one fixed port above 1024. Reply with a game ID, a protocol version and the host's lobby name and player count. Confirm with a TCP handshake before listing.
- **Join validation:** protocol version, game build, and data checksum (the same as the map and rules files). Refuse with a reason string.
- **Host migration:** skip for version 1. If wanted later, copy the ageServer rule: only the first remaining player may claim the host role, no automatic election.
- **Maps and saves:** ship maps and rules with the game and transfer only a map ID and hash. Send a save only when a match is restored; the host keeps it in memory with a TTL.
- **Ports and firewall:** one TCP port and one UDP port, documented, with a Windows firewall rule written by the installer.
- **NAT:** not needed on a LAN. Do not add UPnP or relay servers in version 1.

**Contrast with classic C&C LAN.** The classic games had no lobby server at all. The EA code study (`docs/research/03_cnc_networking_determinism.md`) describes a peer-to-peer lockstep model with a per-frame `FRAMEINFO` heartbeat and a rolling checksum for desync detection. The transport classes are `IPXConnClass` and modem/null-modem connections (`TIBERIANDAWN/CONNECT.H:119-122`) and `IPXManagerClass` (`TIBERIANDAWN/IPXMGR.H:151`). IPX broadcast was the LAN discovery mechanism of that era. ageServer is the reverse of that design: a central lobby with a relayed match. Our choice is between these two shapes, and the rest of the design follows from it.

## 6. Reusability and licence

**Licence fact.** ageServer is AGPL v3 (`LICENSE`). RTS_2027 is GPL v3. Copying AGPL code into our project would make that code AGPL-governed, and a combined work would have to be distributed under AGPL terms. GPL v3 section 13 allows combining GPL and AGPL works, but the result is then AGPL. The AGPL network clause would also apply to any part of our game that users interact with over a network. That is more than we want for a desktop game. Recommendation: do not copy code.

**Reusable without copying (ideas, facts, constants)**

- The discovery pattern: fixed port, multicast group, per-interface broadcast computed as `ip | ^mask`. This is a standard networking technique and not copyrightable.
- The idea of a two-stage check (UDP candidate, then versioned HTTP or TCP confirmation).
- The lobby-host rule from `UpdateHost`: only the first peer can claim the host role.
- The join compatibility list (build, data checksum, mod, flags).
- Simulation period values per game (25, 50, 125 ms). These are facts about the EA games, not code, and they are not needed for our game.

**Not worth porting**

- The HTTP API surface, the cloud and PlayFab stubs, the battle-server manager, the Windows NIC rebroadcast, and the certificate and hosts machinery. All of this is specific to EA's services and would be a maintenance burden.

**Unverified items**

- How the external `ageBattleServer` module orders and forwards commands.
- Whether the stock client reacts to a stale host ID after the host leaves.
- Whether the hosts-file revert is fully reliable after a crash (the repo's own README points to a manual revert command).

## Implications for RTS_2027

- **Recommendation:** Build LAN last, after single-player skirmish and the campaign are stable. Its only hard requirement from the sim side is determinism, which the design must already satisfy for replays.
- **Recommendation:** Use a lobby-on-host model: the first player to open a game owns the lobby state in memory. No lobby server, no accounts, no internet dependency.
- **Recommendation:** For 2 to 4 players, use a star topology through the host for match traffic, so clients never open connections to each other. The alternative (full mesh, as in classic C&C) avoids host load but doubles connection handling. Choose after measuring lockstep bandwidth.
- Discovery should be a single fixed UDP port above 1024, probed to multicast and to each interface's directed broadcast, with a versioned TCP confirmation. Provide a "join by IP" field because broadcast is often blocked.
- Host migration is not needed for version 1. Document the rule now so the protocol leaves room for it: the lobby stores a host ID, and only the first remaining player may claim it.
- Compatibility checks on join (game build, rules checksum, map hash) prevent the most common LAN failure, which is players on different builds. Refuse with a readable reason.
- Do not copy any part of ageServer. Treat it as a reference for shape and failure modes only.
- Do not reproduce hosts-file edits or certificate installation. A game we ship has no need to impersonate third-party domains. Any "trust" we need can be a pre-shared key in the lobby or simply none in version 1.
- Keep the lobby and the simulation in one process, unlike ageServer's separate battle-server process, unless measurements show the host cannot run both.
- The EA design (sim runs peer-to-peer, no server) fits our deterministic-lockstep plan from file 03. A star relay is a small change to that plan, not a rewrite.

## Open questions for the owner

1. **Topology for LAN.** Options: (a) classic peer-to-peer lockstep with a mesh of connections, as in C&C; (b) host-relayed star, so clients talk only to the host; (c) decide after a prototype measures bandwidth. Recommendation is (b) for simplicity.
2. **Host migration in version 1.** Options: (a) none, and a host leaving ends the match; (b) first remaining player may claim the host role manually; (c) automatic election. Recommendation is (a), with the protocol ready for (b).
3. **Discovery fallback.** Options: (a) broadcast and multicast only; (b) broadcast plus a manual "join by IP" field; (c) a saved list of favourite hosts. Recommendation is (b).
4. **Number of players and AI in LAN lobbies.** Options: (a) 2 humans only; (b) 2 to 4 humans; (c) humans plus skirmish AI slots in the same lobby. Option (c) depends on the AI plan in file 14.
5. **Is LAN in scope for the first public release at all?** Options: (a) no, LAN after the campaign ships; (b) LAN as a beta; (c) LAN only if the owner plays it. The priority list calls LAN optional, so this is a scope decision.

## Sources

Local repository read in full at `/home/claude/ref/ageServer` (read-only, AGPL v3). Line ranges are from that clone at commit `aff2476`.

- `/home/claude/ref/ageServer/README.md`: overview, supported games, features, licence terms.
- `/home/claude/ref/ageServer/DEVELOPMENT.md`: build and release process, Go 1.27.
- `/home/claude/ref/ageServer/LICENSE:1-3`: AGPL v3 header.
- `/home/claude/ref/ageServer/server/README.md`: domains, partial proxying, custom files, certificates.
- `/home/claude/ref/ageServer/server/BattleServers.md`: battle server ports and arguments, including `-relayBroadcastPort`.
- `/home/claude/ref/ageServer/server/go.mod:11`: dependency on `github.com/luskaner/ageBattleServer`.
- `/home/claude/ref/ageServer/common/announce.go:1-30`: ports, multicast group, header, announce versions.
- `/home/claude/ref/ageServer/common/common.go:3`: `Name = "ageServer"`.
- `/home/claude/ref/ageServer/common/domain.go:34`: self-signed certificate domains.
- `/home/claude/ref/ageServer/common/server/server.go:22-26, 83-` : latency count, port 443, `LanServerIP`.
- `/home/claude/ref/ageServer/common/battleServer/broadcast.go:22-58`: presence packet parsing, `BroadcastPort`.
- `/home/claude/ref/ageServer/common/battleconfig/config.go:18-34, 249-297`: simulation periods, battle server config, `-relaybroadcastPort 0`.
- `/home/claude/ref/ageServer/common/hosts/line.go:12`, `/home/claude/ref/ageServer/common/hosts/hosts.go`: hosts-file marking and backup.
- `/home/claude/ref/ageServer/common/game/game.go:10-17`: game keys.
- `/home/claude/ref/ageServer/server/internal/cmd/root.go:405-445`: per-address HTTPS listeners, UDP query connections, body-size cap.
- `/home/claude/ref/ageServer/server/internal/ip/announce.go:17-88`: multicast join, UDP probe responder.
- `/home/claude/ref/ageServer/server/internal/runtime.go:10`: server UUID.
- `/home/claude/ref/ageServer/server/internal/routes/test/test.go:1-20`: `/test` headers.
- `/home/claude/ref/ageServer/server/internal/routes/game/advertisement/host.go:31-62, 64-`: host response encoding and flow.
- `/home/claude/ref/ageServer/server/internal/routes/game/advertisement/join.go:36-196`: join validation and peer add.
- `/home/claude/ref/ageServer/server/internal/routes/game/advertisement/leave.go:11-37`: leave.
- `/home/claude/ref/ageServer/server/internal/routes/game/party/updateHost.go:14-42`: host claim rule.
- `/home/claude/ref/ageServer/server/internal/models/advertisement.go:1-140, 457-496`: advertisement fields, peer removal, host setter.
- `/home/claude/ref/ageServer/server/internal/models/battleServer.go:91-130, 190-300`: public IP lookup, LAN flag, address resolution.
- `/home/claude/ref/ageServer/server/internal/models/peer.go:1-82`: peer model.
- `/home/claude/ref/ageServer/server/internal/routes/router/game.go:218-270`: chat and party route registration.
- `/home/claude/ref/ageServer/server/internal/routes/wss/wss.go:1-60`: WebSocket channel.
- `/home/claude/ref/ageServer/server/internal/models/customFiles.go:23-65`: custom file limits and paths.
- `/home/claude/ref/ageServer/battle-server-broadcast/battleServer.go:1-170`: header, NIC discovery, `CloneAnnouncements`, `cloneAnnouncementsLoop`.
- `/home/claude/ref/ageServer/launcher-common/launcher/server/server.go:180-330`: UDP probe, rounds, broadcast calculation.
- `/home/claude/ref/ageServer/launcher-common/launcher/flags.go:95-96, 164-165`: default announce ports and groups.
- `/home/claude/ref/ageServer/launcher-common/cert/cert_windows.go:1-60`: `ROOT` store install.
- `/home/claude/ref/ageServer/launcher-agent/README.md`: agent lifecycle and tray icon.
- `/home/claude/ref/ageServer/launcher-agent/internal/watch/watch.go:1-60, 225-260`: wait-for-game loop, rebroadcast call.
- `/home/claude/ref/ageServer/launcher-agent/internal/watch/watch_windows.go:19-30`: rebroadcast start.
- `/home/claude/ref/CnC_Remastered_Collection/TIBERIANDAWN/CONNECT.H:119-122`, `IPXMGR.H:151`: classic transport classes.
- `/home/claude/rts_2027/docs/research/03_cnc_networking_determinism.md`: prior EA networking study (summary and sections 1 and onward).
