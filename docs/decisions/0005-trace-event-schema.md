# 5. One trace event schema, aligned with OpenTelemetry GenAI

- Status: accepted
- Date: 2026-11-19

## Context

The trace panel is the main element of the demo. The loop, the panel, the
contract tests and the logs all need the same view of a run. Without a fixed
schema each part would invent its own.

## Decision

The loop emits one event shape: `type`,
`phase`, `id`, `parentId`, `runId`, `turn`, `startMs`, `endMs`, `status`,
`name`, tokens, `costUsd`, `args`, `text`. Names follow the OpenTelemetry
GenAI semantic conventions (`gen_ai.*`) where they exist. The Api streams the
events to the browser over `fetch` POST, one event per line.

## Why

- One schema means the panel shows what the loop really did.
- Contract tests can assert on events (order, parent ids).
- OTel alignment lets us add an OTel exporter later with no new schema.

## Consequences

- `args` and `text` go only to the user who asked; they are never stored
  (ADR 0008).
- Cost needs a price table per model in config.
