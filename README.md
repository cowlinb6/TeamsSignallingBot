# teams-call-signaling-bot

Near **real-time Microsoft Teams call signaling** as a proof of concept: a
**.NET 10 ASP.NET Core Web API** that runs in a **single Linux Docker container**
(no Windows, no media SDK, no Kubernetes) and logs telephony events —
**Offered → Establishing → Established → Held → Retrieved → Transferring →
Terminated**, plus participant roster changes — within seconds, instead of the
~15-minute delay of post-call `callRecords`.

It is auto-invited to every in-scope call (1:1 / PSTN / meeting) via a Teams
**compliance recording policy**, and answers with **service-hosted media** (Microsoft
hosts the media; the bot only does call control). That is what keeps it off
Windows — the only Windows-locked component is the application-hosted *media* SDK,
which this PoC deliberately avoids.

## The experiment this settles

Compliance recording is designed around actually recording. **Open question:** will
Teams accept a bot that answers with service-hosted media and **never captures
media**, or drop the call for "recorder not recording"?

- **PASS** (call connects, events incl. Held/Retrieved flow) → the solution stays a
  Linux Web API.
- **FAIL** (call dropped) → granular all-call coverage needs app-hosted media
  (Windows).

## Layout

| Path | What |
| --- | --- |
| `src/SignalingBot/` | The .NET 10 Web API (webhook, token, answer, event mapping) |
| `deploy/setup-compliance-policy.ps1` | Registers the compliance recording app + assigns it to a pilot user |

See [`src/SignalingBot/README.md`](src/SignalingBot/README.md) for full
prerequisites, build/run (`docker` + `ngrok`), configuration, and the test loop.

## Quick start

```bash
cd src/SignalingBot
docker build -t signaling-bot .
docker run --rm -p 8080:8080 \
  -e Bot__TenantId=<tenant-guid> \
  -e Bot__ClientId=<app-client-id> \
  -e Bot__ClientSecret=<secret> \
  -e Bot__PublicUrl=https://<your>.ngrok.app \
  signaling-bot
```

Then expose it with `ngrok http 8080`, point the Azure Bot calling webhook at
`https://<ngrok>/api/calling`, assign the compliance policy to a pilot user, and
place a Teams call.
