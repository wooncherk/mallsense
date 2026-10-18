# Sunway Kluang Mall · MallSense

This MallSense deployment is configured for Sunway Kluang Mall. It provides a shopper-facing directory, current deals and events, visitor assistance, and an authenticated administration area backed by a local SQLite database.

## What it currently does

### Shopper experience

- Browse and filter the store directory by category and floor.
- View published deals and events.
- Ask an AI concierge for grounded store, deal, and event recommendations.
- Calculate deterministic indoor routes, including accessible routes, and display them on a multi-floor map.
- Recover a shopper's location from nearby landmarks or an uploaded image.
- Keep and manage shopper conversation history.

### Mall and tenant operations

- Sign in as a mall administrator or tenant user with role-based access.
- Create and manage stores, tenants, users, navigation nodes, and navigation edges.
- Import text or files into permission-scoped knowledge collections.
- Draft, review, accept, reject, and publish AI-assisted knowledge changes.
- Convert floor-plan images or PDFs into editable graph drafts before publishing them to navigation.
- Upload promotion posters, extract promotion details, clarify missing information, and confirm promotions.
- View shopper-behavior metrics, daily summaries, AI-generated BI narratives, and BI chat history.

### Implementation

- `.NET 10` Web API using Clean Architecture projects for Domain, Application, Infrastructure, and Web API layers.
- React 19, TypeScript, Vite, Tailwind CSS, and daisyUI frontend.
- SQLite storage created automatically on first backend startup.
- Deterministic graph routing for physical directions; Gemini is used for language, retrieval, and image understanding.
- OpenAPI and Scalar API documentation in Development mode.

## Run locally

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) 22 or later with npm

The backend reads its application configuration only from `src/MallSense.WebApi/appsettings.json`. Set `Gemini:ApiKey` there to use live AI features, or leave it empty to use the built-in simulation. Set `Gemini:AllowSimulationFallback` to `true` if live API failures should also fall back to simulated responses.

### Development mode

From the repository root, start the API:

```powershell
dotnet run --project src/MallSense.WebApi
```

In a second terminal, install the frontend dependencies and start Vite:

```powershell
cd client
npm ci
npm run dev
```

Open the Vite URL shown in the terminal, normally [http://localhost:5173](http://localhost:5173). Vite proxies API requests to the backend at `http://localhost:5142`.

API documentation is available at [http://localhost:5142/scalar/v1](http://localhost:5142/scalar/v1).

### Run the built frontend from the API

Build the React app first:

```powershell
cd client
npm ci
npm run build
cd ..
dotnet run --project src/MallSense.WebApi
```

Then open [http://localhost:5142](http://localhost:5142). The API automatically serves `client/dist` when it exists.

### Run tests

```powershell
dotnet test MallSense.sln
```

### Seeded development accounts

The development database is seeded on first startup with these demo accounts:

| Role | Email | Password |
|---|---|---|
| Mall administrator | `admin@test.com` | `Admin123!` |
| Tenant user (SKECHERS) | `test@skechers.com` | `Tenant123!` |

The primary SQLite database is created as `src/MallSense.WebApi/kluangmall.db`. Delete that file and restart the backend if you need a fresh seeded database. The legacy `mallsense.db` file is not used by the application.
