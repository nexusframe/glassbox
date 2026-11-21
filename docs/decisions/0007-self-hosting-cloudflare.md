# 7. Self-hosted arm64 server behind Cloudflare

- Status: accepted
- Date: 2026-11-19

## Context

The demo link goes into the CV, so it must be up when a recruiter opens it.
The demo runs on a small self-hosted arm64 server. The docs do not name the
hardware: it does not matter to the reader, and it would tell strangers what
the server runs.

## Decision

```text
 browser ─▶ Cloudflare Pages (chat page + video, always up)
        └─▶ Cloudflare Tunnel ─▶ arm64 server: Api + MCP tools (Docker)
```

- The Api and the MCP server run in Docker Compose; images are built for
  `linux/arm64` and `linux/amd64`.
- Cloudflare Tunnel: no open inbound ports; TLS ends at Cloudflare.
- CORS allowlist with the Pages origin only.
- The page checks `GET /health`; if it fails, it shows a recorded run (v0)
  or the video (v1).
- Fallback: the same Compose file moves to any small VPS.

## Why

- Free, and the hardware already exists.
- Pages keeps the page up even when the server is down.

## Risks and how we limit them

| Risk | Measure |
|---|---|
| A container escape reaches the host | Non-root, read-only containers; only the tunnel reaches the Api; the tools server is not exposed |
| Storage wear | Runs on the SD card (no SSD at hand; a USB pendrive wears out no slower). Few writes: read-only containers, `/tmp` in RAM, container logs capped at 3 × 1 MB, no prompt text on disk, only the small budget file is written. If the card fails, the page shows the recording until the card is replaced. |
| The server is offline | Recorded run or video on the page, uptime monitor, VPS fallback |
| Rate limit sees only the tunnel IP | Key on `CF-Connecting-IP`, trusted only from `cloudflared` |
| Cloudflare closes long responses (~100 s, documented, not tested) | Max time per run in L0 |
