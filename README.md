# UniConnect

Modular fleet SaaS with three **products** on a shared **platform**:

| Layer | Responsibility |
|-------|----------------|
| **Platform** (`UniConnect.Tenant`, `UniConnect.Application`) | Tenant records, `ProductModule`, JWT auth, `ITenantService` |
| **General Fleet** (`UniConnect.GeneralFleet`) | Conventional fleet maintenance + GPS tracking |
| **Robo-Taxi** (`UniConnect.RoboTaxi`) | Autonomous passenger AV operations |
| **Delivery** (`UniConnect.Delivery`) | Logistics (conventional + autonomous vehicles) |

## Stack

- **Backend:** .NET 10 Web API, EF Core, PostgreSQL (port **5433** via Docker)
- **Frontend:** React 18 + Vite + TypeScript

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 8+)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for PostgreSQL)

## Quick start

### Option A: `dev.ps1` (Windows)

```powershell
.\dev.ps1
```

Starts Postgres (Docker), applies migrations, runs the API on **5000** and Vite on **5173**.

### Option B: Manual

#### 1. Database

```bash
cp .env.example .env
docker compose up -d
```

#### 2. API

```bash
cd src/UniConnect.Infrastructure
dotnet ef database update --startup-project ../UniConnect.Api
cd ../UniConnect.Api
dotnet run
```

API: http://localhost:5000 — Swagger: http://localhost:5000/swagger

#### 3. Web

```bash
cd web/uniconnect-web
npm install
npm run dev
```

Web: http://localhost:5173 — you will be redirected to **Sign in**.

### Demo logins (password: `Demo123!`)

| Email | Product / role |
|-------|----------------|
| `fleet@demo.local` | General Fleet (Demo General Fleet) |
| `av@demo.local` | Robo-Taxi (Demo AV Fleet) |
| `delivery@demo.local` | Delivery admin (Demo Delivery Fleet) |
| `delivery.ops@demo.local` | Delivery operator (same tenant, no Settings admin) |
| `alex.driver@demo.local` | Delivery driver (Alex Driver — routes + calendar only) |
| `admin@demo.local` | Platform admin (all products) |

## Module map

| UI section | Product module | API prefix | UI routes |
|------------|----------------|------------|-----------|
| Tenants (admin) | — | `/api/tenants` | `/tenants`, `/tenants/new`, `/tenants/:id` |
| General Fleet | `General` | `/api/fleet` | `/`, `/fleet/tenants/:id/…` |
| Robo-Taxi | `RoboTaxi` | `/api/robo-taxis` | `/robo-taxis`, … |
| Delivery | `Delivery` | `/api/delivery` | `/delivery`, … |
| Route planning | `RoutePlanning` | `/api/route-planning` | `/delivery/fleets/:id/plan` |
| Insights | `Insights` | `/api/insights` | `/insights` |

Platform admins create and manage tenants via `/api/tenants` (one or more product modules per tenant). General Fleet vehicle/maintenance/tracking endpoints live under `/api/fleet/...`.

## Solution layout

```
src/
  UniConnect.Tenant/          # Platform tenants (Tenant entity, ITenantService)
  UniConnect.Application/    # JWT auth only
  UniConnect.GeneralFleet/   # General Fleet product (vehicles, maintenance, GPS)
  UniConnect.RoboTaxi/       # Robo-Taxi product
  UniConnect.Delivery/       # Delivery product
  UniConnect.RoutePlanning/  # Route planning enhancement (requires Delivery)
  UniConnect.Insights/       # Analytics + LLM report bundles (requires Delivery)
  UniConnect.Infrastructure/ # EF Core, implementations
  UniConnect.Api/            # HTTP API
web/uniconnect-web/          # React SPA (modules/fleets, general-fleet, robo-taxi, delivery)
```

## Demo data

Development seed includes:

- **Demo General Fleet** — conventional vehicles, maintenance, GPS pings
- **Demo AV Fleet** — robo-taxi units with operational states
- **Demo Delivery Fleet** — 25 Tampa Bay depot-pickup orders (Tampa, St. Pete, Clearwater, Lakeland), conventional + AV delivery van

## Current app features

- JWT auth with **tenant-scoped** data access (each operator sees only their tenant)
- **Multi-user tenants** — admins invite team members with per-user product access (`User.ModuleAccess ∩ Tenant.Modules`)
- **Route planning** — Clarke–Wright multi-vehicle assignment, 2-opt stop sequencing, OSRM table matrix when configured (requires `RoutePlanning` module); tenant **planning rules** (markdown → compiled policy) with optional AI compile/explain
- **Insights** — operational event log, driver/vehicle/customer/planner analytics, LLM-ready JSON reports
- **Tenant roles** — `Admin` (org, team, API keys), `Operator` (dispatch/products), `Driver` (own routes + calendar + PTO requests)
- Log maintenance from the vehicle detail page
- Create delivery orders from the UI
- Tenant API keys in **Settings** for partner integrations
- Per-tenant **geocoding provider** (US Census, OpenStreetMap, or Google Maps with encrypted API key) in **Settings → Geocoding** (tenant admin)
- Partner delivery API (`X-Api-Key`): create and track orders
- **Fleet assets** — shared `Vehicles` table with `AssetCategory` (tractor, trailer, light vehicle, etc.) and tenant-unique **vehicle ID** (`VehicleNumber`). Create/edit under General Fleet → **Assets**; Delivery and Robo-Taxi list the same records.

## Partner API (delivery)

Auth: `X-Api-Key` header (tenant-scoped key from **Settings → API keys**).

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/delivery/partner/orders` | Create order |
| `GET` | `/api/delivery/partner/orders` | List tenant orders |
| `GET` | `/api/delivery/partner/orders/{id}` | Order status |

Demo partner key (delivery tenant): `uc_live_DemoDeliveryPartner0123456`

### Tenant team API (admin JWT)

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/tenants/me/users` | List tenant users |
| `POST` | `/api/tenants/me/users` | Invite user (email, role, modules, password) |
| `PATCH` | `/api/tenants/me/users/{id}` | Update display name, email, password, role, modules, or active status |
| `PATCH` | `/api/tenants/me/admin` | Update your own display name, email, or password |
| `DELETE` | `/api/tenants/me/users/{id}` | Remove user |
| `GET` | `/api/tenants/me/geocoding` | Geocoding provider settings (tenant admin) |
| `PUT` | `/api/tenants/me/geocoding` | Set provider (US Census, OpenStreetMap, or Google Maps + API key) |
| `POST` | `/api/tenants/me/geocoding/test` | Test geocoding with a sample address |
| `GET` | `/api/tenants/me/planning-rules` | Tenant route planning rules (markdown + compiled policy) |
| `PUT` | `/api/tenants/me/planning-rules` | Save rules (auto-compiles on save) |
| `POST` | `/api/tenants/me/planning-rules/compile` | Re-compile rules (optional AI when `PlanningRules:OpenAiApiKey` is set) |
| `POST` | `/api/route-planning/plan-runs/{id}/explain` | Plain-English plan summary (optional AI) |

## Phase 2 (next)

Stripe, telematics webhooks, route optimization, proof-of-delivery.
