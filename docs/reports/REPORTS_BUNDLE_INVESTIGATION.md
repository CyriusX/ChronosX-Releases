# Reports Bundle (500) — Investigation & Fix Tracker

## Goal
Stop `/api/v1/reports/bundle` from failing (500) on both Web UI and Desktop Host, and ensure all report sections load reliably.

## Status
- [x] Identified root cause for one class of 500s: parallel queries over a scoped EF Core `DbContext` (not thread-safe).
- [x] Made bundle queries sequential and added per-section timings.
- [x] Optimized `DailySummaryRange` to avoid O(days × sessions) behavior.
- [x] Added non-fatal partial-bundle support (`errors[]`) so a single failing section no longer breaks the entire page.
- [ ] Deploy backend + UI to production (EasyPanel) and confirm `/reports/bundle` returns 200.
- [ ] If `errors[]` is present: capture which section(s) failed and fix the underlying query/repository method.

## How to validate after deploy
1. Open Reports page (Web UI + Desktop Host).
2. Check that the page renders and no longer shows a hard failure.
3. If an error banner appears, it should list the failing sections (from `errors[]`).
4. In API logs, search for `ReportsBundle:` entries:
   - timings: `ReportsBundle: <Section> in <Ms>ms`
   - failures: `ReportsBundle: <Section> failed` (with exception details)

