# UniConnect

Modular fleet SaaS: **Fleet** (maintenance + tracking), **Robo-Taxi** (autonomous passenger AV), and **Delivery** (B2B/B2C logistics with autonomous and conventional vehicles).

## Stack

- **Backend:** .NET 10 Web API, EF Core, PostgreSQL (port **5433** via Docker)
- **Frontend:** React 18 + Vite + TypeScript

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 8+)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for PostgreSQL)

## Quick start

### 1. Database

```bash
cp .env.example .env
docker compose up -d
```

### 2. API

```bash
cd src/UniConnect.Infrastructure
dotnet ef database update --startup-project ../UniConnect.Api
cd ../UniConnect.Api
dotnet run
```

API: http://localhost:5000 — Swagger: http://localhost:5000/swagger

### 3. Web

```bash
cd web/uniconnect-web
npm install
npm run dev
```

Web: http://localhost:5173 — you will be redirected to **Sign in**.

### Demo logins (password: `Demo123!`)

| Email | Fleet / role |
|-------|----------------|
| `fleet@demo.local` | Demo General Fleet |
| `av@demo.local` | Demo AV Fleet (Robo-Taxi) |
| `delivery@demo.local` | Demo Delivery Fleet |
| `admin@demo.local` | Platform admin (all modules) |

## Module map

| UI section | Fleet type | API prefix |
|------------|------------|------------|
| Fleet | `General` | `/api/fleets`, `/api/vehicles` |
| Robo-Taxi | `RoboTaxi` | `/api/robo-taxis` |
| Delivery | `Delivery` | `/api/delivery` |

## Demo data

Development seed includes:

- **Demo General Fleet** — conventional vehicles, maintenance, GPS pings
- **Demo AV Fleet** — robo-taxi units with operational states
- **Demo Delivery Fleet** — B2B/B2C orders, conventional + AV delivery van

## Current app features

- JWT auth with **fleet-scoped** data access (each operator sees only their fleet)
- Log maintenance from the vehicle detail page
- Create B2B/B2C delivery orders from the UI
- Advance delivery status workflow on order detail
- Loading and error states on key pages

## Phase 2 (next)

Stripe, telematics webhooks, route optimization, B2B API keys, proof-of-delivery, multi-fleet users per account.
