let cachedToken = "";
let cachedTokenExpiresAt = 0;

const jsonHeaders = {
  "content-type": "application/json; charset=utf-8",
  "access-control-allow-origin": "*",
  "access-control-allow-methods": "GET,POST,OPTIONS",
  "access-control-allow-headers": "content-type"
};

const platformIds = new Map([
  ["pc", 6],
  ["windows", 6],
  ["steam", 6],
  ["nes", 18],
  ["nintendo entertainment system", 18],
  ["snes", 19],
  ["super nintendo", 19],
  ["nintendo 64", 4],
  ["n64", 4],
  ["gamecube", 21],
  ["game cube", 21],
  ["wii", 5],
  ["wii u", 41],
  ["switch", 130],
  ["nintendo switch", 130],
  ["game boy", 33],
  ["game boy color", 22],
  ["game boy advance", 24],
  ["gba", 24],
  ["nintendo ds", 20],
  ["nds", 20],
  ["nintendo 3ds", 37],
  ["3ds", 37],
  ["playstation", 7],
  ["ps1", 7],
  ["playstation 2", 8],
  ["ps2", 8],
  ["playstation 3", 9],
  ["ps3", 9],
  ["playstation 4", 48],
  ["ps4", 48],
  ["playstation 5", 167],
  ["ps5", 167],
  ["psp", 38],
  ["playstation portable", 38],
  ["dreamcast", 23],
  ["sega dreamcast", 23],
  ["saturn", 32],
  ["sega saturn", 32],
  ["genesis", 29],
  ["mega drive", 29],
  ["sega genesis", 29],
  ["xbox", 11],
  ["xbox 360", 12],
  ["xbox one", 49],
  ["xbox series", 169]
]);

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") {
      return new Response(null, { headers: jsonHeaders });
    }

    const url = new URL(request.url);
    if (url.pathname === "/health") {
      return json({ ok: true, provider: "igdb", configured: isConfigured(env) });
    }

    if (!isConfigured(env)) {
      return json({ error: "IGDB_CLIENT_ID and IGDB_CLIENT_SECRET are not configured." }, 500);
    }

    if (request.method === "GET" && url.pathname === "/search") {
      const query = url.searchParams.get("query") || "";
      const platform = url.searchParams.get("platform") || "";
      const limit = toLimit(url.searchParams.get("limit"));
      return json(await searchGames(env, query, platform, limit));
    }

    if (request.method === "POST" && url.pathname === "/metadata/search") {
      const body = await request.json().catch(() => ({}));
      return json(await searchGames(env, body.query || "", body.platform || "", toLimit(body.limit)));
    }

    return json({ error: "Use GET /search or POST /metadata/search." }, 404);
  }
};

async function searchGames(env, rawQuery, platform, limit) {
  const queries = buildQueries(rawQuery);
  if (queries.length === 0) {
    return [];
  }

  const token = await getToken(env);
  const platformFilter = buildPlatformFilter(platform);
  const candidates = [];

  for (const query of queries.slice(0, 4)) {
    const withPlatform = await searchIgdb(env, token, query, limit, platformFilter);
    candidates.push(...withPlatform);

    if (withPlatform.length === 0 && platformFilter) {
      candidates.push(...await searchIgdb(env, token, query, limit, ""));
    }
  }

  const seen = new Set();
  return candidates
    .map(game => ({ game, score: scoreGame(rawQuery, game) }))
    .sort((left, right) => right.score - left.score || (right.game.total_rating || 0) - (left.game.total_rating || 0))
    .filter(entry => {
      const key = String(entry.game.id || entry.game.slug || entry.game.name).toLowerCase();
      if (seen.has(key)) {
        return false;
      }

      seen.add(key);
      return true;
    })
    .slice(0, limit)
    .map(entry => mapGame(entry.game));
}

async function searchIgdb(env, token, query, limit, platformFilter) {
  const where = platformFilter ? `where version_parent = null & ${platformFilter}; ` : "where version_parent = null; ";
  const body =
    `search "${escapeApicalypse(query)}"; ` +
    "fields id,name,slug,summary,first_release_date,total_rating,url,category," +
    "cover.url,artworks.url,screenshots.url,genres.name,platforms.name," +
    "alternative_names.name,websites.url,involved_companies.company.name," +
    "involved_companies.developer,involved_companies.publisher,videos.video_id; " +
    where +
    `limit ${limit};`;

  const response = await fetch("https://api.igdb.com/v4/games", {
    method: "POST",
    headers: {
      "accept": "application/json",
      "client-id": env.IGDB_CLIENT_ID,
      "authorization": `Bearer ${token}`
    },
    body
  });

  if (!response.ok) {
    return [];
  }

  return await response.json();
}

async function getToken(env) {
  const now = Date.now();
  if (cachedToken && cachedTokenExpiresAt > now + 300000) {
    return cachedToken;
  }

  const params = new URLSearchParams({
    client_id: env.IGDB_CLIENT_ID,
    client_secret: env.IGDB_CLIENT_SECRET,
    grant_type: "client_credentials"
  });
  const response = await fetch(`https://id.twitch.tv/oauth2/token?${params.toString()}`, { method: "POST" });
  if (!response.ok) {
    throw new Error(`Twitch token request failed: ${response.status}`);
  }

  const payload = await response.json();
  cachedToken = payload.access_token || "";
  cachedTokenExpiresAt = now + Math.max(60000, (payload.expires_in || 3600) * 1000);
  return cachedToken;
}

function mapGame(game) {
  const developers = (game.involved_companies || [])
    .filter(entry => entry.developer)
    .map(entry => entry.company?.name)
    .filter(Boolean);
  const publishers = (game.involved_companies || [])
    .filter(entry => entry.publisher)
    .map(entry => entry.company?.name)
    .filter(Boolean);
  const screenshots = (game.screenshots || [])
    .map(image => normalizeImage(image.url, "t_screenshot_big"))
    .filter(Boolean)
    .slice(0, 8);
  const artwork = (game.artworks || [])
    .map(image => normalizeImage(image.url, "t_1080p"))
    .find(Boolean) || "";
  const cover = normalizeImage(game.cover?.url, "t_cover_big");
  const slug = game.slug || "";

  return {
    id: String(game.id || ""),
    remoteId: String(game.id || ""),
    slug,
    remoteSlug: slug,
    title: game.name || "",
    name: game.name || "",
    summary: game.summary || "",
    description: game.summary || "",
    releaseDate: game.first_release_date ? new Date(game.first_release_date * 1000).toISOString() : "",
    coverImageUrl: cover,
    backgroundImageUrl: artwork || screenshots[0] || cover,
    bannerImageUrl: screenshots[0] || artwork || cover,
    iconImageUrl: cover,
    websiteUrl: firstWebsite(game.websites) || game.url || "",
    providerItemUrl: game.url || (slug ? `https://www.igdb.com/games/${slug}` : ""),
    url: game.url || "",
    communityRating: game.total_rating ? Math.round((game.total_rating / 20) * 100) / 100 : null,
    rating: game.total_rating || null,
    platforms: (game.platforms || []).map(item => item.name).filter(Boolean),
    genres: (game.genres || []).map(item => item.name).filter(Boolean),
    developers: unique(developers),
    publishers: unique(publishers),
    tags: (game.alternative_names || []).map(item => item.name).filter(Boolean).slice(0, 10),
    screenshots,
    trailerUrl: youtubeUrl((game.videos || [])[0]?.video_id)
  };
}

function buildQueries(rawQuery) {
  const primary = cleanTitle(rawQuery);
  if (!primary) {
    return [];
  }

  const aliases = [
    primary,
    primary.replace(/\bhd\b/gi, "").replace(/\s+/g, " ").trim(),
    primary.replace(/\bcollection\b/gi, "").replace(/\s+/g, " ").trim(),
    primary.replace(/\b(rom|iso|usa|europe|japan|rev\s*\d+)\b/gi, "").replace(/\s+/g, " ").trim()
  ];

  return unique(aliases.filter(Boolean));
}

function cleanTitle(value) {
  return String(value || "")
    .replace(/\[[^\]]*\]/g, " ")
    .replace(/\([^)]*(usa|europe|japan|world|rev|disc|prototype|beta)[^)]*\)/gi, " ")
    .replace(/\.(iso|rom|zip|7z|rar|nes|sfc|smc|n64|z64|gba|gbc|gb|nds|3ds|cia|rvz|wbfs|gcz|cso|chd|cue|bin)$/i, " ")
    .replace(/[_+]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function scoreGame(query, game) {
  const left = tokenSet(cleanTitle(query));
  const right = tokenSet(game.name || "");
  if (left.size === 0 || right.size === 0) {
    return 0;
  }

  const intersection = [...left].filter(token => right.has(token)).length;
  const union = new Set([...left, ...right]).size;
  let score = union ? (intersection / union) * 80 : 0;
  const normalizedQuery = [...left].join(" ");
  const normalizedName = [...right].join(" ");

  if (normalizedName === normalizedQuery) {
    score += 80;
  }
  if (normalizedName.includes(normalizedQuery) || normalizedQuery.includes(normalizedName)) {
    score += 18;
  }
  if (left.has("collection") && !right.has("collection")) {
    score -= 18;
  }
  if (left.has("hd") && !right.has("hd")) {
    score -= 10;
  }

  return score;
}

function tokenSet(value) {
  return new Set(String(value || "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .split(" ")
    .filter(token => token.length > 1));
}

function buildPlatformFilter(platform) {
  const normalized = String(platform || "").toLowerCase().trim();
  if (!normalized) {
    return "";
  }

  const ids = unique([...platformIds.entries()]
    .filter(([key]) => normalized.includes(key) || key.includes(normalized))
    .map(([, id]) => id));

  return ids.length ? `platforms = (${ids.join(",")})` : "";
}

function normalizeImage(url, size) {
  if (!url) {
    return "";
  }

  const absolute = url.startsWith("//") ? `https:${url}` : url;
  return absolute.replace(/t_[a-z0-9_]+/i, size);
}

function firstWebsite(websites = []) {
  return websites.map(site => site.url).find(Boolean) || "";
}

function youtubeUrl(videoId) {
  return videoId ? `https://www.youtube.com/watch?v=${videoId}` : "";
}

function escapeApicalypse(value) {
  return String(value || "").replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function isConfigured(env) {
  return Boolean(env.IGDB_CLIENT_ID && env.IGDB_CLIENT_SECRET);
}

function toLimit(value) {
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? Math.max(1, Math.min(parsed, 30)) : 20;
}

function unique(values) {
  return [...new Set(values.filter(Boolean))];
}

function json(value, status = 200) {
  return new Response(JSON.stringify(value, null, 2), { status, headers: jsonHeaders });
}
