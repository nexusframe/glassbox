# Glassbox

Glassbox is an AI agent you can see inside. You chat with it in the browser. Next to the chat, a live panel shows each step the agent takes: the guards, the model calls and the tool calls. The agent loop is our own code, and the tools run on a separate MCP server.

Demo: <https://glassbox.wasyluk.pl>

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

## Evals

The evals measure the real model, not the loop. Each of 15 cases has an input, the tools the model must call and the facts the answer must contain. The model gets the production prompt and tool specs. Tool results come from fixed recordings, so the expected facts stay fixed; `calculate` runs for real. Facts are regex patterns, not an LLM judge. Each case runs 5 times. Reason for the split from the CI tests: [ADR 0006](docs/decisions/0006-contract-tests-and-evals.md).

Results on 2026-11-23, 75 runs per model:

| Metric | `anthropic/claude-haiku-5.5` (production) | `openai/gpt-oss-20b` |
|---|---|---|
| Pass rate | 75/75 (100%) | 64/75 (85%) |
| Tool choice | 75/75 (100%) | 72/75 (96%) |
| Facts present | 75/75 (100%) | 65/75 (87%) |
| Stable cases | 15/15 | 10/15 |
| Run errors | 0/75 | 0/75 |
| Cost of the run | 0.027 USD | 0.003 USD |

What the cheaper model gets wrong: 7 of 15 out-of-scope answers are in English, but the questions are Polish. In 2 of 5 runs of "weather and time in Paris" it skips `get_datetime` and gives the time of the weather reading. Once it computes 15% of 200 without `calculate`, and once it gives a wrong Tokyo time after the tool call.

Limit: 15 cases is a small set. Haiku passes all of them, so the set does not yet find its edge. Full reports: [Haiku](tests/Glassbox.Evals/results/2026-11-23-anthropic-claude-haiku-5.5.md), [gpt-oss-20b](tests/Glassbox.Evals/results/2026-11-23-openai-gpt-oss-20b.md).

Run it (manual, it costs money):

```bash
OPENROUTER_API_KEY=... dotnet run --project tests/Glassbox.Evals          # all cases, 5 runs
OPENROUTER_API_KEY=... dotnet run --project tests/Glassbox.Evals --only D14 --runs 3
OPENROUTER_API_KEY=... dotnet run --project tests/Glassbox.Evals --model openai/gpt-oss-20b
```
