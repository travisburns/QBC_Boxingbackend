# Apex Athletic — Backend API

ASP.NET Core 8 Web API for the Apex Athletic gym site. Handles member accounts
(auth) and Square-powered membership payments.

- **Framework:** ASP.NET Core 8 (Web API)
- **Data:** EF Core + SQL Server
- **Auth:** ASP.NET Core Identity + JWT bearer tokens
- **Payments:** Square (Subscriptions, Cards, Customers) via the Square REST API

## Security model (read this first)

Card data **never touches this backend**. The browser tokenizes the card with
Square's Web Payments SDK and sends us only a **single-use token** (`sourceId`).
We exchange that token — plus the gym owner's server-side Square access token —
for a card on file and a subscription. We persist only:

- Square identifiers (customer id, subscription id, card id)
- Subscription status
- Card **brand + last 4** (display only)

This keeps the site in scope for **PCI DSS SAQ-A**. Webhooks are **HMAC-SHA256
signature-verified** and de-duplicated before we act on them.

## Project layout

```
src/QBC.Api/
├─ Program.cs                 # DI, auth, CORS, pipeline, startup migration
├─ Controllers/               # Auth, Plans, Checkout, Account, Webhooks
├─ Services/                  # TokenService (JWT), MembershipService
│  └─ Square/                 # SquareGateway (REST), signature verification
├─ Models/                    # Entities: ApplicationUser, MembershipSubscription, enums
├─ Data/AppDbContext.cs       # Identity + subscriptions + webhook log
├─ Catalog/PlanCatalog.cs     # Server-side plan/price source of truth
├─ Dtos/                      # Request/response contracts
└─ Options/                   # Jwt / Square / Cors settings
```

## Prerequisites

- .NET 8 SDK
- SQL Server (LocalDB, Express, or Azure SQL)
- `dotnet tool install --global dotnet-ef`
- A Square account (sandbox for development)

## Configuration

Non-secret defaults live in `appsettings.json`. **Never commit secrets.** Set
them with user-secrets locally, and environment variables / a vault in
production.

```bash
cd src/QBC.Api
dotnet user-secrets init

# JWT signing key (>= 32 chars)
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"

# SQL Server (if not using the LocalDB default in appsettings.Development.json)
dotnet user-secrets set "ConnectionStrings:Default" "Server=...;Database=ApexAthletic;..."

# Square (sandbox values from the Square Developer Dashboard)
dotnet user-secrets set "Square:AccessToken" "EAAA....(sandbox access token)"
dotnet user-secrets set "Square:LocationId" "L....."
dotnet user-secrets set "Square:WebhookSignatureKey" "....."
dotnet user-secrets set "Square:WebhookNotificationUrl" "https://your-host/api/webhooks/square"

# Map each plan to its Square subscription plan-variation id
dotnet user-secrets set "Square:PlanVariationIds:strength"  "VARIATION_ID_1"
dotnet user-secrets set "Square:PlanVariationIds:boxing"    "VARIATION_ID_2"
dotnet user-secrets set "Square:PlanVariationIds:unlimited" "VARIATION_ID_3"
```

Create the subscription plans (and their variations) in the Square Dashboard or
Catalog API; paste the **plan variation** ids above. Prices are defined in the
Square plan **and** validated in `PlanCatalog` — keep them in sync.

## Database

An `InitialCreate` migration is already included under `Data/Migrations`. Just
point it at your database and apply it:

```bash
cd src/QBC.Api
dotnet ef database update      # or let the app apply migrations on startup
```

## Run

```bash
cd src/QBC.Api
dotnet run                     # http://localhost:5080  (Swagger at /swagger)
```

## API surface

| Method | Route                             | Auth | Purpose                          |
|-------:|-----------------------------------|:----:|----------------------------------|
| POST   | `/api/auth/register`              |  —   | Create account, returns JWT      |
| POST   | `/api/auth/login`                 |  —   | Log in, returns JWT              |
| GET    | `/api/auth/me`                    |  ✔   | Current user                     |
| GET    | `/api/plans`                      |  —   | Membership tiers                 |
| POST   | `/api/checkout/subscription`      |  ✔   | Start a membership (Square)      |
| GET    | `/api/account/membership`         |  ✔   | Current membership state         |
| POST   | `/api/account/membership/cancel`  |  ✔   | Cancel at period end             |
| POST   | `/api/account/payment-method`     |  ✔   | Update card on file              |
| POST   | `/api/webhooks/square`            | sig  | Square event sync (verified)     |
| GET    | `/health`                         |  —   | Health probe                     |

## Webhooks

In the Square Dashboard, add a webhook subscription pointing at
`/api/webhooks/square` and subscribe to `subscription.*` and `invoice.*`
events. Copy the signature key into `Square:WebhookSignatureKey` and the exact
notification URL into `Square:WebhookNotificationUrl` (it's part of the
signature). Use the Square CLI or a tunnel (e.g. ngrok) to test locally.

> Verified: builds clean with the .NET 8 SDK (`dotnet build -c Release`, 0
> warnings / 0 errors) and the `InitialCreate` migration is included. Before
> first run, set your secrets (above), confirm the Square plan-variation ids,
> and point the connection string at your SQL Server.
