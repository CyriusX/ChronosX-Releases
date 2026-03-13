namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Generates weekly history data for dashboard
/// </summary>
public static class WeeklyHistoryGenerator
{
    public static object[] Generate()
    {
        var today = DateTime.Today;
        var history = new List<object>();

        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dayOfWeek = date.DayOfWeek;

            history.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                dayName = GetDayName(dayOfWeek),
                hours = i == 0 ? 0 : Random.Shared.Next(2, 9) + Random.Shared.NextDouble(),
                isToday = i == 0
            });
        }

        return history.ToArray();
    }

    private static string GetDayName(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Sunday => "Dom",
        DayOfWeek.Monday => "Seg",
        DayOfWeek.Tuesday => "Ter",
        DayOfWeek.Wednesday => "Qua",
        DayOfWeek.Thursday => "Qui",
        DayOfWeek.Friday => "Sex",
        DayOfWeek.Saturday => "Sáb",
        _ => "???"
    };
}
