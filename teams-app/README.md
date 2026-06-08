# Teams app package

A Teams app package for the signaling/compliance-recording bot: a zip of
`manifest.json` + two icons that you can **sideload for testing**, **upload to your
org catalog**, or **submit to the Teams Store** via Partner Center.

> **Read this first.** The bot does **not** need a Teams app package to function —
> it is invoked entirely through the Azure Bot + application instance + compliance
> recording policy (see [`../deploy/setup-compliance-policy.ps1`](../deploy/setup-compliance-policy.ps1)).
> This package only gives the app an **identity/listing** so admins can discover and
> deploy it. `supportsCalling`/`supportsVideo` are intentionally **omitted**: they are
> experimental, not for production, and irrelevant to compliance recording.

## Contents

| File | Purpose |
| --- | --- |
| `manifest.json` | Teams app manifest (schema v1.19) |
| `color.png` | 192×192 colour icon (placeholder) |
| `outline.png` | 32×32 transparent outline icon (placeholder) |
| `generate-icons.py` | Regenerates the two placeholder icons (stdlib only) |
| `package.sh` | Zips the three files into `appPackage.zip` (bash/macOS/Linux) |
| `package.ps1` | Same, for Windows PowerShell |

## 1. Fill in the manifest

Edit `manifest.json` and replace the placeholders:

| Field | Set to |
| --- | --- |
| `id` | A **new GUID** for the Teams app, distinct from the bot id. The manifest ships with a generated placeholder GUID so it imports cleanly; replace it with the GUID the Developer Portal assigns. |
| `bots[0].botId` | Your Azure Bot / Entra **Client ID** (currently `e5dddf52-…`). |
| `developer.*` URLs | Real website / privacy / terms URLs (required for Store submission). |
| `validDomains` | Public host(s) the app uses, e.g. `speech.cowling.dev`. |
| `webApplicationInfo` | Only needed for SSO/RSC — otherwise it can be removed. |

> `name.short` must be ≤ 30 chars and unique enough to pass Store name checks.

## 2. Build the package

```bash
cd teams-app
python3 generate-icons.py   # only if you don't have real icons yet
./package.sh                # -> appPackage.zip (files at the zip root)
```

On Windows:

```powershell
cd teams-app
python generate-icons.py    # only if you don't have real icons yet
.\package.ps1               # -> appPackage.zip (files at the zip root)
```

Replace `color.png` / `outline.png` with real branding before any Store
submission — the Store reviews icon quality. The placeholders are fine for
sideloading and org-catalog testing.

## 3. Validate

- **Developer Portal for Teams** (`dev.teams.microsoft.com`) → *Apps* → *Import app*
  → upload `appPackage.zip` → run the **app validation / preflight** checker. It flags
  manifest issues before you ever submit.
- Or the **App validation tool** in Teams admin center.

## 4. Test (sideload) before publishing

Two options:

- **Sideload to a team/chat** (needs "Upload custom apps" enabled in your setup
  policy): Teams → *Apps* → *Manage your apps* → *Upload an app* → *Upload a custom app*
  → pick `appPackage.zip`.
- **Org-wide catalog**: Teams admin center → *Teams apps* → *Manage apps* → *Upload new app*
  → upload the zip. This makes it available to your tenant without Store review — often
  the right end state for an internal compliance tool.

Because the recorder runs via the compliance policy, "installing" the app is not what
triggers recording — placing a call as the policy-assigned pilot user is. Use the
sideload only to validate the listing renders correctly.

## 5. Publish to the Teams Store (optional)

Store distribution goes through **Microsoft Partner Center** (Microsoft 365 and
Copilot program → Teams Store offer):

1. Create/confirm a Partner Center account enrolled in the Microsoft 365 program.
2. Create a new **Teams Store** offer and upload `appPackage.zip`.
3. Complete listing metadata (descriptions, screenshots, privacy/terms, support).
4. Pass **Store validation** (manifest, security, functionality review). A
   compliance-recording bot draws extra scrutiny — be ready to document data handling
   and that media is *not* captured (signaling only).
5. Submit; approved updates roll out to users within a few hours.

For an internal-only deployment you can stop at **step 4 of section 4** (org catalog)
and skip the Store entirely.

## Notes

- Keep `manifest.json` `version` (semver) bumped on every change you re-upload.
- The Teams app `id` (GUID) and the `bots[].botId` (Entra Client ID) are **different
  identifiers** — don't conflate them.
- Schema v1.19 is stable and broadly supported; newer versions (1.22+) exist but add
  features this app doesn't use.
