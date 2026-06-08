# Path to Microsoft Marketplace (ISV Success)

Turning this PoC into a certifiable Microsoft Marketplace offer. ISV Success itself
is an **org enablement program** — eligibility is about your company joining the
partner programs and committing to publish, not about the code. The work below is
what's needed to actually **publish and pass certification** (re-certified every
6 months) and unlock the program's sales/marketing benefits.

Legend: `[ ]` todo · `[~]` partial · `[x]` done

## 0. Organizational prerequisites (Partner Center — not code)
- [ ] Join the **Microsoft AI Cloud Partner Program (MAICPP)**.
- [ ] Join **Microsoft Marketplace** (Commercial Marketplace) as a publisher.
- [ ] Confirm HQ is not on Microsoft's embargo list.
- [ ] Enroll in **ISV Success** and accept the Benefits Guide terms.
- [ ] Create the Partner Center **publisher profile** (verified publisher domain).

## 1. Marketplace offer — Teams app package
Artifacts live in [`teams-app/`](../teams-app/).
- [~] App package scaffolded (`manifest.json`, icons, `package.sh`).
- [ ] Replace `manifest.json` `id` with a dedicated **Teams app GUID** (distinct from `botId`).
- [ ] Set real `developer.name` / `websiteUrl` / `privacyUrl` / `termsOfUseUrl`.
- [ ] Replace placeholder `color.png` / `outline.png` with **production branding**.
- [ ] Finalize `name`/`description` (Store name uniqueness + length limits).
- [ ] Validate in **Developer Portal for Teams** (preflight) with zero errors.
- [ ] Decide listing type: free / contact-me / **transactable** (SaaS billing).

## 2. Legal & trust pages (required for review)
- [ ] Public **Privacy Policy** page (reachable, matches `privacyUrl`).
- [ ] Public **Terms of Use** page (matches `termsOfUseUrl`).
- [ ] Support contact / SLA page.
- [ ] Data Processing / sub-processor disclosure (handling Teams call signaling data).

## 3. Productization & hardening (today it's a single-instance PoC)
- [ ] Replace in-memory `CallStateTracker` with **durable storage** (e.g. Redis/SQL).
- [ ] Run **multiple instances** behind a load balancer (stateless or shared state).
- [ ] Health/readiness probes beyond `/healthz`; structured logging → log sink.
- [ ] Metrics + **alerting on bot availability** (fail-open means silent coverage gaps).
- [ ] Secret management: move `Bot__ClientSecret` to Key Vault / managed identity;
      consider **certificate credentials** over a client secret.
- [ ] Token-acquisition resilience under load (per-tenant cache already in place).
- [ ] Defined **data retention & deletion** for any captured signaling events.

## 4. Security & certification
- [ ] **Microsoft 365 Certification** (or Publisher Attestation) for the Teams app —
      expected for apps handling organizational communications data.
- [ ] Security review: TLS, token validation (done), least-privilege Graph scopes,
      tenant isolation in the multi-tenant token path.
- [ ] Penetration test / vulnerability scan evidence (certification may request it).
- [ ] Document admin-consent scopes and why each is needed
      (`Calls.AccessMedia.All`, `Calls.JoinGroupCall.All`, `Calls.JoinGroupCallAsGuest.All`).

## 5. Compliance-recording specifics (extra reviewer scrutiny)
- [ ] Document that the bot answers with **service-hosted media and captures no
      media** (signaling only) — reduces data-handling risk surface.
- [ ] Confirm current Teams platform policy still accepts a non-recording recorder
      (validate against recent third-party recording-bot restrictions).
- [ ] Per-tenant onboarding runbook: admin consent URL + `deploy/setup-compliance-policy.ps1`.
- [ ] Consent/notification posture documented (recording announcements, regional rules).

## 6. Operations for staying certified
- [ ] Versioning + changelog; bump `manifest.json` `version` per release.
- [ ] Calendar the **6-month re-certification** cadence.
- [ ] Incident response + customer-facing status page.

---

### Honest current state
- **Eligible to join ISV Success:** yes (B2B, Microsoft Cloud, marketplace-bound).
- **Ready to publish/certify:** not yet — sections 1–5 carry the real gaps. The bot
  *works* end to end as a PoC; productization, legal pages, branding, and
  certification are what stand between this and a live Marketplace offer.
