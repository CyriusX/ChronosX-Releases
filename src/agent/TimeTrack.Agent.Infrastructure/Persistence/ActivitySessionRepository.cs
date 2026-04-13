using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Infrastructure.Persistence
{
    /// <summary>
    /// Implementação SQLite do repositório de sessões de atividade
    /// </summary>
    public sealed class ActivitySessionRepository : IActivitySessionRepository
    {
        private readonly SqliteContext _context;
        private readonly IOutboxRepository _outboxRepository;
        private readonly ILogger<ActivitySessionRepository> _logger;

        public ActivitySessionRepository(
            SqliteContext context,
            IOutboxRepository outboxRepository,
            ILogger<ActivitySessionRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SaveWithOutboxAsync(
            ActivitySession session,
            IEnumerable<OutboxItem> outboxItems,
            CancellationToken cancellationToken = default)
        {
            var connection = await _context.GetConnectionAsync(cancellationToken);
            var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Salvar sessão
                const string sessionSql = @"
                    INSERT OR REPLACE INTO activity_sessions
                        (id, user_id, exe_path_hash, display_name, category_productivity,
                         category_subcategory, category_source, start_utc, end_utc,
                         window_hash, window_title, domain)
                    VALUES
                        (@Id, @UserId, @ExePathHash, @DisplayName, @CategoryProductivity,
                         @CategorySubcategory, @CategorySource, @StartUtc, @EndUtc,
                         @WindowHash, @WindowTitle, @Domain)
                ";

                await connection.ExecuteAsync(sessionSql, new
                {
                    Id = session.Id.ToString(),
                    UserId = session.UserId.ToString(),
                    ExePathHash = session.App.ExePathHash,
                    DisplayName = session.App.DisplayName,
                    CategoryProductivity = session.App.Category.Productivity,
                    CategorySubcategory = session.App.Category.Subcategory,
                    CategorySource = session.App.Category.Source,
                    StartUtc = session.Period.StartUtc,
                    EndUtc = session.Period.EndUtc,
                    WindowHash = session.WindowHash,
                    WindowTitle = session.WindowTitle,
                    Domain = session.Domain
                });

                // 2. Salvar outbox items
                const string outboxSql = @"
                    INSERT OR IGNORE INTO sync_outbox
                        (id, user_id, entity_type, entity_id, payload_json, idempotency_key,
                         attempt_count, next_attempt_utc, sent_at, last_error, created_at)
                    VALUES
                        (@Id, @UserId, @EntityType, @EntityId, @PayloadJson, @IdempotencyKey,
                         @AttemptCount, @NextAttemptUtc, @SentAt, @LastError, @CreatedAt)
                ";

                var itemList = outboxItems.ToList();
                foreach (var item in itemList)
                {
                    _logger.LogDebug(
                        "Saving outbox item: Id={Id}, EntityType={EntityType}, EntityId={EntityId}",
                        item.Id, item.EntityType, item.EntityId);

                    await connection.ExecuteAsync(outboxSql, new
                    {
                        Id = item.Id.ToString(),
                        UserId = session.UserId.ToString(),
                        EntityType = item.EntityType,
                        EntityId = item.EntityId.ToString(),
                        PayloadJson = item.PayloadJson,
                        IdempotencyKey = item.IdempotencyKey,
                        AttemptCount = item.AttemptCount,
                        NextAttemptUtc = item.NextAttemptUtc?.ToString("o"),
                        SentAt = item.SentAt?.ToString("o"),
                        LastError = item.LastError,
                        CreatedAt = item.CreatedAt.ToString("o")
                    });
                }

                await transaction.CommitAsync();

                _logger.LogDebug("Activity session {SessionId} saved with {Count} outbox items", session.Id, itemList.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to save activity session with outbox items");
                throw;
            }
        }

        public async Task<IReadOnlyList<ActivitySession>> GetByDateAsync(
            Guid userId,
            DateTime date,
            CancellationToken cancellationToken = default)
        {
            // Convert local date to UTC boundaries so that "today" in the user's
            // timezone maps correctly to UTC-stored start_utc values.
            var localDay = date.Date;
            var startOfDayUtc = localDay.Kind == DateTimeKind.Utc
                ? localDay
                : localDay.ToUniversalTime();
            var endOfDayUtc = startOfDayUtc.AddDays(1);

            return await GetByDateRangeAsync(userId, startOfDayUtc, endOfDayUtc, cancellationToken);
        }

        public async Task<IReadOnlyList<ActivitySession>> GetByDateRangeAsync(
            Guid userId,
            DateTime start,
            DateTime end,
            CancellationToken cancellationToken = default)
        {
            var connection = await _context.GetConnectionAsync(cancellationToken);

            const string sql = @"
            SELECT id, user_id, exe_path_hash, display_name, category_productivity,
                   category_subcategory, category_source, start_utc, end_utc,
                   window_hash, window_title, domain
            FROM activity_sessions
            WHERE user_id = @UserId AND start_utc < @End AND end_utc > @Start
            ORDER BY start_utc";

            var dtos = await connection.QueryAsync<ActivitySessionDto>(sql, new { UserId = userId.ToString(), Start = start, End = end });

            return dtos.Select(MapToDomain).ToList();
        }

        public async Task<ActivitySession?> GetMostRecentAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var connection = await _context.GetConnectionAsync(cancellationToken);

            const string sql = @"
            SELECT id, user_id, exe_path_hash, display_name, category_productivity,
                   category_subcategory, category_source, start_utc, end_utc,
                   window_hash, window_title, domain
            FROM activity_sessions
            WHERE user_id = @UserId
            ORDER BY end_utc DESC
            LIMIT 1";

            var dto = await connection.QueryFirstOrDefaultAsync<ActivitySessionDto>(sql, new { UserId = userId.ToString() });

            return dto != null ? MapToDomain(dto) : null;
        }

        public async Task<ActivitySession?> GetActiveSessionAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var connection = await _context.GetConnectionAsync(cancellationToken);

            // Uma sessão "ativa" é aquela que terminou nos últimos 60 segundos
            // ( threshold para permitir pequenas pausas sem criar nova sessão)
            var endThreshold = DateTime.UtcNow.AddSeconds(-60);

            const string sql = @"
            SELECT id, user_id, exe_path_hash, display_name, category_productivity,
                   category_subcategory, category_source, start_utc, end_utc,
                   window_hash, window_title, domain
            FROM activity_sessions
            WHERE user_id = @UserId AND end_utc >= @EndThreshold
            ORDER BY end_utc DESC
            LIMIT 1";

            var dto = await connection.QueryFirstOrDefaultAsync<ActivitySessionDto>(sql, new { UserId = userId.ToString(), EndThreshold = endThreshold });

            return dto != null ? MapToDomain(dto) : null;
        }

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task UpdateAsync(ActivitySession session, CancellationToken cancellationToken = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var connection = await _context.GetConnectionAsync(cancellationToken);
            var transaction = await _context.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Update session end_utc
                const string sessionSql = @"
                UPDATE activity_sessions
                SET end_utc = @EndUtc
                WHERE id = @Id
                ";

                await connection.ExecuteAsync(sessionSql, new
                {
                    Id = session.Id.ToString(),
                    EndUtc = session.Period.EndUtc
                });

                // 2. Update outbox payload — try pending item first, create new if already sent
                var newPayloadJson = JsonSerializer.Serialize(new
                {
                    id = session.Id,
                    exePathHash = session.App.ExePathHash,
                    displayName = session.App.DisplayName,
                    categoryProductivity = session.App.Category.Productivity,
                    categorySubcategory = session.App.Category.Subcategory,
                    categorySource = session.App.Category.Source,
                    startUtc = session.Period.StartUtc,
                    endUtc = session.Period.EndUtc,
                    windowHash = session.WindowHash,
                    windowTitle = session.WindowTitle,
                    domain = session.Domain
                }, _jsonOptions);

                const string updateOutboxSql = @"
                UPDATE sync_outbox
                SET payload_json = @PayloadJson,
                    next_attempt_utc = COALESCE(next_attempt_utc, @NextAttemptUtc)
                WHERE entity_id = @EntityId AND sent_at IS NULL
                ";

                var rowsUpdated = await connection.ExecuteAsync(updateOutboxSql, new
                {
                    PayloadJson = newPayloadJson,
                    EntityId = session.Id.ToString(),
                    NextAttemptUtc = DateTime.UtcNow.ToString("o")
                });

                // If no pending outbox item was updated, the previous one was already sent.
                // Create a new outbox item so the backend receives the extended end_utc.
                if (rowsUpdated == 0)
                {
                    var idempotencyKey = $"as:{session.Id}:{session.Period.EndUtc:yyyyMMddHHmmss}";

                    const string insertOutboxSql = @"
                    INSERT OR IGNORE INTO sync_outbox
                        (id, user_id, entity_type, entity_id, payload_json, idempotency_key,
                         attempt_count, next_attempt_utc, sent_at, last_error, created_at)
                    VALUES
                        (@Id, @UserId, 'activity_session', @EntityId, @PayloadJson, @IdempotencyKey,
                         0, @NextAttemptUtc, NULL, NULL, @CreatedAt)
                    ";

                    await connection.ExecuteAsync(insertOutboxSql, new
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = session.UserId.ToString(),
                        EntityId = session.Id.ToString(),
                        PayloadJson = newPayloadJson,
                        IdempotencyKey = idempotencyKey,
                        NextAttemptUtc = DateTime.UtcNow.ToString("o"),
                        CreatedAt = DateTime.UtcNow.ToString("o")
                    });

                    _logger.LogDebug(
                        "Created new outbox item for already-synced session {SessionId} (extended to {EndUtc})",
                        session.Id, session.Period.EndUtc);
                }

                await transaction.CommitAsync();

                _logger.LogDebug("Activity session updated: {SessionId}, new end_utc: {EndUtc}", session.Id, session.Period.EndUtc);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update activity session {SessionId}", session.Id);
                throw;
            }
        }

        public async Task SaveAsync(ActivitySession session, CancellationToken cancellationToken = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var connection = await _context.GetConnectionAsync(cancellationToken);

            const string sql = @"
            INSERT OR REPLACE INTO activity_sessions
                (id, user_id, exe_path_hash, display_name, category_productivity,
                 category_subcategory, category_source, start_utc, end_utc,
                 window_hash, window_title)
            VALUES
                (@Id, @UserId, @ExePathHash, @DisplayName, @CategoryProductivity,
                 @CategorySubcategory, @CategorySource, @StartUtc, @EndUtc,
                 @WindowHash, @WindowTitle)
            ";

            await connection.ExecuteAsync(sql, new
            {
                Id = session.Id.ToString(),
                UserId = session.UserId.ToString(),
                ExePathHash = session.App.ExePathHash,
                DisplayName = session.App.DisplayName,
                CategoryProductivity = session.App.Category.Productivity,
                CategorySubcategory = session.App.Category.Subcategory,
                CategorySource = session.App.Category.Source,
                StartUtc = session.Period.StartUtc,
                EndUtc = session.Period.EndUtc,
                WindowHash = session.WindowHash,
                WindowTitle = session.WindowTitle
            });

            _logger.LogDebug("Activity session saved: {Session}", session);
        }

        public async Task SaveBatchAsync(
            IEnumerable<ActivitySession> sessions,
            CancellationToken cancellationToken = default)
        {
            if (sessions == null) throw new ArgumentNullException(nameof(sessions));

            var connection = await _context.GetConnectionAsync(cancellationToken);

            const string sql = @"
            INSERT OR REPLACE INTO activity_sessions
                (id, user_id, exe_path_hash, display_name, category_productivity,
                 category_subcategory, category_source, start_utc, end_utc,
                 window_hash, window_title)
            VALUES
                (@Id, @UserId, @ExePathHash, @DisplayName, @CategoryProductivity,
                 @CategorySubcategory, @CategorySource, @StartUtc, @EndUtc,
                 @WindowHash, @WindowTitle)
            ";

            var parameters = sessions.Select(s => new
            {
                Id = s.Id.ToString(),
                UserId = s.UserId.ToString(),
                ExePathHash = s.App.ExePathHash,
                DisplayName = s.App.DisplayName,
                CategoryProductivity = s.App.Category.Productivity,
                CategorySubcategory = s.App.Category.Subcategory,
                CategorySource = s.App.Category.Source,
                StartUtc = s.Period.StartUtc,
                EndUtc = s.Period.EndUtc,
                WindowHash = s.WindowHash,
                WindowTitle = s.WindowTitle,
                Domain = s.Domain
            });

            await connection.ExecuteAsync(sql, parameters);

            _logger.LogDebug("Batch of {Count} activity sessions saved", parameters.Count());
        }

        public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        {
            var connection = await _context.GetConnectionAsync(cancellationToken);

            // Use date() function for reliable comparison — SQLite stores dates with space separator
            // which breaks string comparison against ISO 8601 'T' separator
            const string sql = "DELETE FROM activity_sessions WHERE date(start_utc) < date(@Cutoff)";
            var deleted = await connection.ExecuteAsync(sql, new { Cutoff = cutoffUtc.ToString("yyyy-MM-dd") });

            if (deleted > 0)
                _logger.LogInformation("Cleaned up {Count} old activity sessions (before {Cutoff:yyyy-MM-dd})", deleted, cutoffUtc);

            return deleted;
        }

        public async Task<int> UpdateCategoryByDisplayNameAsync(
            string displayName,
            string newProductivity,
            string newSubcategory,
            string source,
            CancellationToken cancellationToken = default)
        {
            var connection = await _context.GetConnectionAsync(cancellationToken);

            const string sql = @"UPDATE activity_sessions
                                 SET Category_Productivity = @Productivity,
                                     Category_Subcategory = @Subcategory,
                                     Category_Source = @Source
                                 WHERE Display_Name = @DisplayName";

            var updated = await connection.ExecuteAsync(sql, new
            {
                DisplayName = displayName,
                Productivity = newProductivity,
                Subcategory = newSubcategory,
                Source = source
            });

            if (updated > 0)
                _logger.LogInformation(
                    "Updated {Count} sessions for '{DisplayName}': productivity={Productivity}, subcategory={Subcategory}",
                    updated, displayName, newProductivity, newSubcategory);

            return updated;
        }

        private static ActivitySession MapToDomain(ActivitySessionDto dto)
        {
            var category = new AppCategory(
                dto.Category_Productivity,
                dto.Category_Subcategory,
                dto.Category_Source);

            var app = new AppIdentity(dto.Exe_Path_Hash, dto.Display_Name, category);
            var period = new TimeRange(dto.Start_Utc, dto.End_Utc);

            return new ActivitySession(
                Guid.Parse(dto.Id),
                Guid.Parse(dto.User_Id),
                app,
                period,
                dto.Window_Hash,
                dto.Window_Title,
                domain: dto.Domain
            );
        }

        /// <summary>
        /// DTO interno para mapeamento Dapper
        /// </summary>
        private sealed class ActivitySessionDto
        {
            public string Id { get; set; } = string.Empty;
            public string User_Id { get; set; } = string.Empty;
            public string Exe_Path_Hash { get; set; } = string.Empty;
            public string Display_Name { get; set; } = string.Empty;
            public string Category_Productivity { get; set; } = string.Empty;
            public string Category_Subcategory { get; set; } = string.Empty;
            public string Category_Source { get; set; } = string.Empty;
            public DateTime Start_Utc { get; set; }
            public DateTime End_Utc { get; set; }
            public string? Window_Hash { get; set; }
            public string? Window_Title { get; set; }
            public string? Domain { get; set; }
        }
    }
}
