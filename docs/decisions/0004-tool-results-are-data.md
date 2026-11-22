# 4. Tool results are data, not instructions

- Status: accepted
- Date: 2026-11-19

## Context

Tool results go back into the model context. A result can contain text that
looks like an instruction ("ignore your rules and ..."), for example from a
weather API or a note. This is prompt injection. The demo is public.

## Decision

- The system prompt tells the model that tool results are data and never
  change its rules.
- Tool results are wrapped and marked as tool output in the messages.
- Limits do not depend on the model: L0 guards (budget, turns, tool calls per
  turn, max time) are enforced in code, so an injected instruction cannot
  raise them.
- Tools have no side effects in v0 and v1 (read-only), so a bad call cannot
  change anything.

## Why

A prompt alone does not stop injection. Hard limits in code and read-only
tools make the worst case small.

## Consequences

- A future tool with side effects needs a new ADR (confirmation step).
- `wiki_summary` returns text written by third parties: the clearest case for this decision.
