-- Check recent activity sessions with AppCategory
SELECT 
    DATE(started_at) as date,
    app_category,
    COUNT(*) as session_count,
    SUM(duration_seconds) as total_seconds
FROM activity_sessions
WHERE started_at >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY DATE(started_at), app_category
ORDER BY date DESC, total_seconds DESC;
