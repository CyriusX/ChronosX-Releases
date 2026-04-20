using Npgsql;

var connStr = "postgres://chronosx:chronosx@easypanel.cyriusx.com:6543/chronosx?sslmode=disable";
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

// Delete failed event logs so they can be reprocessed
await using var cmd = conn.CreateCommand();
cmd.CommandText = """
    DELETE FROM stripe_event_logs 
    WHERE stripe_event_id IN ('evt_1TNdwqRyoDZcfmM59nAwUbn5', 'evt_1TNdykRyoDZcfmM5kzr31ElI')
""";
var deleted = await cmd.ExecuteNonQueryAsync();
Console.WriteLine($"Deleted {deleted} event log entries");

// Check current subscription state
cmd.CommandText = """
    SELECT id, status, stripe_subscription_id, plan_id, current_period_start, current_period_end 
    FROM org_subscriptions 
    WHERE org_id = 'ded7a776-9a26-43ff-b635-f099df9dfece'
""";
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync()) {
    Console.WriteLine($"Sub: id={reader.GetGuid(0)}, status={reader[1]}, stripe_sub_id={reader.IsDBNull(2)?"null":reader.GetString(2)}, plan_id={reader.IsDBNull(3)?"null":reader[3]}, period_start={reader.IsDBNull(4)?"null":reader[4]}, period_end={reader.IsDBNull(5)?"null":reader[5]}");
}
