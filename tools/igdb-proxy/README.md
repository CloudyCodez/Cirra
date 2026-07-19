# CloudBerry IGDB Proxy

This is a small Cloudflare Worker that keeps Twitch/IGDB credentials out of the desktop app. CloudBerry calls this proxy, and the proxy calls the official IGDB API.

## What You Need

- A Twitch Developer app with IGDB access credentials.
- `IGDB_CLIENT_ID`
- `IGDB_CLIENT_SECRET`
- Node.js LTS installed locally so `npx wrangler` can run.
- A free Cloudflare account.

## Fast Setup

Open PowerShell in this folder and run:

```powershell
.\deploy.cmd
```

The wrapper bypasses local script-policy restrictions for this one run. If Node.js/npm is missing, the script offers to install Node.js LTS through `winget`, then logs into Cloudflare, stores the two IGDB values as Worker secrets, deploys the Worker, and prints the deployed URL. Paste that URL into CloudBerry:

`Settings > Metadata > IGDB Proxy Endpoint`

If Cloudflare asks you to register a `workers.dev` subdomain and the first URL fails afterward, run:

```powershell
.\redeploy.cmd
```

That publishes the existing Worker again without asking for IGDB credentials.

## Endpoints CloudBerry Uses

```text
GET /search?query=Dragon%20Ball%20Z%20Budokai%20HD%20Collection&platform=&limit=20
POST /metadata/search
GET /health
```

The response shape matches CloudBerry's `IgdbProxyMetadataProvider`.
