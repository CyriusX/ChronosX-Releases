namespace TimeTrack.Backend.AI.Configuration;

public sealed class ClassificationJobOptions
{
    public const string SectionName = "ClassificationJob";

    public int MaxAppsPerRun { get; set; } = 50;
    public int RecentRejectionDays { get; set; } = 30;
}
