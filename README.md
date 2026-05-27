# UniConnect

Modular fleet SaaS with three **products** on a shared **platform**:

| Layer | Responsibility |
|-------|----------------|
| **Platform** (`UniConnect.Tenant`, `UniConnect.Application`) | Tenant records, `ProductModule`, JWT auth, `ITenantService` |
| **General Fleet** (`UniConnect.GeneralFleet`) | Conventional fleet maintenance + GPS tracking |
| **Robo-Taxi** (`UniConnect.RoboTaxi`) | Autonomous passenger AV operations |
| **Delivery** (`UniConnect.Delivery`) | B2B/B2C logistics (conventional + autonomous vehicles) |

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
| `delivery@demo.local` | Delivery (Demo Delivery Fleet) |
| `admin@demo.local` | Platform admin (all products) |

## Module map

| UI section | Product module | API prefix | UI routes |
|------------|----------------|------------|-----------|
| Tenants (admin) | — | `/api/tenants` | `/tenants`, `/tenants/new`, `/tenants/:id` |
| General Fleet | `General` | `/api/fleet` | `/`, `/fleet/tenants/:id/…` |
| Robo-Taxi | `RoboTaxi` | `/api/robo-taxis` | `/robo-taxis`, … |
| Delivery | `Delivery` | `/api/delivery` | `/delivery`, … |

Platform admins create and manage tenants via `/api/tenants` (one or more product modules per tenant). General Fleet vehicle/maintenance/tracking endpoints live under `/api/fleet/...`.

## Solution layout

```
src/
  UniConnect.Tenant/          # Platform tenants (Tenant entity, ITenantService)
  UniConnect.Application/    # JWT auth only
  UniConnect.GeneralFleet/   # General Fleet product (vehicles, maintenance, GPS)
  UniConnect.RoboTaxi/       # Robo-Taxi product
  UniConnect.Delivery/       # Delivery product
  UniConnect.Infrastructure/ # EF Core, implementations
  UniConnect.Api/            # HTTP API
web/uniconnect-web/          # React SPA (modules/fleets, general-fleet, robo-taxi, delivery)
```

## Demo data

Development seed includes:

- **Demo General Fleet** — conventional vehicles, maintenance, GPS pings
- **Demo AV Fleet** — robo-taxi units with operational states
- **Demo Delivery Fleet** — B2B/B2C orders, conventional + AV delivery van

## Current app features

- JWT auth with **tenant-scoped** data access (each operator sees only their tenant)
- Log maintenance from the vehicle detail page
- Create B2B/B2C delivery orders from the UI
- Advance delivery status workflow on order detail
- Loading and error states on key pages

## Phase 2 (next)

Stripe, telematics webhooks, route optimization, B2B API keys, proof-of-delivery, multi-fleet users per account.
