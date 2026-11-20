#!/usr/bin/env bash
# Local dev stack: tools server :8081, API :8090, chat page :5500.
# Reads OPENROUTER_API_KEY from deploy/.env (git-ignored).
# Dev only: the API runs in Development with content logging ON, so prompts, answers
# and tool data go to data/logs/api.log (git-ignored). Never use these settings on a server.
# Stop with Ctrl+C.
set -u
cd "$(dirname "$0")/.." || exit 1
ROOT=$PWD
LOGS=$ROOT/data/logs
mkdir -p "$LOGS"
[ -f deploy/.env ] && { set -a; . deploy/.env; set +a; }
[ -n "${OPENROUTER_API_KEY:-}" ] || echo "warning: OPENROUTER_API_KEY is not set; the page will show the offline recording"
dotnet build -v q >/dev/null || { echo "build failed"; exit 1; }

for p in 8081 8090 5500; do
  if curl -s -o /dev/null "localhost:$p"; then echo "port $p is already in use"; exit 1; fi
done

# exec the app dll directly, so $! is the app itself and the trap really stops it
# ('dotnet run' starts a child process that survives killing the wrapper).
(cd src/Glassbox.Tools.Mcp/bin/Debug/net10.0 && exec dotnet Glassbox.Tools.Mcp.dll) >"$LOGS/tools.log" 2>&1 &
TOOLS=$!
(cd src/Glassbox.Api/bin/Debug/net10.0 && \
  Glassbox__ToolsUrl=http://localhost:8081/mcp Glassbox__BudgetFile="$ROOT/data/budget.json" \
  Glassbox__AllowedOrigins__0=http://localhost:5500 \
  ASPNETCORE_ENVIRONMENT=Development Glassbox__LogContent=true \
  ASPNETCORE_URLS=http://localhost:8090 exec dotnet Glassbox.Api.dll) >"$LOGS/api.log" 2>&1 &
API=$!
python3 -m http.server 5500 --bind 127.0.0.1 -d web >"$LOGS/web.log" 2>&1 &
WEB=$!
trap 'kill $TOOLS $API $WEB 2>/dev/null; echo stopped' EXIT INT TERM

echo "Open http://localhost:5500  (Ctrl+C to stop). Logs: data/logs/"
wait
