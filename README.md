# Glassbox

Glassbox is an AI agent you can see inside. You chat with it in the browser. Next to the chat, a live panel shows each step the agent takes: the guards, the model calls and the tool calls. The agent loop is our own code, and the tools run on a separate MCP server.

Demo: (link coming soon)

## What the visitor sees

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

## Where it runs

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

## Run locally

You need Docker. Steps:

```sh
cp deploy/.env.example deploy/.env   # then set OPENROUTER_API_KEY

curl localhost:8080/health
```

Serve the page:

```sh
python3 -m http.server 5500 -d web
```

`web/config.js` points to port 8090. To use it, set `API_PORT=8090` in `deploy/.env`.

## Privacy

Prompt text is not written to disk or logs.

A public instance must name its data controller (RODO/GDPR): set `GLASSBOX_OPERATOR` and `GLASSBOX_CONTACT` (an e-mail or an `https://` URL) in `web/config.js` at deploy time. The first-view notice shows them.

## How it is built

The code is written with AI coding agents; I read, review and accept every change, and I decide the direction. The ADRs in `docs/` show that work.
