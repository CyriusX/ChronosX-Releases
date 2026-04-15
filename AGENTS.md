# Repository Guidelines

## Project Structure & Module Organization

TimeTracking is a time-tracking system with three main components organized under `src/`:

- **Agent** (`src/agent/`) — Windows desktop agent (`.NET 8`) with service and desktop host
- **Backend** (`src/backend/`) — Cloud REST API (`ASP.NET Core 8`) with PostgreSQL
- **UI** (`src/ui/timetrack-ui/`) — React SPA (`Vite + TypeScript`) embedded in WebView2
- **Shared** (`shared/`) — Shared DDD kernel and IPC protocol contracts

Each component follows Clean Architecture layers: `Domain → Application → Infrastructure → Entry point`

Tests are colocated in their respective subsystems (`TimeTrack.Backend.Tests`, `TimeTrack.Agent.Tests`, `__tests__/` for UI).

## Build, Test, and Development Commands

### Backend (.NET 8)
```bash
dotnet restore TimeTrack.sln && dotnet build TimeTrack.sln
dotnet test TimeTrack.sln --filter "FullyQualifiedName~<TestName>"
dotnet ef database update --project src/backend/TimeTrack.Backend.Infrastructure --startup-project src/backend/TimeTrack.Api
dotnet run --project src/backend/TimeTrack.Api
```

### Frontend (from `src/ui/timetrack-ui/`)
```bash
npm install && npm run dev          # Dev server on port 5173
npm run build                       # tsc -b && vite build
npm run lint                        # ESLint
npm run test                        # Vitest run once
npm run test:watch                  # Watch mode
npm run test:coverage               # With coverage report
```

### Docker
```bash
docker-compose up                   # Starts PostgreSQL + API
```

## Coding Style & Naming Conventions

- **Indentation**: 4 spaces for C#, 2 spaces for TypeScript
- **C#**: PascalCase for public members, camelCase for private, _camelCase for fields
- **TypeScript/React**: PascalCase for components, camelCase for functions/variables
- **DDD**: Follow existing patterns (AggregateRoot, ValueObject, DomainEvent)
- **Formatting**: No explicit formatter configured — follow existing code style

## Testing Guidelines

- **Frameworks**: xUnit (backend/agent), Vitest + @testing-library/react (UI)
- **Coverage**: Aim for meaningful coverage on critical business logic
- **Naming**: `Should_ExpectedBehavior_When_StateUnderTest` format for unit tests
- **UI tests**: Colocate `*.test.ts` files with source or in `__tests__/` subdirs

## Commit & Pull Request Guidelines

- **Commit messages**: Conventional commits (`feat:`, `fix:`, `refactor:`, `docs:`) based on recent history
- **Branch naming**: Feature branches from `dev`, PRs target `main`
- **PR requirements**: Include description, linked issues if applicable
- **CI**: GitHub Actions on `windows-latest` runs tests on push to `main` and PRs

## Key Configuration Points

- Backend uses environment variables or `appsettings.Development.json` for connection strings, JWT secrets, and API keys
- Agent uses environment variables for process priority settings
- UI uses `.env.local` for local development overrides
- Never commit secrets, connection strings, or API keys

## Architecture Notes

- Multi-tenancy enforced via global `OrgId` query filters in `TimeTrackDbContext`
- Offline-first architecture with outbox pattern for reliable sync
- IPC via Named Pipes between AgentService and DesktopHost (contracts in `TimeTrack.Protocol`)
- Hangfire manages background jobs for sync, reports, and focus scores
