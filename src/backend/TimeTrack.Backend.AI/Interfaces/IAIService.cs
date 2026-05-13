namespace TimeTrack.Backend.AI.Interfaces;

public interface IAIService
{
    Task<ClassificationResult> ClassifyAppAsync(
        AppClassificationRequest request,
        Guid orgId,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<string> GenerateWeeklyNarrativeAsync(
        WeeklyFeatureContext context,
        Guid orgId,
        Guid userId,
        CancellationToken ct = default);

    Task<string> DescribePatternAsync(
        UserPatternContext pattern,
        Guid orgId,
        Guid userId,
        CancellationToken ct = default);

    Task<string> GenerateAlertMessageAsync(
        AlertContext context,
        Guid orgId,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<string> GenerateReportsSuggestionAsync(
        ReportsSuggestionContext context,
        Guid orgId,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<string> GenerateWeeklyEmailReportAsync(
        WeeklyEmailReportContext context,
        Guid orgId,
        Guid userId,
        CancellationToken ct = default);

    void InvalidateClassificationCache(string exeName);
}
