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
                         window_hash, window_title)
                    VALUES
                        (@Id, @UserId, @ExePathHash, @DisplayName, @CategoryProductivity,
                         @CategorySubcategory, @CategorySource, @StartUtc, @EndUtc,
                         @WindowHash, @WindowTitle)
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
                    WindowTitle = session.WindowTitle
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
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            return await GetByDateRangeAsync(userId, startOfDay, endOfDay, cancellationToken);
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
                   window_hash, window_title
            FROM activity_sessions
            WHERE user_id = @UserId AND start_utc >= @Start AND start_utc < @End
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
                   window_hash, window_title
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
            var threshold = DateTime.UtcNow.AddSeconds(-60);

            const string sql = @"
            SELECT id, user_id, exe_path_hash, display_name, category_productivity,
                   category_subcategory, category_source, start_utc, end_utc,
                   window_hash, window_title
            FROM activity_sessions
            WHERE user_id = @UserId AND end_utc >= @Threshold
            ORDER BY end_utc DESC
            LIMIT 1";

            var dto = await connection.QueryFirstOrDefaultAsync<ActivitySessionDto>(sql, new { UserId = userId.ToString(), Threshold = threshold });

            return dto != null ? MapToDomain(dto) : null;
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
                WindowTitle = s.WindowTitle
            });

            await connection.ExecuteAsync(sql, parameters);

            _logger.LogDebug("Batch of {Count} activity sessions saved", parameters.Count());
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
                dto.Window_Title
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
        }
    }
}
