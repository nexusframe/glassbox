"use strict";

// ---------- pure helpers (testable in Node) ----------

// Split a streamed chunk into complete lines. `rest` is the unfinished tail.
function splitLines(buffer, chunk) {
  const parts = (buffer + chunk).split("\n");
  const rest = parts.pop();
  return { lines: parts.filter(l => l.trim() !== ""), rest };
}

// Parse one NDJSON line. Returns null for a bad line.
function parseEventLine(line) {
  try {
    const o = JSON.parse(line);
    return o && typeof o === "object" && typeof o.type === "string" ? o : null;
  } catch { return null; }
}

// Pretty-print the args JSON string; fall back to raw text.
function formatArgs(args) {
  if (typeof args !== "string") return "";
  try { return JSON.stringify(JSON.parse(args)); } catch { return args; }
}

// Pretty-print args for the details block; fall back to raw text.
function prettyArgs(args) {
  if (typeof args !== "string") return "";
  try { return JSON.stringify(JSON.parse(args), null, 2); } catch { return args; }
}

// Short args for the one-line summary: a lone string value, else compact JSON.
function argSummary(args) {
  if (typeof args !== "string") return "";
  let o;
  try { o = JSON.parse(args); } catch { return args; }
  if (o && typeof o === "object" && !Array.isArray(o)) {
    const keys = Object.keys(o);
    if (keys.length === 0) return "";
    if (keys.length === 1 && typeof o[keys[0]] === "string") return o[keys[0]];
  }
  return JSON.stringify(o);
}

// One-line tool summary: name(args) ... / name(args) → result / name(args) → ✗ error.
function toolSummary(name, args, running, result, isError) {
  const head = name + "(" + argSummary(args) + ")";
  if (running) return head + " …";
  if (typeof result !== "string" || result === "") return isError ? head + " → ✗" : head;
  return head + " → " + (isError ? "✗ " : "") + result.replace(/\s+/g, " ");
}

// Build the replay event list (real event shape) from a scenario.
function buildReplay(sc, text) {
  const runId = "recording";
  const out = [];
  const add = (at, ev) => out.push({ at, ev: Object.assign({ runId }, ev) });
  add(0, { type: "run", id: "run", startMs: 0, phase: "start" });
  sc.spans.forEach((s, i) => {
    const base = { startMs: s.start, parentId: s.parent || "run" };
    if (s.kind === "guard") {
      add(s.start, { type: "guard", id: "guard", phase: "start", ...base });
      add(s.end, { type: "guard", id: "guard", phase: "end", ...base, endMs: s.end, status: "ok", budgetUsed: sc.budget });
    } else if (s.kind === "model") {
      add(s.start, { type: "model", id: "m" + s.turn, phase: "start", turn: s.turn, ...base });
      add(s.end, { type: "model", id: "m" + s.turn, phase: "end", turn: s.turn, ...base, endMs: s.end, status: "ok",
        name: sc.model || "model", inputTokens: s.inTok, outputTokens: s.outTok, costUsd: s.cost });
    } else {
      const id = "t" + s.turn + "." + s.n;
      add(s.start, { type: "tool", id, phase: "start", turn: s.turn, name: s.name, args: s.args, ...base });
      add(s.end, { type: "tool", id, phase: "end", turn: s.turn, name: s.name, ...base, endMs: s.end, status: "ok", text: s.result });
    }
  });
  const last = sc.spans.filter(s => s.kind === "model").pop();
  const words = text.split(" ");
  const from = last.start + 50, to = last.end - 10;
  words.forEach((w, i) => add(from + (to - from) * i / words.length,
    { type: "answer_delta", id: "m" + last.turn + ".d", startMs: Math.round(from), parentId: "m" + last.turn, text: (i ? " " : "") + w }));
  const total = sc.total;
  add(total, { type: "run", id: "run", startMs: 0, phase: "end", endMs: total, status: "ok",
    inputTokens: sc.spans.reduce((a, s) => a + (s.inTok || 0), 0),
    outputTokens: sc.spans.reduce((a, s) => a + (s.outTok || 0), 0),
    costUsd: sc.spans.reduce((a, s) => a + (s.cost || 0), 0) });
  return out.sort((a, b) => a.at - b.at);
}

if (typeof module !== "undefined" && module.exports) {
  module.exports = { splitLines, parseEventLine, formatArgs, prettyArgs, argSummary, toolSummary, buildReplay };
}

// ---------- browser app ----------

if (typeof document !== "undefined") (function () {
  const API = (window.GLASSBOX_API || "").replace(/\/$/, "");
  // Replay at real speed: the recording is a real 5.6 s run.
  const MAX_LEN = 500, MAX_TURNS = 5, SLOW = 1;
  const $ = id => document.getElementById(id);
  const I18N = window.GB_I18N, SAMPLES = window.GB_SAMPLES;

  // A real recorded run (timings, tokens and cost from the live API). The answer
  // text is reconstructed from the tool results, since the stream was not saved.
  const REC = {
    total: 5625, budget: 0, model: "anthropic/claude-haiku-5.5",
    q: { en: "What time is it in Tokyo, and what is 17% of 240?", pl: "Która godzina jest w Tokio i ile to 17% z 240?" },
    a: { en: "In Tokyo it is 08:47 on Friday, 20 November. 17% of 240 is 40.80.",
         pl: "W Tokio jest 08:47, piątek, 20 listopada. 17% z 240 to 40,80." },
    spans: [
      { kind: "guard", start: 49, end: 53 },
      { kind: "model", turn: 1, start: 435, end: 2231, inTok: 752, outTok: 109, cost: 0.0001297 },
      { kind: "tool", turn: 1, n: 1, parent: "m1", name: "get_datetime", args: '{"timeZone":"Asia/Tokyo"}', result: "2026-11-20 08:47, Friday, UTC+09:00 (Asia/Tokyo)", start: 2234, end: 2276 },
      { kind: "tool", turn: 1, n: 2, parent: "m1", name: "calculate", args: '{"expression":"240*0.17"}', result: "40.80", start: 2245, end: 2276 },
      { kind: "model", turn: 2, start: 2277, end: 5625, inTok: 956, outTok: 42, cost: 0.0001166 },
    ],
  };

  let lang = "en", sessionId = null, running = false, live = false;
  let t0 = 0, timeScale = 1, raf = 0, replayTimers = [];
  let run = null; // per-run state

  const t = (k, n) => (I18N[lang][k] || k).replace("{n}", n);
  const fmt = n => n.toLocaleString(lang === "pl" ? "pl-PL" : "en-US");
  const money = n => n.toFixed(4).replace(".", lang === "pl" ? "," : ".") + " USD";

  function store(k, v) { try { if (v === undefined) return localStorage.getItem(k); localStorage.setItem(k, v); } catch { } return null; }

  // ----- static text -----
  function applyStatic() {
    document.documentElement.lang = lang;
    document.title = t("title");
    const map = { "t-heading": "heading", "t-chat": "chat", "t-trace": "trace", "t-samples": "samples",
      "t-input-label": "inputLabel", "send": "send", "newchat": "newChat", "n-q": "nQ", "n-l0": "nL0", "n-ctx": "nCtx",
      "n-model": "nModel", "n-ans": "nAns", "t-wfstep": "wfStep", "t-wftime": "wfTime", "lg-l0": "lGuard", "lg-model": "nModel",
      "lg-tool": "lTool", "l-turns": "sTurns", "l-tok": "sTokens", "l-cost": "sCost", "l-time": "sTime", "l-bud": "sBudget",
      "g-lead": "lead", "g-ai-h": "aiH", "g-ai": "ai", "g-data-h": "dataH", "g-data": "data", "g-cookie-h": "cookieH",
      "g-cookie": "cookie", "g-start": "start", "tab-chat": "chat", "tab-trace": "traceTab" };
    for (const id in map) $(id).textContent = t(map[id]);
    $("t-callres").textContent = "↓ " + t("calls") + "   ↑ " + t("results");
    $("msg").placeholder = t("placeholder");
    $("info").setAttribute("aria-label", t("info")); $("info").title = t("info");
    $("strip").setAttribute("aria-label", t("openTrace"));
    $("back").textContent = "‹ " + t("backChat");
    // Fine print: one short AI line + a link that reopens the full notice.
    const fp = $("t-privacy"); fp.textContent = t("fine") + " ";
    const a = document.createElement("a"); a.href = "#"; a.textContent = t("fineLink");
    a.addEventListener("click", e => { e.preventDefault(); openGate(); });
    fp.appendChild(a);
    renderOperator();
    document.querySelectorAll(".lang").forEach(g => g.setAttribute("aria-label", t("lang")));
    document.querySelectorAll("[data-lang]").forEach(b => b.setAttribute("aria-pressed", String(b.dataset.lang === lang)));
    buildSamples();
    renderStrip();
    if (run) refreshLabels();
    updateControls();
  }

  function buildSamples() {
    document.querySelectorAll("#samples .chip").forEach(c => c.remove());
    SAMPLES[lang].forEach(q => {
      const b = document.createElement("button");
      b.type = "button"; b.className = "chip"; b.textContent = q;
      b.addEventListener("click", () => send(q));
      $("samples").appendChild(b);
    });
  }

  function updateControls() {
    const off = running || !live;
    $("send").disabled = off; $("msg").disabled = off; $("newchat").disabled = off;
    document.querySelectorAll("#samples .chip").forEach(c => { c.disabled = off; });
  }

  // ----- chat -----
  // Local wall-clock time, HH:MM:SS (24 h), for message stamps and the header clock.
  function hms(d) { return [d.getHours(), d.getMinutes(), d.getSeconds()].map(n => String(n).padStart(2, "0")).join(":"); }

  // Returns the text element, so streamed answers can append to it without touching the stamp.
  function addMsg(cls, text) {
    const d = document.createElement("div");
    d.className = "msg " + cls;
    const body = document.createElement("span"); body.className = "body"; body.textContent = text;
    const ts = document.createElement("time"); ts.className = "ts";
    const now = new Date(); ts.dateTime = now.toISOString(); ts.textContent = hms(now);
    d.append(body, ts);
    $("empty").hidden = true;
    $("chat").appendChild(d); $("chat").scrollTop = $("chat").scrollHeight;
    return body;
  }

  // Remove all messages; keep (and show) the empty state with the sample questions.
  function clearChat() {
    [...$("chat").children].forEach(c => { if (c.id !== "empty") c.remove(); });
    $("empty").hidden = false;
  }

  function tickClock() { const c = $("clock"); if (c) c.textContent = hms(new Date()); }
  tickClock(); setInterval(tickClock, 1000);
  function showError(key) { if (run && run.errShown === key) return; if (run) run.errShown = key; removeTyping(); addMsg("err", t(key)); }
  function removeTyping() { if (run && run.typing) { run.typing.remove(); run.typing = null; } }

  // ----- trace panel -----
  const BASE = ["n-q", "n-l0", "n-ctx", "n-model", "n-ans"];
  function setNode(id, st) { const n = $(id); if (!n) return; n.classList.remove("on", "done"); if (st) n.classList.add(st); }

  function newRun() {
    cancelAnimationFrame(raf);
    run = { spans: new Map(), order: [], tools: new Map(), turns: 0, tokens: 0, cost: 0, budget: null, endMs: 0,
      bot: null, typing: null, finished: false, blockedShown: false, errShown: null, sawEnd: false };
    $("wf").innerHTML = ""; $("toolnodes").innerHTML = "";
    BASE.forEach(n => setNode(n, null));
    setNode("n-q", "done");
    $("wf-scale").textContent = "0 ms";
    $("loopnote").textContent = t("turn") + " 0 / " + MAX_TURNS;
    renderSummary(0);
  }

  function addSpan(ev, kind, labelFn) {
    const row = document.createElement("div"); row.className = "wf-row";
    const lbl = document.createElement("span"); lbl.className = "lbl";
    const track = document.createElement("div"); track.className = "track";
    const bar = document.createElement("div"); bar.className = "bar k-" + kind;
    track.appendChild(bar);
    const ms = document.createElement("span"); ms.className = "ms";
    row.append(lbl, track, ms);
    $("wf").appendChild(row);
    const s = { id: ev.id, kind, start: ev.startMs, end: null, row, lbl, bar, ms, labelFn };
    lbl.textContent = labelFn();
    run.spans.set(ev.id, s); run.order.push(s);
    return s;
  }
  function refreshLabels() { run.order.forEach(s => { s.lbl.textContent = s.labelFn(); if (s.renderTool) s.renderTool(); }); updateLoopNote(); }

  // Tool row extras: a one-line summary button that expands full args and result.
  // All event data goes in through textContent.
  function addToolDetail(s, name, args) {
    const item = document.createElement("div"); item.className = "wf-item";
    s.row.parentNode.insertBefore(item, s.row); item.appendChild(s.row);
    const btn = document.createElement("button");
    btn.type = "button"; btn.className = "tool-sum"; btn.setAttribute("aria-expanded", "false");
    const chev = document.createElement("span"); chev.className = "chev"; chev.setAttribute("aria-hidden", "true"); chev.textContent = "▸";
    const line = document.createElement("span"); line.className = "sum-text";
    btn.append(chev, line);
    const det = document.createElement("div"); det.className = "tool-detail"; det.hidden = true; det.id = "td-" + s.id;
    btn.setAttribute("aria-controls", det.id);
    const mk = () => { const h = document.createElement("div"); h.className = "td-h"; const p = document.createElement("pre"); p.className = "td-pre"; det.append(h, p); return [h, p]; };
    const [argH, argP] = mk(), [resH, resP] = mk();
    item.append(btn, det);
    btn.addEventListener("click", () => {
      const open = btn.getAttribute("aria-expanded") !== "true";
      btn.setAttribute("aria-expanded", String(open)); chev.textContent = open ? "▾" : "▸"; det.hidden = !open;
    });
    s.args = args; s.result = undefined; s.isError = false;
    s.renderTool = () => {
      const running = s.end === null && s.result === undefined;
      line.textContent = toolSummary(name, s.args, running, s.result, s.isError);
      line.classList.toggle("err", s.isError);
      argH.textContent = t("argsLabel"); argP.textContent = prettyArgs(s.args) || "{}";
      resH.textContent = t("resultLabel");
      resP.textContent = running ? "…" : (s.result === undefined || s.result === "" ? "-" : s.result);
      resP.classList.toggle("err", s.isError);
    };
    s.renderTool();
  }

  function updateLoopNote() {
    if (!run) return;
    $("loopnote").textContent = run.finished
      ? (run.status === "ok" ? t("turnsEnd", run.turns) : t("stopped", run.turns))
      : t("turn") + " " + run.turns + " / " + MAX_TURNS;
  }

  function updateNodes() {
    const open = kind => run.order.some(s => s.kind === kind && s.end === null);
    const guard = run.order.find(s => s.kind === "guard");
    setNode("n-l0", guard ? (guard.end === null ? "on" : "done") : null);
    setNode("n-ctx", guard && guard.end !== null ? "done" : null);
    setNode("n-model", run.finished ? "done" : (open("model") ? "on" : null));
    run.tools.forEach((ids, name) => {
      const spans = ids.map(i => run.spans.get(i));
      setNode("n-tool-" + name, spans.some(s => s.end === null) ? "on" : "done");
    });
    setNode("n-ans", run.finished && run.status === "ok" ? "done" : null);
    renderStrip();
  }

  // Phone live strip: copy the state of the trace nodes into one line of pills.
  function renderStrip() {
    const st = $("strip"); if (!st) return;
    st.textContent = "";
    const ids = ["n-l0", "n-model"].concat([...document.querySelectorAll("#toolnodes .node")].map(n => n.id), ["n-ans"]);
    ids.forEach((id, i) => {
      const n = $(id); if (!n) return;
      if (i) { const s = document.createElement("span"); s.className = "sep"; s.textContent = "›"; st.appendChild(s); }
      const p = document.createElement("span");
      p.className = "pill" + (n.classList.contains("tool") ? " tool" : "") + (n.classList.contains("on") ? " on" : n.classList.contains("done") ? " done" : "");
      p.textContent = n.textContent; st.appendChild(p);
    });
    const go = document.createElement("span"); go.className = "go"; go.textContent = t("traceTab") + " ›"; st.appendChild(go);
  }

  // RODO: data controller + contact from config.js. Contact is an e-mail or an https:// URL;
  // anything else is ignored. Empty config (local dev) shows nothing.
  function renderOperator() {
    const el = $("g-op"), name = String(window.GLASSBOX_OPERATOR || "").trim(), contact = String(window.GLASSBOX_CONTACT || "").trim();
    let href = "";
    if (/^https:\/\/[^\s]+$/i.test(contact)) href = contact;
    else if (/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(contact)) href = "mailto:" + contact;
    el.textContent = "";
    if (!name || !href) { el.hidden = true; return; }
    const a = document.createElement("a"); a.href = href; a.textContent = contact;
    if (href.startsWith("https:")) { a.target = "_blank"; a.rel = "noopener"; }
    el.append(t("opLabel") + " " + name + ". " + t("contactLabel") + " ", a);
    el.hidden = false;
  }

  // ----- tabs (phone) -----
  function setTab(name) {
    ["chat", "trace"].forEach(k => {
      const on = k === name;
      $("tab-" + k).setAttribute("aria-selected", String(on));
      $("tab-" + k).tabIndex = on ? 0 : -1;
      $("p-" + k).classList.toggle("active", on);
    });
  }

  // ----- first-view notice (AI Act / RODO / cookies) -----
  const NOTICE_KEY = "glassbox.notice", NOTICE_VER = "1";
  function openGate() {
    $("gate").hidden = false; document.body.classList.add("gated"); $("app").inert = true;
    $("g-start").focus();
  }
  function closeGate() {
    store(NOTICE_KEY, NOTICE_VER);
    $("gate").hidden = true; document.body.classList.remove("gated"); $("app").inert = false;
    if (!$("msg").disabled) $("msg").focus();
  }

  function renderSummary(elapsed) {
    $("s-turns").textContent = run ? run.turns : 0;
    $("s-tok").textContent = fmt(run ? run.tokens : 0);
    $("s-cost").textContent = money(run ? run.cost : 0);
    $("s-time").textContent = fmt(Math.round(elapsed)) + " ms";
    $("s-bud").textContent = run && run.budget !== null ? (run.budget * 100).toFixed(1).replace(".", lang === "pl" ? "," : ".") + "%" : "-";
  }

  function elapsedNow() { return (performance.now() - t0) / timeScale; }

  function draw() {
    if (!run) return;
    const el = run.finished ? run.endMs : elapsedNow();
    const maxEnd = run.order.reduce((m, s) => Math.max(m, s.end === null ? 0 : s.end), 0);
    const scale = Math.max(el, maxEnd, 1000);
    $("wf-scale").textContent = "0 ms ─────── " + fmt(Math.round(scale)) + " ms";
    run.order.forEach(s => {
      const end = s.end === null ? Math.max(el, s.start) : s.end;
      s.bar.style.left = (s.start / scale * 100) + "%";
      s.bar.style.width = (Math.max(end - s.start, 0) / scale * 100) + "%";
      s.row.classList.add("vis");
      s.ms.textContent = fmt(Math.round(end - s.start)) + " ms";
    });
    $("s-time").textContent = fmt(Math.round(el)) + " ms";
  }
  function tick() { draw(); if (run && !run.finished) raf = requestAnimationFrame(tick); }
  function startClock(scale) { timeScale = scale; t0 = performance.now(); cancelAnimationFrame(raf); raf = requestAnimationFrame(tick); }

  // ----- event handling -----
  function handleEvent(ev) {
    if (!run) return;
    const start = ev.phase === "start", end = ev.phase === "end";
    switch (ev.type) {
      case "guard":
        if (start) addSpan(ev, "l0", () => t("lGuard"));
        else if (end) {
          const s = run.spans.get(ev.id); if (s) { s.end = ev.endMs; if (ev.status) s.row.classList.add(ev.status === "ok" ? "ok" : ev.status); }
          if (typeof ev.budgetUsed === "number") run.budget = ev.budgetUsed;
        }
        break;
      case "model": {
        const turn = ev.turn || 0;
        if (start) { run.turns = Math.max(run.turns, turn); addSpan(ev, "model", () => t("lModel", turn)); }
        else if (end) {
          const s = run.spans.get(ev.id); if (s) { s.end = ev.endMs; if (ev.status === "error") s.row.classList.add("err"); }
          run.tokens += (ev.inputTokens || 0) + (ev.outputTokens || 0);
          run.cost += ev.costUsd || 0;
        }
        break;
      }
      case "tool":
        if (start) {
          const name = ev.name || "tool";
          const s = addSpan(ev, "tool", () => " └ " + name);
          const args = formatArgs(ev.args);
          addToolDetail(s, name, ev.args);
          if (!run.tools.has(name)) {
            run.tools.set(name, []);
            const n = document.createElement("span");
            n.className = "node tool"; n.id = "n-tool-" + name; n.textContent = name;
            if (args) n.title = args;
            $("toolnodes").appendChild(n);
          }
          run.tools.get(name).push(ev.id);
        } else if (end) {
          const s = run.spans.get(ev.id);
          if (s) {
            s.end = ev.endMs; s.isError = ev.status === "error";
            if (s.isError) s.row.classList.add("err");
            s.result = typeof ev.text === "string" ? ev.text : "";
            if (s.renderTool) s.renderTool();
          }
        }
        break;
      case "answer_delta":
        removeTyping();
        if (!run.bot) run.bot = addMsg("bot", "");
        {
          // Follow the streamed answer, unless the reader scrolled up to read older messages.
          const c = $("chat"), atBottom = c.scrollHeight - c.scrollTop - c.clientHeight < 40;
          run.bot.textContent += ev.text || "";
          if (atBottom) c.scrollTop = c.scrollHeight;
        }
        break;
      case "error":
        showError("errRun");
        break;
      case "run":
        if (end) {
          run.sawEnd = true; run.finished = true; run.status = ev.status || "ok"; run.endMs = ev.endMs || 0;
          if (typeof ev.inputTokens === "number") run.tokens = ev.inputTokens + (ev.outputTokens || 0);
          if (typeof ev.costUsd === "number") run.cost = ev.costUsd;
          run.order.forEach(s => { if (s.end === null) s.end = run.endMs; });
          removeTyping();
          if (run.status === "blocked") showError("blocked");
          else if (!run.bot) showError("noAnswer"); // run ended without any answer text
          else if (run.status === "error") showError("errRun");
          draw();
        }
        break;
    }
    if (ev.type === "guard" && end && ev.status === "blocked") showError("blocked");
    updateNodes(); updateLoopNote(); renderSummary(run.finished ? run.endMs : elapsedNow());
    if (run.finished) draw();
  }

  // ----- live send -----
  const ERR_KEYS = { empty_message: "errEmpty", message_too_long: "errLong", bad_request: "errBad",
    session_busy: "errBusy", rate_limited: "errRate", paused: "errPaused" };

  async function send(text) {
    text = (text || "").trim();
    if (running || !live) return;
    if (!text) { newRun(); showError("errEmpty"); return; }
    if (text.length > MAX_LEN) { newRun(); showError("errLong"); return; }
    // The chat keeps the whole conversation (the server session does too); only the trace panel is per run.
    newRun();
    addMsg("user", text);
    run.typing = document.createElement("div"); run.typing.className = "typing"; run.typing.textContent = t("working");
    $("chat").appendChild(run.typing);
    $("msg").value = "";
    running = true; updateControls();
    startClock(1);
    const myRun = run;
    try {
      const body = { message: text }; if (sessionId) body.sessionId = sessionId;
      const resp = await fetch(API + "/chat", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!resp.ok) {
        let code = ""; try { code = (await resp.json()).error; } catch { }
        showError(ERR_KEYS[code] || "errServer");
        myRun.finished = true; return;
      }
      const sid = resp.headers.get("X-Session-Id"); if (sid) sessionId = sid;
      const reader = resp.body.getReader(), dec = new TextDecoder();
      let buf = "";
      for (;;) {
        const { done, value } = await reader.read();
        const chunk = done ? dec.decode() : dec.decode(value, { stream: true });
        const r = splitLines(buf, chunk); buf = r.rest;
        const lines = done && buf.trim() ? r.lines.concat(buf) : r.lines;
        lines.forEach(l => { const ev = parseEventLine(l); if (ev) handleEvent(ev); });
        if (done) break;
      }
      if (!myRun.sawEnd) { myRun.finished = true; showError("errNet"); }
    } catch {
      myRun.finished = true; showError("errNet");
    } finally {
      myRun.finished = true; draw();
      running = false; updateControls();
    }
  }

  // ----- offline replay -----
  function stopReplay() { replayTimers.forEach(clearTimeout); replayTimers = []; }
  function startReplay() {
    stopReplay();
    clearChat();
    newRun();
    addMsg("user", REC.q[lang]);
    run.typing = document.createElement("div"); run.typing.className = "typing"; run.typing.textContent = t("working");
    $("chat").appendChild(run.typing);
    startClock(SLOW);
    buildReplay(REC, REC.a[lang]).forEach(item => {
      replayTimers.push(setTimeout(() => handleEvent(item.ev), item.at * SLOW));
    });
  }
  function showOffline() {
    live = false;
    const b = $("banner"); b.hidden = false; b.textContent = "";
    const p = document.createElement("span"); p.textContent = t("offline") + " " + t("recording");
    const r = document.createElement("button"); r.type = "button"; r.id = "replay"; r.textContent = t("replay");
    r.addEventListener("click", startReplay);
    b.append(p, r);
    b.dataset.offline = "1";
    updateControls(); startReplay();
  }
  function refreshBanner() {
    if ($("banner").dataset.offline) { showOffline(); }
  }

  // ----- init -----
  function setLang(l) {
    lang = l; store("glassbox.lang", l);
    applyStatic();
    if ($("banner").dataset.offline) refreshBanner();
  }

  async function health() {
    const ctl = new AbortController(), timer = setTimeout(() => ctl.abort(), 4000);
    try {
      const r = await fetch(API + "/health", { signal: ctl.signal });
      const j = await r.json();
      return r.ok && j && j.modelCallsEnabled === true;
    } catch { return false; } finally { clearTimeout(timer); }
  }

  document.querySelectorAll("[data-lang]").forEach(b => b.addEventListener("click", () => setLang(b.dataset.lang)));
  $("form").addEventListener("submit", e => { e.preventDefault(); send($("msg").value); });
  // New chat: forget the session id (the server then starts an empty history) and clear the chat.
  $("newchat").addEventListener("click", () => {
    if (running || !live) return;
    sessionId = null;
    clearChat();
    newRun(); setNode("n-q", null); draw(); renderStrip();
    setTab("chat"); $("msg").focus();
  });
  $("tab-chat").addEventListener("click", () => setTab("chat"));
  $("tab-trace").addEventListener("click", () => setTab("trace"));
  $("tabs").addEventListener("keydown", e => {
    if (e.key !== "ArrowLeft" && e.key !== "ArrowRight") return;
    const next = $("tab-chat").getAttribute("aria-selected") === "true" ? "trace" : "chat";
    setTab(next); $("tab-" + next).focus();
  });
  $("strip").addEventListener("click", () => setTab("trace"));
  $("back").addEventListener("click", () => setTab("chat"));
  $("info").addEventListener("click", openGate);
  $("g-start").addEventListener("click", closeGate);
  document.addEventListener("keydown", e => {
    if (e.key === "Escape" && !$("gate").hidden && store(NOTICE_KEY) === NOTICE_VER) closeGate();
  });

  const saved = store("glassbox.lang");
  lang = saved === "pl" || saved === "en" ? saved : "en";
  setTab("chat");
  applyStatic();
  newRun();
  $("loopnote").textContent = "";
  if (store(NOTICE_KEY) !== NOTICE_VER) openGate();
  health().then(ok => { if (ok) { live = true; updateControls(); } else showOffline(); });
})();
