# 6. Loop contract tests in CI, evals on the real model

- Status: accepted
- Date: 2026-11-19

## Context

Tests against a fake model are cheap and stable, but they say nothing about
how well the real model picks tools. Calling them "evals" would be wrong.

## Decision

Two kinds, two names:

- **Loop contract tests** (`tests/Glassbox.Tests`, CI on every push, v0):
  fake `IComplete`; check that tool calls are run and fed back, limits stop
  the loop, events come in order with the right parent ids, errors end the
  run cleanly.
- **Evals** (`tests/Glassbox.Evals`, manual run, v1): real model; each case
  has an input, the expected tool calls and the expected facts. Metrics:
  tool-choice accuracy, facts present, repeatability (N runs, pass rate).
  Results (pass rate, model id, date) go into the README.

## Why

Honest names. An interviewer who knows evals will check what "eval" means
here.

## Consequences

- Evals cost money; they do not run in CI.
- Evals start in v1, when there are enough tools to choose between.
