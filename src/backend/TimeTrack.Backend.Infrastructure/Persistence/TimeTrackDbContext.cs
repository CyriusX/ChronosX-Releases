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
    public DbSet<OrgInviteLink> OrgInviteLinks => Set<OrgInviteLink>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TaskTimeEntry> TaskTimeEntries => Set<TaskTimeEntry>();
    public DbSet<AgentNotificationInbox> AgentNotificationInbox => Set<AgentNotificationInbox>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<DailyFocusScore> DailyFocusScores => Set<DailyFocusScore>();

    // Machine Metrics
    public DbSet<MachineMetrics> MachineMetrics => Set<MachineMetrics>();

    // Agent Event Logs
    public DbSet<AgentEventLog> AgentEventLogs => Set<AgentEventLog>();

    // Platform API keys (SysAdmin-only integrations)
    public DbSet<PlatformApiKey> PlatformApiKeys => Set<PlatformApiKey>();

    // Ops device issue transitions (SysAdmin monitoring)
    public DbSet<OpsDeviceIssueState> OpsDeviceIssueStates => Set<OpsDeviceIssueState>();

    // Platform-level events and health state (SysAdmin monitoring)
    public DbSet<PlatformEventLog> PlatformEventLogs => Set<PlatformEventLog>();
    public DbSet<PlatformHealthState> PlatformHealthState => Set<PlatformHealthState>();

    // Remote Commands
    public DbSet<RemoteCommand> RemoteCommands => Set<RemoteCommand>();

    // App Categories (CX-143)
    public DbSet<AppCategoryGlobal> AppCategoryGlobals => Set<AppCategoryGlobal>();
    public DbSet<AppCategoryOverride> AppCategoryOverrides => Set<AppCategoryOverride>();

    // Third-party integrations (Linear for v1)
    public DbSet<UserIntegration> UserIntegrations => Set<UserIntegration>();
    public DbSet<LinearSyncHistory> LinearSyncHistory => Set<LinearSyncHistory>();

    // Subscriptions & Billing
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<OrgSubscription> OrgSubscriptions => Set<OrgSubscription>();
    public DbSet<StripeEventLog> StripeEventLogs => Set<StripeEventLog>();
    public DbSet<OrgUsageRecord> OrgUsageRecords => Set<OrgUsageRecord>();
    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();

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
            .HasQueryFilter(p => (!_currentUser.IsAuthenticated || p.OrgId == _currentUser.OrgId) && p.DeletedAt == null);

        // Project Members
        modelBuilder.Entity<ProjectMember>()
            .HasQueryFilter(pm => !_currentUser.IsAuthenticated || pm.OrgId == _currentUser.OrgId);

        // Project Tasks (also exclude soft-deleted)
        modelBuilder.Entity<ProjectTask>()
            .HasQueryFilter(t => (!_currentUser.IsAuthenticated || t.OrgId == _currentUser.OrgId) && t.DeletedAt == null);

        // Task Time Entries
        modelBuilder.Entity<TaskTimeEntry>()
            .HasQueryFilter(e => !_currentUser.IsAuthenticated || e.OrgId == _currentUser.OrgId);

        // Notification Inbox
        modelBuilder.Entity<AgentNotificationInbox>()
            .HasQueryFilter(n => !_currentUser.IsAuthenticated || n.OrgId == _currentUser.OrgId);

        // Project Members
        modelBuilder.Entity<ProjectMember>()
            .HasQueryFilter(pm => !_currentUser.IsAuthenticated || pm.OrgId == _currentUser.OrgId);

        // Project Tasks (also exclude soft-deleted)
        modelBuilder.Entity<ProjectTask>()
            .HasQueryFilter(t => (!_currentUser.IsAuthenticated || t.OrgId == _currentUser.OrgId) && t.DeletedAt == null);

        // Task Time Entries
        modelBuilder.Entity<TaskTimeEntry>()
            .HasQueryFilter(e => !_currentUser.IsAuthenticated || e.OrgId == _currentUser.OrgId);

        // Notification Inbox
        modelBuilder.Entity<AgentNotificationInbox>()
            .HasQueryFilter(n => !_currentUser.IsAuthenticated || n.OrgId == _currentUser.OrgId);

        // Daily Summaries
        modelBuilder.Entity<DailySummary>()
            .HasQueryFilter(d => !_currentUser.IsAuthenticated || d.OrgId == _currentUser.OrgId);

        // Daily Focus Scores
        modelBuilder.Entity<DailyFocusScore>()
            .HasQueryFilter(d => !_currentUser.IsAuthenticated || d.OrgId == _currentUser.OrgId);

        // App Category Overrides (CX-143)
        modelBuilder.Entity<AppCategoryOverride>()
            .HasQueryFilter(o => !_currentUser.IsAuthenticated || o.OrgId == _currentUser.OrgId);

        // Machine Metrics
        modelBuilder.Entity<MachineMetrics>()
            .HasQueryFilter(m => !_currentUser.IsAuthenticated || m.OrgId == _currentUser.OrgId);

        // Agent Event Logs
        modelBuilder.Entity<AgentEventLog>()
            .HasQueryFilter(e => !_currentUser.IsAuthenticated || e.OrgId == _currentUser.OrgId);

        // Remote Commands
        modelBuilder.Entity<RemoteCommand>()
            .HasQueryFilter(rc => !_currentUser.IsAuthenticated || rc.OrgId == _currentUser.OrgId);

        // User Integrations (Linear etc.)
        modelBuilder.Entity<UserIntegration>()
            .HasQueryFilter(i => !_currentUser.IsAuthenticated || i.OrgId == _currentUser.OrgId);

        // Linear Sync History
        modelBuilder.Entity<LinearSyncHistory>()
            .HasQueryFilter(h => !_currentUser.IsAuthenticated || h.OrgId == _currentUser.OrgId);

        // Org Subscriptions (tenant-scoped)
        modelBuilder.Entity<OrgSubscription>()
            .HasQueryFilter(s => !_currentUser.IsAuthenticated || s.OrgId == _currentUser.OrgId);

        // Stripe Event Logs (tenant-scoped)
        modelBuilder.Entity<StripeEventLog>()
            .HasQueryFilter(e => !_currentUser.IsAuthenticated || e.OrgId == _currentUser.OrgId);

        // Org Usage Records (tenant-scoped)
        modelBuilder.Entity<OrgUsageRecord>()
            .HasQueryFilter(r => !_currentUser.IsAuthenticated || r.OrgId == _currentUser.OrgId);

        // Billing Invoices (tenant-scoped)
        modelBuilder.Entity<BillingInvoice>()
            .HasQueryFilter(i => !_currentUser.IsAuthenticated || i.OrgId == _currentUser.OrgId);

        // Org Invite Links (tenant-scoped; GetByTokenHashAsync bypasses via IgnoreQueryFilters)
        modelBuilder.Entity<OrgInviteLink>()
            .HasQueryFilter(l => !_currentUser.IsAuthenticated || l.OrgId == _currentUser.OrgId);
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
            var createdAtProperty = entry.Metadata.FindProperty("CreatedAt");
            if (entry.State == EntityState.Added && createdAtProperty != null)
            {
                var currentValue = entry.Property("CreatedAt").CurrentValue;
                if (currentValue == null || (currentValue is DateTime dt && dt == default))
                {
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
                }
            }

            var updatedAtProperty = entry.Metadata.FindProperty("UpdatedAt");
            if (entry.State == EntityState.Modified && updatedAtProperty != null)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }
    }
}
