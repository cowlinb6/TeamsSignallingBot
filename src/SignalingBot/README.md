# SignalingBot — Teams real-time call signaling PoC

A .NET 10 Web API that receives Microsoft Teams **call signaling** in near real
time and logs telephony events: **Offered → Establishing → Established → Held →
Retrieved → Transferring → Terminated**, plus participant roster changes.

It runs as a **single Linux Docker container** — no Windows, no Kubernetes, no
media. It uses **service-hosted media** (Microsoft hosts the media; the bot only
does call control), so the Windows-only real-time media SDK is never needed.

## The experiment this PoC settles

A Teams **compliance recording policy** auto-invites this bot to every in-scope
call (1:1 / PSTN / meeting) with no manual add. Compliance recording is *designed*
around actually recording. **The open question:** will Teams accept a bot that
answers with service-hosted media and **never captures media**, or will it drop
the call for "recorder not recording"?

- **PASS** (call connects, events flow incl. Held/Retrieved) → the whole solution
  can stay a Linux Web API.
- **FAIL** (call dropped) → granular all-call coverage requires app-hosted media,
  which is Windows-only.

## How it works

1. Graph POSTs notifications to `POST /api/calling`.
2. The bot validates the inbound Bearer token against the published OpenID config
   (`api.aps.skype.com`, issuer `https://api.botframework.com`, `aud` = bot App ID).
3. On the incoming policy call it answers via Graph REST
   `POST /communications/calls/{id}/answer` with `serviceHostedMediaConfig` (inside
   the ~5s window).
4. Subsequent call-state notifications are mapped to events and logged as one JSON
   line each (`CALL_EVENT { ... }`).

No Graph Communications Calling/Media SDK — just the webhook JSON + REST.

## Prerequisites

1. **Entra app registration** — Client ID + secret. Application permissions (admin
   consent): `Calls.JoinGroupCall.All`, `Calls.JoinGroupCallAsGuest.All`,
   `Calls.AccessMedia.All`.
2. **Azure Bot** resource sharing the same Client ID; Teams channel + calling on;
   **calling webhook** set to `https://<public-host>/api/calling`.
3. **Public HTTPS endpoint** — for the PoC, an [ngrok](https://ngrok.com) tunnel to
   the container. Only this signaling endpoint is needed (no media ports).
4. **Compliance recording policy** on one pilot user — see
   [`../../deploy/setup-compliance-policy.ps1`](../../deploy/setup-compliance-policy.ps1).
5. **Docker** (build uses the .NET 10 SDK image; no local SDK required).

## Configure

Set via environment variables (preferred) or `appsettings`:

| Variable | Meaning |
| --- | --- |
| `Bot__TenantId` | Entra tenant ID |
| `Bot__ClientId` | App registration / Azure Bot Client ID |
| `Bot__ClientSecret` | App registration client secret |
| `Bot__PublicUrl` | Public base URL (your ngrok URL), e.g. `https://abc123.ngrok.app` |
| `Bot__ValidateNotificationToken` | `true` (set `false` only for local plumbing tests) |

## Build & run

```bash
# from src/SignalingBot
docker build -t signaling-bot .

docker run --rm -p 8080:8080 \
  -e Bot__TenantId=<tenant-guid> \
  -e Bot__ClientId=<app-client-id> \
  -e Bot__ClientSecret=<secret> \
  -e Bot__PublicUrl=https://<your>.ngrok.app \
  signaling-bot

# in another shell: expose it and copy the HTTPS URL into the Azure Bot calling webhook
ngrok http 8080
```

Check health: `curl http://localhost:8080/healthz` → `{"status":"ok"}`.

## Test loop

1. Point the Azure Bot **calling webhook** at `https://<ngrok>/api/calling`.
2. Assign the compliance policy to the pilot user (the PowerShell script).
3. As the pilot user, place a **Teams VoIP 1:1 call**; put it **on hold**, then
   **resume**, then hang up.
4. Watch the container logs for `CALL_EVENT` lines — expect
   `Offered → Establishing → Established → Held → Retrieved → Terminated`.
5. Repeat with a **meeting** (3 people) for `ParticipantsChanged`, and — once a
   Calling Plan number is assigned — a **PSTN call**.

If the call connects and events flow, the experiment PASSES. If the call drops
when the bot answers, record it as a FAIL (media/Windows required).

## Notes & limits

- The ~5s answer window is tight; the Graph token is pre-warmed at startup.
- State is in-memory (fine for a single-instance PoC).
- PSTN needs Teams Phone licensing + a number; unsupported with Location-Based
  Routing.
