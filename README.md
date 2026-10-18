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

## Container demo on Google Cloud Run

The root `Dockerfile` builds React and publishes the .NET API into one Linux image. Run all commands below from the repository root. Building this image compiles both applications; these are instructions to run when a build is wanted.

Application configuration remains exclusively in `src/MallSense.WebApi/appsettings.json`. The image copies those settings and adjusts three container-specific values in its own JSON file: SQLite uses `/tmp/kluangmall.db`, Kestrel listens on `0.0.0.0:8080`, and `Hosting:HttpsRedirectionEnabled` is `false` because Cloud Run handles external HTTPS. Local settings are unchanged. No environment configuration overrides, Secret Manager, or Cloud SQL are required for this demo.

Local databases are excluded from the image. A fresh container creates and seeds a new database. All subsequent database changes are disposable and can disappear after idle scale-down, deployments, crashes, or platform restarts. Minimum and maximum instance settings of `1` reduce turnover and independent database copies but do not guarantee persistence or prevent replacement overlap during deployments.

The existing BI background scheduler remains enabled. It attempts to create yesterday's missing summaries at startup and runs at Malaysia midnight while CPU is available. With Cloud Run's default request-based billing, generation may be delayed until traffic resumes. Opening the BI page does not directly trigger generation; a refresh may be needed once it finishes.

Settings, including any API keys, are included in the image. Use demo credentials and keep the Artifact Registry repository private. The deployed shopper website is public, while admin APIs retain application authentication.

### Build and try locally

Install Docker with Linux container support, then run:

```powershell
docker build -t mallsense:demo .
docker run --rm -p 8080:8080 mallsense:demo
```

Open [http://localhost:8080](http://localhost:8080). Stopping this disposable container loses its database.

### Build and deploy on Google Cloud

Install the Google Cloud CLI, sign in, and choose a project with billing enabled. Replace `YOUR_PROJECT_ID` below. These commands create a repository, submit a remote container build, and deploy a public service; they incur Google Cloud usage charges.

```powershell
$projectId = "YOUR_PROJECT_ID"
$region = "asia-southeast1"
$image = "${region}-docker.pkg.dev/${projectId}/mallsense/app:demo"

gcloud auth login
gcloud config set project $projectId
gcloud services enable run.googleapis.com cloudbuild.googleapis.com artifactregistry.googleapis.com

# One-time repository setup; skip if it already exists.
gcloud artifacts repositories create mallsense --repository-format=docker --location=$region

gcloud builds submit --tag $image .
gcloud run deploy mallsense --image $image --region $region --port 8080 --min=1 --max=1 --memory=1Gi --timeout=900 --allow-unauthenticated
```

The caller and Cloud Build service account need the relevant build, Artifact Registry, deployment, and service-account permissions. The deploy command prints the service URL. The 900-second request timeout accommodates the configured 600-second Gemini timeout. Minimum one instance incurs idle-instance charges; use `--min=0` if cold starts and more frequent demo resets are acceptable.

To update the demo, rerun the build and deploy commands. Expect the deployed database to reset. Smoke-check the directory, admin login, a saved edit, and BI page after deployment.

References: [Cloud Run container contract](https://docs.cloud.google.com/run/docs/container-contract), [deploying images](https://docs.cloud.google.com/run/docs/deploying), and [background activity](https://docs.cloud.google.com/run/docs/tips/general).
