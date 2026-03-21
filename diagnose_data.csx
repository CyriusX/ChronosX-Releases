// Quick diagnostic script - run with: dotnet script diagnose_data.csx
// or copy the SQL queries to run directly

// SQL to check data:
/*
-- Check recent sessions with AppCategory
SELECT
    id,
    process_name,
    app_category,
    app_subcategory,
    duration_seconds,
    started_at
FROM activity_sessions
WHERE started_at >= CURRENT_DATE - INTERVAL '1 day'
ORDER BY started_at DESC
LIMIT 20;

-- Check what categories exist
SELECT DISTINCT app_category, COUNT(*) as count
FROM activity_sessions
WHERE started_at >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY app_category;

-- Check if there are ANY productive sessions
SELECT
    app_category,
    SUM(duration_seconds) as total_seconds
FROM activity_sessions
WHERE started_at >= CURRENT_DATE - INTERVAL '1 day'
GROUP BY app_category;
*/
