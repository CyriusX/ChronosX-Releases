namespace TimeTrack.Backend.Application.Storage.DTOs;

public sealed class StorageUsageResponse
{
    public long UsedBytes { get; init; }
    public long QuotaBytes { get; init; }
    public double Percentage { get; init; }
    public decimal QuotaGb { get; init; }
    public decimal UsedGb { get; init; }
}

public sealed class UpdateStorageQuotaRequest
{
    public decimal QuotaGb { get; init; }
}
