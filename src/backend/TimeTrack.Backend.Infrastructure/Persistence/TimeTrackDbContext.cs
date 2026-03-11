using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence;

/// <summary>
/// DbContext principal do TimeTrack
/// </summary>
public sealed class TimeTrackDbContext : DbContext
{
    private readonly ICurrentUserContext? _currentUser;

    public TimeTrackDbContext(
        DbContextOptions<TimeTrackDbContext> options,
        ICurrentUserContext? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // DbSets
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ActivitySession> ActivitySessions => Set<ActivitySession>();
    public DbSet<IdlePeriod> IdlePeriods => Set<IdlePeriod>();
    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimeTrackDbContext).Assembly);

        // Global query filter for multi-tenancy (applied per entity type)
        ConfigureMultiTenantFilters(modelBuilder);
    }

    private void ConfigureMultiTenantFilters(ModelBuilder modelBuilder)
    {
        var orgId = _currentUser?.OrgId ?? Guid.Empty;

        // Users
        modelBuilder.Entity<User>()
            .HasQueryFilter(u => u.OrgId == orgId);

        // Devices
        modelBuilder.Entity<Device>()
            .HasQueryFilter(d => d.OrgId == orgId);

        // Activity Sessions
        modelBuilder.Entity<ActivitySession>()
            .HasQueryFilter(a => a.OrgId == orgId);

        // Idle Periods
        modelBuilder.Entity<IdlePeriod>()
            .HasQueryFilter(i => i.OrgId == orgId);

        // Focus Sessions
        modelBuilder.Entity<FocusSession>()
            .HasQueryFilter(f => f.OrgId == orgId);

        // Idempotency Keys
        modelBuilder.Entity<IdempotencyKey>()
            .HasQueryFilter(k => k.OrgId == orgId);

        // Policies
        modelBuilder.Entity<Policy>()
            .HasQueryFilter(p => p.OrgId == orgId);

        // Audit Logs
        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(a => a.OrgId == orgId);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added && entry.Property("CreatedAt").CurrentValue == null)
            {
                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified && entry.Property("UpdatedAt") != null)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }
    }
}
