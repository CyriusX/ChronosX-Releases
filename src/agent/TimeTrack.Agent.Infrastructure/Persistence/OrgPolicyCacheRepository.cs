using Dapper;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// SQLite-backed cache for org policies (currently: idle threshold).
/// </summary>
public sealed class OrgPolicyCacheRepository : IOrgPolicyCacheRepository
{
    private readonly SqliteContext _context;
    private readonly ILogger<OrgPolicyCacheRepository> _logger;

    public OrgPolicyCacheRepository(SqliteContext context, ILogger<OrgPolicyCacheRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrgPolicyCacheEntry?> GetAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT org_id as OrgId,
                   idle_threshold_seconds as IdleThresholdSeconds,
                   idle_justification_prompt_threshold_seconds as IdleJustificationPromptThresholdSeconds,
                   version as Version,
                   updated_at as UpdatedAtUtc
            FROM org_policies_cache
            WHERE org_id = @OrgId
            LIMIT 1";

        var dto = await connection.QueryFirstOrDefaultAsync<OrgPolicyCacheDto>(sql, new { OrgId = orgId.ToString() });
        if (dto is null) return null;

        return new OrgPolicyCacheEntry
        {
            OrgId = Guid.Parse(dto.OrgId),
            IdleThresholdSeconds = dto.IdleThresholdSeconds,
            IdleJustificationPromptThresholdSeconds = dto.IdleJustificationPromptThresholdSeconds,
            Version = dto.Version,
            UpdatedAtUtc = DateTime.TryParse(dto.UpdatedAtUtc, out var parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : DateTime.UtcNow
        };
    }

    public async Task UpsertAsync(OrgPolicyCacheEntry entry, CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO org_policies_cache (org_id, idle_threshold_seconds, idle_justification_prompt_threshold_seconds, version, updated_at)
            VALUES (@OrgId, @IdleThresholdSeconds, @IdleJustificationPromptThresholdSeconds, @Version, @UpdatedAtUtc)
            ON CONFLICT(org_id) DO UPDATE SET
              idle_threshold_seconds = excluded.idle_threshold_seconds,
              idle_justification_prompt_threshold_seconds = excluded.idle_justification_prompt_threshold_seconds,
              version = excluded.version,
              updated_at = excluded.updated_at";

        await connection.ExecuteAsync(sql, new
        {
            OrgId = entry.OrgId.ToString(),
            IdleThresholdSeconds = entry.IdleThresholdSeconds,
            IdleJustificationPromptThresholdSeconds = entry.IdleJustificationPromptThresholdSeconds,
            Version = entry.Version,
            UpdatedAtUtc = entry.UpdatedAtUtc.ToString("o")
        });

        _logger.LogDebug(
            "Org policy cached: OrgId={OrgId}, IdleThreshold={IdleThresholdSeconds}s, IdleJustificationPromptThreshold={PromptThreshold}s, Version={Version}",
            entry.OrgId, entry.IdleThresholdSeconds, entry.IdleJustificationPromptThresholdSeconds, entry.Version);
    }

    private sealed class OrgPolicyCacheDto
    {
        public string OrgId { get; init; } = string.Empty;
        public int IdleThresholdSeconds { get; init; }
        public int? IdleJustificationPromptThresholdSeconds { get; init; }
        public int Version { get; init; }
        public string UpdatedAtUtc { get; init; } = string.Empty;
    }
}
