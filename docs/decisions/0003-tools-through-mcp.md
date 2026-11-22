# 3. Tools served through MCP, in a separate process

- Status: accepted
- Date: 2026-11-19

## Context

The agent needs tools (`calculate`, `get_datetime` in v0; `get_weather`,
`convert_currency`, `wiki_summary` in v1). Tools can live in-process as C# functions, or in a
separate server that speaks the Model Context Protocol (MCP).

## Decision

Tools live in `Glassbox.Tools.Mcp`, a separate MCP server. The agent reaches
them through the `ICallTool` port and an MCP client adapter. Transport inside
Docker Compose: Streamable HTTP (builder may confirm at build time).

## Why

- MCP is the standard that .NET AI job offers ask about.
- A process boundary keeps tool code away from the loop and from secrets.
- Any MCP client (for example Claude Code) can use the same tools.

## Alternatives

| Option | Why not |
|---|---|
| In-process C# tool functions | Simpler, but shows no MCP and no isolation. |

## Consequences

- One more container on the server (small memory cost).
- Tool calls add network time; the trace panel shows it, which is fine.
- `ICallTool` must allow an agent behind it later (v2 subagent).
