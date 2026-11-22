# Glassbox at a glance

An AI agent you can see inside. You chat with it in the browser; next to the
chat, a live panel shows each step the agent takes.

Decisions and their reasons: `docs/decisions/`.

## 1. What the visitor sees

```text
┌─ Chat ─────────────────────┐┌─ How the agent works ───────────────┐
│ You: Time in Tokyo, and    ││ [1] question               0 ms     │
│      17% of 240?           ││ [2] L0 OK   budget 12% of day       │
│                            ││ [3] model ▶ 2 tools      410 ms     │
│ Agent: 23:14 in Tokyo.     ││   ├─ get_datetime(Tokyo)   4 ms ✓   │
│ 17% of 240 is 40.80.       ││   └─ calculate(240*0.17)   3 ms ✓   │
│                            ││ [4] model ▶ answer       520 ms     │
│ [sample questions] [PL|EN] ││ [5] end  2 turns, 1140 tok, $0.002  │
└────────────────────────────┘└─────────────────────────────────────┘
```

## 2. Where it runs (public demo)

```text
 user's browser
        │ opens page                       │ chat: fetch POST (stream)
        ▼                                  ▼
 ┌──────────────────┐            ┌───────────────────┐
 │ Cloudflare Pages │            │ Cloudflare Tunnel │  no open
 │ chat page, video │            └─────────┬─────────┘  inbound
 └──────────────────┘                      ▼
                          ┌─ small arm64 server (Docker) ──────┐
                          │ ┌────────────┐  MCP   ┌──────────┐ │
                          │ │ Api + loop │ ─────▶ │  Tools   │ │
                          │ └─────┬──────┘        └──────────┘ │
                          └───────┼────────────────────────────┘
                                  ▼ HTTPS
                          ┌──────────────────┐
                          │ OpenRouter ──▶   │
                          │ cheap cloud LLM  │
                          └──────────────────┘
```

If the server is down, the page shows a recorded run (v0) or the video (v1) instead of an error.

## 3. The agent loop (the core)

```text
 question ─▶ L0 guards ─▶ model ──── wants tools? ──yes──▶ call tools (MCP)
                ▲           ▲            │                        │
                │           └────────────┼──── tool results ◀─────┘
          blocked? stop                  no
                                         ▼
                                  answer (streams word by word)

 Each step emits a trace event ──▶ the same stream feeds the panel
 Stops at: no more tool calls │ max 5 turns │ max time │ budget out
```

## 4. Layers

```text
 L0  Rules        budget (day, session), rate limit, kill switch, max time
 L1  Checks       loop contract tests (CI) + evals on the real model (v1)
 L3  Config       appsettings: which adapter for each port
 ─────────────────────────────────────────────────────────────────────────
 L4  Ports        IComplete   ICallTool   ITranscribe   ISpeak
                      │           │            │           │
 L5  Adapters     OpenRouter  MCP client   local-stt   local-tts
                  / Ollama                 (v1, local voice mode only)

 Rule: adapters depend on ports, never the other way.
```

## 5. What is in each release

```text
            v0 (first CV link)        v1                    later
 ─────────────────────────────────────────────────────────────────────
 loop       ✓                         ✓                     v2: subagent
 tools      calculate, get_datetime   + get_weather,        (agent as a
                                        convert_currency,     tool)
                                        wiki_summary
 panel      ✓                         ✓                     v3: notes
 L0 guards  ✓                         ✓                         graph
 tests      contract tests in CI      + evals (real model)
 voice      -                         local + README video
 hosting    self-hosted, arm64        same
```

## 6. What the repo will hold

```text
src/
├── Glassbox.Domain/      loop, messages, ports (L4), L0 rules
├── Glassbox.Adapters/    model, MCP client, STT/TTS adapters (L5)
├── Glassbox.Api/         web API + chat page
├── Glassbox.Voice/       local voice console (v1)
└── Glassbox.Tools.Mcp/   MCP server with the tools
tests/
├── Glassbox.Tests/       unit + loop contract tests
└── Glassbox.Evals/       evals on the real model (v1)
docs/
├── overview.md           this file
└── decisions/            ADRs
```

## 7. Not in this project

- No voice in the public demo; voice is local only.
- No long-term memory and no user accounts.
- No agent framework; the loop is our own code.
- No prompt text written to disk or logs.
