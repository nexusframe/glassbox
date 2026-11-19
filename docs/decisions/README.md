# Architecture decision records

Each file records one decision: context, decision, why, alternatives,
consequences. A new decision gets the next number. A changed decision gets a
new ADR that replaces the old one (status "replaced by N").

| # | Decision | Status |
|---|---|---|
| [0001](0001-own-agent-loop.md) | Own agent loop on Microsoft.Extensions.AI, no agent framework | accepted |
| [0002](0002-thin-icomplete-port.md) | Thin `IComplete` port over `IChatClient` | accepted |
| [0003](0003-tools-through-mcp.md) | Tools served through MCP, in a separate process | accepted |
| [0004](0004-tool-results-are-data.md) | Tool results are data, not instructions | accepted |
| [0005](0005-trace-event-schema.md) | One trace event schema, aligned with OpenTelemetry GenAI | accepted |
| [0006](0006-contract-tests-and-evals.md) | Loop contract tests in CI, evals on the real model | accepted |
| [0007](0007-self-hosting-cloudflare.md) | Self-hosted arm64 server behind Cloudflare | accepted |
| [0008](0008-session-history-and-privacy.md) | Session history in server memory; no prompt text stored | accepted |
| [0009](0009-openrouter.md) | Cloud model through OpenRouter | accepted |
