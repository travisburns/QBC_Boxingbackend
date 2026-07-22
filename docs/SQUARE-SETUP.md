# Connecting QBC Boxing to Square

This is the operational guide for wiring the site to the client's **existing
Square account** so members can sign up and pay online. The code is already
built — this covers the credentials, the Square Dashboard setup, and going live
safely.

> **Processor:** Square (Web Payments SDK + Subscriptions API). Not Stripe.

---

## 0. The security model (read this first)

The whole design keeps us in the **lowest PCI tier (SAQ-A)** by never touching a
raw card. Do not break these rules:

- **Card data is entered only in Square's hosted iframe** (`lib/square.ts` on the
  frontend). The raw card number never reaches our code, API, or database. We
  only ever receive Square's single-use token (`sourceId`).
- **The Access Token and Webhook Signature Key are secrets.** They live *only*
  on the backend, in environment variables / user-secrets / a vault — never in
  the git repo, never in any `NEXT_PUBLIC_*` value, never sent to the browser.
- **The browser never sets the price.** Amounts come from server-side catalogs
  (`PlanCatalog`), so a tampered request can't change what's charged.
- **Every webhook is signature-verified** before we act on it, and each event is
  processed at most once.
- **HTTPS only** in production. No card flow over plain http.

### What is and isn't a secret

| Value | Lives in | Secret? |
|---|---|---|
| Application ID | frontend `NEXT_PUBLIC_SQUARE_APP_ID` | No — publishable |
| Location ID | frontend + backend | No |
| **Access Token** | backend only (`Square:AccessToken`) | **YES** |
| **Webhook Signature Key** | backend only (`Square:WebhookSignatureKey`) | **YES** |

---

## 1. Getting credentials from the client — safely

The client owns the Square account, so this is a **single-merchant** setup (no
Square OAuth needed). You need four values from his account.

**Do NOT accept the Access Token over email or chat in plaintext.** Use one of:

- He adds you to his **Square Developer** account, or
- He generates the values and shares them through a **password manager** (e.g.
  1Password/Bitwarden secure share) — never plaintext.

Develop against **Sandbox** the entire time. Only swap in his real production
token at launch (§5).

---

## 2. Backend configuration (never commit real values)

`appsettings.json` ships with empty placeholders. Real values go in
user-secrets (dev) or environment variables (prod).

### Development (user-secrets)

```bash
cd src/QBC.Api
dotnet user-secrets init
dotnet user-secrets set "Square:Environment"          "sandbox"
dotnet user-secrets set "Square:AccessToken"          "<SANDBOX_ACCESS_TOKEN>"
dotnet user-secrets set "Square:LocationId"           "<SANDBOX_LOCATION_ID>"
dotnet user-secrets set "Square:WebhookSignatureKey"  "<WEBHOOK_SIGNATURE_KEY>"
dotnet user-secrets set "Square:WebhookNotificationUrl" "https://<your-tunnel>/api/webhooks/square"
dotnet user-secrets set "Square:PlanVariationIds:strength"  "<VARIATION_ID>"
dotnet user-secrets set "Square:PlanVariationIds:boxing"    "<VARIATION_ID>"
dotnet user-secrets set "Square:PlanVariationIds:unlimited" "<VARIATION_ID>"
```

### Production

Set the same keys as **environment variables** (double-underscore = section
nesting), e.g.:

```
Square__Environment=production
Square__AccessToken=<PROD_ACCESS_TOKEN>
Square__LocationId=<PROD_LOCATION_ID>
Square__WebhookSignatureKey=<PROD_WEBHOOK_SIGNATURE_KEY>
Square__WebhookNotificationUrl=https://api.qbcboxing.com/api/webhooks/square
Square__PlanVariationIds__strength=<PROD_VARIATION_ID>
Square__PlanVariationIds__boxing=<PROD_VARIATION_ID>
Square__PlanVariationIds__unlimited=<PROD_VARIATION_ID>
```

`SquareOptions.ApiBaseUrl` automatically targets
`connect.squareupsandbox.com` vs `connect.squareup.com` based on
`Square:Environment` — you don't set the base URL by hand.

---

## 3. Create the membership plans in Square

The site has three tiers (source of truth: `Catalog/PlanCatalog.cs` and
`lib/plans.ts`). Prices there are what we **display**; the actual recurring
charge is driven by the Square subscription plan you create here.

In the Square Dashboard (Sandbox first):

1. **Subscriptions → Plans → Create plan** for each tier:
   - Strength — $89.00 / month
   - Boxing — $99.00 / month
   - Unlimited — $149.00 / month
2. Each plan has a **plan variation** — copy its ID (`Square:PlanVariationIds`).
3. Keep the display prices in `PlanCatalog.cs` in sync with the Square plans.

> Prices live in two places on purpose: the catalog for display/validation, and
> Square for actual billing. If you change a price, change both.

---

## 4. Register the webhook

Renewals, failed payments, and cancellations reach us via webhook — without it,
a member who lapses would still show as active.

1. Square Dashboard → **Developer → Webhooks → Add endpoint**.
2. URL = your public `…/api/webhooks/square` (in dev, expose localhost with a
   tunnel such as the Square CLI or ngrok — the URL must match
   `Square:WebhookNotificationUrl` **exactly**, since it's part of the signature).
3. Subscribe to at least: `subscription.created`, `subscription.updated`,
   `invoice.payment_made`, `invoice.scheduled_charge_failed`.
4. Copy the endpoint's **Signature Key** → `Square:WebhookSignatureKey`.

Verify: the handler rejects bad signatures with `401` and de-dupes by
`event_id` (`WebhookEvents` table).

---

## 5. Frontend configuration

Copy `.env.example` → `.env.local` and fill in the **publishable** values:

```
NEXT_PUBLIC_API_BASE_URL=http://localhost:5080
NEXT_PUBLIC_SQUARE_ENV=sandbox
NEXT_PUBLIC_SQUARE_APP_ID=<SANDBOX_APPLICATION_ID>
NEXT_PUBLIC_SQUARE_LOCATION_ID=<SANDBOX_LOCATION_ID>
```

At launch, switch `NEXT_PUBLIC_SQUARE_ENV=production` and use the production
Application ID + Location ID.

---

## 6. Test the full flow (Sandbox)

Use Square's [sandbox test cards](https://developer.squareup.com/docs/devtools/sandbox/payments)
(e.g. `4111 1111 1111 1111`, any future expiry, any CVV):

1. Register an account → go to a plan → checkout.
2. Confirm a `Subscriptions` row is created with status `Active` and card
   brand/last-4 only (no PAN).
3. In the Square Sandbox Dashboard, confirm the customer + subscription exist.
4. Trigger a webhook (Square CLI) and confirm status stays in sync.
5. Test a decline card → confirm the user gets a clean "card declined" message
   (`422`) and **no** local subscription row is left active.

---

## 7. Going live checklist

- [ ] Swap sandbox → production Access Token, Location ID, and plan variation IDs.
- [ ] Set `Square:Environment=production` (backend) and
      `NEXT_PUBLIC_SQUARE_ENV=production` (frontend).
- [ ] Register a **production** webhook endpoint and set its signature key.
- [ ] Confirm the site is served over HTTPS end to end.
- [ ] Run one real low-value transaction, then refund it from the Square Dashboard.
- [ ] Confirm no secret appears in the repo, logs, or any `NEXT_PUBLIC_*` value.

---

## 8. Planned: one-time / drop-in charges (not built yet)

Future use case: walk-ins paying once (e.g. a single class) — likely via a
tablet at the front desk. This is a **different Square API** than subscriptions
(`/v2/payments`, a single charge) but reuses the exact same tokenization flow,
so it slots into the existing architecture cleanly. When we build it:

- **Add `CreatePaymentAsync` to `ISquareGateway`** → calls `POST /v2/payments`
  with the `sourceId` token, amount, currency, idempotency key, and location.
- **Add a server-side `DropInCatalog`** (same pattern as `PlanCatalog`) so the
  one-time prices are defined on the server — the tablet sends an *item id*, never
  an amount.
- **Add an `Orders` table** to record one-time transactions (separate from
  `Subscriptions`), then generate the migration.
- **New endpoint** (e.g. `POST /api/checkout/one-time`). Decide the auth model:
  walk-ins probably shouldn't need a login, so this would be a guest charge
  (name + email, no account) — unlike the membership flow, which requires auth.

**Open decision:** card typed on the tablet screen (Web Payments SDK, reuses
what we have) vs. a physical Square card reader (**Square Terminal**, a separate
card-present integration). That choice determines the frontend design.

Non-negotiables when we build it: server-side amount, idempotency key on every
charge, and the same "never touch the raw card" rule.
