namespace TimeTrack.Backend.AI.Configuration;

public sealed class NarrativeJobOptions
{
    public const string SectionName = "NarrativeJob";

    public int MaxUsersPerRun { get; set; } = 100;
    public int MinimumDataDays { get; set; } = 3;
}
