# 2. Thin `IComplete` port over `IChatClient`

- Status: accepted
- Date: 2026-11-19

## Context

`Microsoft.Extensions.AI.IChatClient` is already an abstraction over model
providers. Our own port `IComplete` (L4) overlaps with it. A reviewer will ask
why we have both.

## Decision

Keep `IComplete` as a thin port in `Glassbox.Domain`. One adapter in
`Glassbox.Adapters` wraps `IChatClient`.

## Why

- The domain has no dependency on Microsoft types; the layer rule
  "adapters depend on ports" stays true.
- The port is the one place that emits model trace events (start, end,
  tokens, cost; ADR 0005), so no adapter can forget them.
- Contract tests use a fake `IComplete` with a small surface.

## Alternatives

| Option | Why not |
|---|---|
| Use `IChatClient` in the domain | Domain depends on a library; tracing must be added by every caller or by middleware we do not control. |

## Consequences

- The port must stay thin: messages in, a response with tool calls and usage
  out. If it starts to copy `IChatClient` features, reconsider this ADR.
