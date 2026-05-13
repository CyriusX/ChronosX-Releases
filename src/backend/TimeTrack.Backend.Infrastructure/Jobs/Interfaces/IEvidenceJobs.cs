namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

public interface IEvidenceRetentionJob
{
    Task ExecuteAsync();
}

public interface IStorageQuotaCheckJob
{
    Task ExecuteAsync();
}

public interface IOrphanCleanupJob
{
    Task ExecuteAsync();
}
