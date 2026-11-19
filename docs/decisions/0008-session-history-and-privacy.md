# 8. Session history in server memory; no prompt text stored

- Status: accepted
- Date: 2026-11-19

## Context

The chat needs follow-up questions, so the model needs the history. If the
browser sends the history, a user can fake model or tool messages and bypass
limits. Prompts from a public demo are personal data (GDPR).

## Decision

- The server keeps the history in memory, by a random session id. The browser
  sends only the session id and the new message.
- Sessions expire after a TTL (for example 30 min); a max session count
  protects memory. A restart drops all chats.
- Nothing with prompt or answer text goes to disk or logs. We store only
  timings, token counts, cost and status (trace events without `text` and
  `args`).
- The daily budget counter is the only persisted state (SQLite or a file on
  a volume).
- One line under the chat tells the user this.

## Alternatives

| Option | Why not |
|---|---|
| History in the browser | Forgeable; needs signing (HMAC) to be safe. |
| No history (one question per run) | Safe, but no follow-up questions. |

## Consequences

- Debugging a bad answer needs the user to repeat it; we have no transcript.
- Memory use grows with sessions; TTL and max count keep it small on 4 GB.
