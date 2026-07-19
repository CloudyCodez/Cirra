# CloudBerry Social Service

This is a small Cloudflare Worker used for Cloud Code search and online presence.

It stores a public, lightweight profile card keyed by `CloudCode` and exposes:

- `GET /health`
- `POST /publish`
- `POST /heartbeat`
- `GET /friend/{cloudCode}`

## One-time setup

Run:

```powershell
cd C:\Users\conno\Documents\CloudBerry\tools\social-service
powershell -ExecutionPolicy Bypass -File .\deploy.ps1
```

The deploy script will:

1. ensure `npx` exists
2. log into Cloudflare with Wrangler
3. create a KV namespace if needed
4. patch `wrangler.toml`
5. deploy the worker

Once deployed, CloudBerry already points at the expected default URL:

`https://cloudberry-social.cloudberryxo.workers.dev`

If you deploy under a different worker name, update `PresenceEndpoint` in settings later if needed.
