import fs from "node:fs";

const input = fs.readFileSync(0, "utf8").trim();
const games = input ? JSON.parse(input) : [];

const normalize = (value) =>
  String(value ?? "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim();

const groups = new Map();
for (const game of games) {
  const key = normalize(game.title);
  if (!key) continue;
  if (!groups.has(key)) groups.set(key, []);
  groups.get(key).push(game);
}

const duplicates = [...groups.values()]
  .filter((items) => items.length > 1)
  .map((items) => ({
    title: items[0].title,
    count: items.length,
    detail: items.map((item) => item.platform || "Unknown").join(", "),
  }))
  .slice(0, 8);

const missingMetadata = games.filter(
  (game) => !game.summary || !game.coverImagePath || !game.backgroundImagePath
);

const missingMedia = games.filter(
  (game) => !game.coverImagePath || !game.backgroundImagePath || !game.bannerImagePath || !game.iconImagePath
);

const neverPlayed = games
  .filter((game) => !game.lastPlayedOn)
  .slice(0, 10);

const platformCounts = new Map();
for (const game of games) {
  const platform = game.platform || "Unknown";
  platformCounts.set(platform, (platformCounts.get(platform) ?? 0) + 1);
}

const topPlatform = [...platformCounts.entries()].sort((a, b) => b[1] - a[1])[0];

const insights = [];
if (duplicates.length) {
  insights.push({
    title: "Possible duplicate titles",
    severity: "Warning",
    detail: `${duplicates.length} duplicate group(s) need review before the library gets noisy.`,
  });
}

if (missingMetadata.length) {
  insights.push({
    title: "Metadata cleanup queue",
    severity: "Info",
    detail: `${missingMetadata.length} title(s) still need summary, cover, or background art.`,
  });
}

if (missingMedia.length) {
  insights.push({
    title: "Artwork polish pass",
    severity: "Info",
    detail: `${missingMedia.length} title(s) are missing at least one media slot.`,
  });
}

if (neverPlayed.length) {
  insights.push({
    title: "New picks ready",
    severity: "Info",
    detail: `${neverPlayed.length} never-played title(s) can feed stronger dashboard recommendations.`,
  });
}

if (topPlatform) {
  insights.push({
    title: "Dominant platform",
    severity: "Good",
    detail: `${topPlatform[0]} leads the library with ${topPlatform[1]} title(s).`,
  });
}

process.stdout.write(
  JSON.stringify({
    summary:
      insights.length === 0
        ? "Library looks tidy. No companion cleanup suggestions right now."
        : `${insights.length} companion insight(s) generated from ${games.length} title(s).`,
    insights,
    duplicates,
  })
);
