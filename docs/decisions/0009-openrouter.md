# 9. Cloud model through OpenRouter

- Status: accepted (owner decision)
- Date: 2026-11-19

## Context

The public demo needs a cheap cloud model with tool calling. The model must
change by config, with no code change. Each provider has its own API,
key and billing.

## Decision

The model adapter talks to OpenRouter (`https://openrouter.ai/api/v1`), which
has an OpenAI-compatible API. We use `Microsoft.Extensions.AI.OpenAI` (the
OpenAI .NET SDK as an `IChatClient`) with that base URL and the key from
`OPENROUTER_API_KEY`. The model id is a config value.

## Why

- One key and one bill for many models; changing the model is a config change.
- The OpenAI-compatible API is the most common one, so the same adapter also
  fits Ollama and other local servers later.
- A credit limit on the OpenRouter key is a second guard behind our day
  budget (L0).

## Risks and how we limit them

| Risk | Measure |
|---|---|
| OpenRouter is one more company that sees the prompts (GDPR) | No prompt logging in OpenRouter settings; only providers that do not train on data; the privacy line on the page names OpenRouter |
| Tool calling works differently per model | Pick a model that supports tools; evals (v1) check it |
| OpenRouter is down | The page shows the offline message; the model can move to another base URL by config |

## Consequences

- The price table (ADR 0005) uses OpenRouter prices for the chosen model id.
