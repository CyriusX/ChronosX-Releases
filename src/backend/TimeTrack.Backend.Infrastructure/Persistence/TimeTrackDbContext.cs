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
    public DbSet<OrgPolicy> OrgPolicies => Set<OrgPolicy>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();

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
        // Query filters that bypass tenant filtering when there's no authenticated user
        // This is necessary for login/registration flows where the user context is not yet established
        // Note: _currentUser is always injected, but OrgId is null when not authenticated

        // Users
        modelBuilder.Entity<User>()
            .HasQueryFilter(u => !_currentUser.IsAuthenticated || u.OrgId == _currentUser.OrgId);

        // Devices
        modelBuilder.Entity<Device>()
            .HasQueryFilter(d => !_currentUser.IsAuthenticated || d.OrgId == _currentUser.OrgId);

        // Activity Sessions
        modelBuilder.Entity<ActivitySession>()
            .HasQueryFilter(a => !_currentUser.IsAuthenticated || a.OrgId == _currentUser.OrgId);

        // Idle Periods
        modelBuilder.Entity<IdlePeriod>()
            .HasQueryFilter(i => !_currentUser.IsAuthenticated || i.OrgId == _currentUser.OrgId);

        // Focus Sessions
        modelBuilder.Entity<FocusSession>()
            .HasQueryFilter(f => !_currentUser.IsAuthenticated || f.OrgId == _currentUser.OrgId);

        // Idempotency Keys
        modelBuilder.Entity<IdempotencyKey>()
            .HasQueryFilter(k => !_currentUser.IsAuthenticated || k.OrgId == _currentUser.OrgId);

        // Policies
        modelBuilder.Entity<Policy>()
            .HasQueryFilter(p => !_currentUser.IsAuthenticated || p.OrgId == _currentUser.OrgId);

        // Org Policies
        modelBuilder.Entity<OrgPolicy>()
            .HasQueryFilter(p => !_currentUser.IsAuthenticated || p.OrgId == _currentUser.OrgId);

        // Audit Logs
        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(a => !_currentUser.IsAuthenticated || a.OrgId == _currentUser.OrgId);

        // Projects
        modelBuilder.Entity<Project>()
            .HasQueryFilter(p => !_currentUser.IsAuthenticated || p.OrgId == _currentUser.OrgId);

        // Daily Summaries
        modelBuilder.Entity<DailySummary>()
            .HasQueryFilter(d => !_currentUser.IsAuthenticated || d.OrgId == _currentUser.OrgId);
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
