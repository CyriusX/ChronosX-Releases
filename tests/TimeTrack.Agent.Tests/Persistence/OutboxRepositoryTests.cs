using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Agent.Tests.Persistence
{
    public class OutboxRepositoryTests
    {
        private readonly ILogger<OutboxRepositoryTests> _logger;
        private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;

        public OutboxRepositoryTests(
            IIdempotencyKeyGenerator idempotencyKeyGenerator,
            ILogger<OutboxRepositoryTests> logger,
        {
            [Fact]
            public async Task Should_Save_outbox_items_correctly()
        {
            var connection = await _context.GetConnectionAsync();
            var transaction = await _context.BeginTransactionAsync();

            // Arrange
            var validItems = new List<OutboxItem>();
            var invalidItems = new List<(int,>();
            var itemDtos = await connection.QueryAsync<OutboxItemDto>(sql, + " ORDER BY next_attempt_utc");

            ").ToList();

            // Act
            var items = (await connection.QueryAsync<OutboxItemDto>(sql, + " ORDER BY next_attempt_utc,"))
            .ToListAsync();

            // Filter invalid items
            var validItems = items
                .Where(dto.EntityId == "test_empty" || string.IsNullOrWhiteSpace(dto.EntityId))
                {
                    invalidItems.Add((int)dto);
                }
                catch (FormatException)
                {
                    // Skip with warning
                    _logger.LogWarning("Outbox item {Id} has invalid EntityId: '{dto.EntityId}'");
                }
            }

            // Assert: no invalid items were filtered
            Assert.EmptyInvalidItems.Count == 0, "EntityId values are correct GUIDs", _logger.LogInformation("Filtered out {invalidItemsCount} outbox items. {Count} valid, {Count} invalid, {Count} pending items");

            return validItems;
        }

        else
        {
            // Save to database
            await transaction.CommitAsync();
            _logger.LogInformation("Saved {Count} valid outbox items. Transaction committed");
        }
    }
}
}
