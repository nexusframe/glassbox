# 1. Own agent loop on Microsoft.Extensions.AI, no agent framework

- Status: accepted
- Date: 2026-11-19

## Context

Glassbox is a portfolio project. A technical interviewer must see the agent
loop (model call, tool call, tool result, repeat, stop) in our code. .NET has
agent frameworks (Microsoft Agent Framework, Semantic Kernel) that hide this
loop.

## Decision

We write the loop ourselves in `Glassbox.Domain`. We use
`Microsoft.Extensions.AI` (`IChatClient`) only to talk to the model, and the
official MCP C# SDK only for the protocol.

## Why

- The loop is the main thing the project must show.
- `IChatClient` lets us change the model by config, with no code change.
- The MCP SDK saves us from writing the protocol, which shows nothing.

## Alternatives

| Option | Why not |
|---|---|
| Microsoft Agent Framework | Hides the loop; the framework changes fast. |
| Everything from scratch | Much code to maintain; reinvents the HTTP clients. |

## Consequences

- We own loop bugs: limits, stop rules and error handling need tests
  (loop contract tests, ADR 0006).
- The README can later compare our loop with Agent Framework, because .NET AI
  job offers ask about it.
