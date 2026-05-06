# Activities Page Slow Load — Investigation & Fix Tracker

## Goal
Reduce Activities page initial load time (Web UI + Desktop Host). Target: data visible within a few seconds, not ~1 minute.

## Hypothesis (most likely)
The Activities page performed multiple report requests concurrently (calendar range + top apps + daily sessions). The Reports rate limiter can return 429, and the UI retries with `Retry-After`, leading to long stalls.

## Fix implemented
- Fetch the three endpoints **sequentially** (not `Promise.all`) to avoid tripping the rate limiter and accumulating retry delays.

## Next steps if still slow
1. Check browser/desktop console for `429` warnings from `apiClient` and confirm if rate limiting is still the cause.
2. If responses are slow without 429:
   - Profile backend queries for `/api/v1/reports/activities` and add/adjust DB indexes.
   - Consider adding a composite endpoint (activities bundle) to reduce round-trips further.

