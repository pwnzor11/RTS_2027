# M5 plan · LLM commander (D-087)

Status: draft for the owner (session 6). Owner answers already in: Anthropic **and** OpenAI-compatible providers; one
strategy decision every **30 s** of game time; in-character chat **on** by default; **no cheating** — the LLM sees only
what its player can see. Open points are in §9.

## 1 · Goal and what the player can try
- Skirmish setup: any AI slot's Kind can be **LLM commander** (on top of a tier, e.g. "LLM · Veteran"). The built-in AI
  of that tier does all moment-to-moment control; every 30 s the LLM reads a battlefield summary and sets the strategy.
- The commander talks in character as a faction leader (docs/lore/leaders.md, once approved) in a chat panel: a line when
  the strategy changes, reactions to big events (a base lost, a superweapon fired), and short replies when the player
  types to it.
- Spectator: LLM vs LLM (or LLM vs tiered AI) with both chat streams visible.
- No key, no network, or a failing provider: the slot plays as its plain tier and the chat says so once. The game never
  waits for the network.

## 2 · Determinism (D-051, D-087)
The LLM is outside the sim and is not deterministic. It only produces a **directive**: small integers, delivered as a
normal command in the command log at a tick, so replays, save/load and LAN lockstep replay the directive, never the call.
- New `CommandKind.Directive = 23` (append-only). Payload: `Subject` unused, `Arg` = packed directive (§3), `Cell` = focus
  cell or default. The sim stores the latest directive per player (hashed, append-only state part "directive") and
  exposes it in `PlayerView.Directive`. The sim never acts on it.
- Chat lines go through the existing `CommandKind.Chat` so they are in replays too (text table already exists for Chat —
  M5-02 checks length limits).
- The tiered AI reads `PlayerView.Directive` in Think and biases its managers (§4). Because the AI reads it from the view,
  the AI stays a pure function of (view, own memory, seeded RNG).
- Network timing: a request starts at a decision tick; the reply is applied at the first tick after it arrives (the
  game layer stamps the command with that tick). Replays store that tick, so playback is exact. A reply older than 30 s
  of game time is dropped. In LAN play (M9) only the slot's host machine calls the LLM and its directive travels as a
  command like any player input.

## 3 · Directive contract (all integers)
| Field | Range | Meaning |
|---|---|---|
| Posture | 0 turtle · 1 balanced · 2 aggressive · 3 all-in | wave thresholds, home guard size, retreat eagerness |
| Focus | 0 economy · 1 tech · 2 army · 3 air · 4 superweapon | where spare money goes first |
| TargetPlayer | slot or 255 = AI chooses | who the waves go to |
| Mix | 0 auto · 1 infantry · 2 vehicles · 3 anti-air · 4 anti-structure | unit bag weighting |
| Expand | 0/1 | build a forward refinery / take ruins |
| FocusCell | cell or none | attack/defend point (must be a cell the player has seen; else ignored) |

Packed into `Command.Arg` (int) as 4 bits each + 8 bits target. Unknown/out-of-range values clamp to "auto".

## 4 · How the tiered AI uses it
`DirectiveBias` (new, src/AI/Recruit/DirectiveBias.cs) turns the directive into multipliers the managers already have
knobs for: MilitaryManager wave threshold and target player, ProductionManager bag weights, BuildOrderManager /
TierPlanner / UpgradeManager reserve, GarrisonManager keep-in-army, EconomyRescue unaffected (survival first). The AI
still refuses impossible orders (locked tier, no money). A directive never gives information: the AI only targets what
its view knows.

## 5 · The commander (game layer, src/Llm — new project, referenced by Game only)
- `IChatProvider` with `AnthropicProvider` (Messages API) and `OpenAiCompatibleProvider` (base URL + model; works with
  OpenAI, local servers such as LM Studio / Ollama / llama.cpp). HttpClient, timeout 20 s, one request in flight per slot.
- `BattlefieldSummary` built from the slot's `PlayerView` only: own economy, army by role, tier, upgrades, known enemy
  buildings (with "last seen" age), visible enemy army, recent events (losses, attacks, superweapon charge seen), game
  time. Target ≤ 1 500 tokens. A golden test pins the summary for a fixed view.
- Prompt: system = rules of the game in brief + the directive schema + the leader persona; user = summary + last 3
  directives + player chat since last call. The model replies in JSON `{directive:{…}, say:"…"}`; parse strictly, clamp,
  ignore extras; on parse failure keep the last directive.
- Chat panel: in-character lines (max ~200 chars), player can type to a commander (`Enter`, then `/to 2 …`); the reply
  comes with the next decision or a short extra call (rate-limited).
- Cost guard: per-game call cap (default 120 ≈ 60 min) and a session token estimate shown in Settings.

## 6 · Settings and secrets
`Settings` gains `llm_provider`, `llm_base_url`, `llm_model`, `llm_chat_on` (default true), `llm_call_cap`. The API key is
stored **outside settings.toml**, encrypted for the current Windows user (DPAPI, `ProtectedData`), in
`%APPDATA%/RTS_2027/llm.key`; never written to logs, replays, saves, crash reports or the summary. A "Test connection"
button in Settings. Tests use a fake provider; CI never needs a key.

## 7 · Packages (proposed ids)
- M5-01 Contracts: CommandKind.Directive, directive state part + hash, PlayerView.Directive, pack/unpack, tests (Opus).
- M5-02 DirectiveBias in the tiered AI + tests that each field changes behaviour measurably (Sonnet).
- M5-03 src/Llm: providers, fake provider, JSON parsing/clamping, timeouts (Sonnet).
- M5-04 BattlefieldSummary from PlayerView + golden test + fog-honesty test (Sonnet).
- M5-05 Commander loop in the game: 30 s cadence, apply-at-tick, drop stale, fallback to plain tier (Sonnet).
- M5-06 Personas: prompt pack per leader in data/llm/*.toml (Haiku, after leaders are approved).
- M5-07 Chat panel + player→commander chat (Sonnet).
- M5-08 Settings: provider fields, DPAPI key store, Test connection (Sonnet).
- M5-09 Setup + spectator: LLM kind per slot, LLM-vs-LLM (Haiku).
- M5-10 Replay/save tests with recorded directives; owner play-test notes (lead).

## 8 · Acceptance
- A replay of an LLM game plays back identically with no network.
- Removing the key mid-game: the slot keeps playing at its tier, one chat notice.
- Fog test: a summary never mentions an enemy actor the player hasn't seen.
- A recorded "all-in vs player 2" directive measurably changes the AI's wave timing and target in a scripted test.

## 9 · Open questions for the owner
- Q-M5-a Default models: Anthropic → a fast, cheap model (e.g. Haiku-class) by default with Sonnet-class selectable; OpenAI-
  compatible → user types the model name. **Rec:** yes.
- Q-M5-b Key storage: DPAPI-encrypted file (rec) vs plain text in settings.toml (simpler, visible to anything on the PC).
- Q-M5-c Personas: leaders.md is still a DRAFT. Approve it (or mark up) before M5-06; until then a neutral "commander"
  voice per faction.
- Q-M5-d Call cap per game: 120 calls (rec) / none / user-set.
- Q-M5-e Talkback: may the player chat to enemy commanders (taunts, ceasefire banter)? **Rec:** yes, flavour only — chat
  never changes the directive unless it is that commander's own decision.
