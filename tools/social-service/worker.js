function json(data, init = {}) {
  return new Response(JSON.stringify(data), {
    status: init.status ?? 200,
    headers: {
      "content-type": "application/json; charset=utf-8",
      "access-control-allow-origin": "*",
      "access-control-allow-methods": "GET, POST, OPTIONS",
      "access-control-allow-headers": "content-type",
      ...(init.headers ?? {}),
    },
  });
}

function normalizeCode(value) {
  return (value ?? "").trim().toUpperCase();
}

function toRecord(body, nowIso) {
  return {
    cloudCode: normalizeCode(body.cloudCode),
    iconKey: (body.iconKey ?? "").trim(),
    displayName: (body.displayName ?? "").trim(),
    motto: (body.motto ?? "").trim(),
    presenceLine: (body.presenceLine ?? "").trim(),
    highlightTone: (body.highlightTone ?? "").trim(),
    shellStyle: (body.shellStyle ?? "").trim(),
    preferredTheme: (body.preferredTheme ?? "").trim(),
    favoritePlatform: (body.favoritePlatform ?? "").trim(),
    libraryCount: Number(body.libraryCount ?? 0),
    achievementCount: Number(body.achievementCount ?? 0),
    gamerScore: Number(body.gamerScore ?? 0),
    lastSeenAtUtc: nowIso,
  };
}

function withPresence(record) {
  const lastSeen = record?.lastSeenAtUtc ? Date.parse(record.lastSeenAtUtc) : 0;
  const isOnline = Number.isFinite(lastSeen) && Date.now() - lastSeen <= 180000;
  return { ...record, isOnline };
}

async function tryReadJson(request) {
  try {
    return await request.json();
  } catch {
    return null;
  }
}

export default {
  async fetch(request, env) {
    try {
      if (request.method === "OPTIONS") {
        return json({ ok: true });
      }

      const url = new URL(request.url);
      if (url.pathname === "/health") {
        return json({ ok: true, service: "cloudberry-social" });
      }

      if (!env.SOCIAL_KV) {
        return json({ ok: false, error: "KV binding missing" }, { status: 500 });
      }

      if (request.method === "POST" && url.pathname === "/publish") {
        const body = await tryReadJson(request);
        if (!body) {
          return json({ ok: false, error: "invalid json body" }, { status: 400 });
        }

        const nowIso = new Date().toISOString();
        const record = toRecord(body, nowIso);
        if (!record.cloudCode || !record.displayName) {
          return json({ ok: false, error: "cloudCode and displayName are required" }, { status: 400 });
        }

        await env.SOCIAL_KV.put(`profile:${record.cloudCode}`, JSON.stringify(record));
        return json({ ok: true, cloudCode: record.cloudCode, lastSeenAtUtc: record.lastSeenAtUtc });
      }

      if (request.method === "POST" && url.pathname === "/heartbeat") {
        const body = await tryReadJson(request);
        if (!body) {
          return json({ ok: false, error: "invalid json body" }, { status: 400 });
        }

        const cloudCode = normalizeCode(body.cloudCode);
        if (!cloudCode) {
          return json({ ok: false, error: "cloudCode is required" }, { status: 400 });
        }

        const existingRaw = await env.SOCIAL_KV.get(`profile:${cloudCode}`);
        if (!existingRaw) {
          return json({ ok: false, error: "profile not found" }, { status: 404 });
        }

        const existing = JSON.parse(existingRaw);
        existing.lastSeenAtUtc = new Date().toISOString();
        await env.SOCIAL_KV.put(`profile:${cloudCode}`, JSON.stringify(existing));
        return json({ ok: true, cloudCode, lastSeenAtUtc: existing.lastSeenAtUtc });
      }

      if (request.method === "GET" && url.pathname.startsWith("/friend/")) {
        const cloudCode = normalizeCode(url.pathname.split("/").pop());
        if (!cloudCode) {
          return json({ ok: false, error: "cloudCode is required" }, { status: 400 });
        }

        const existingRaw = await env.SOCIAL_KV.get(`profile:${cloudCode}`);
        if (!existingRaw) {
          return json({ ok: false, error: "profile not found" }, { status: 404 });
        }

        return json(withPresence(JSON.parse(existingRaw)));
      }

      return json({ ok: false, error: "not found" }, { status: 404 });
    } catch (error) {
      return json(
        {
          ok: false,
          error: error?.message ?? "worker failure",
          stack: error?.stack ?? null,
        },
        { status: 500 });
    }
  },
};
