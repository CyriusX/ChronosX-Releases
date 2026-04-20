using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data migration: backfill a 14-day trialing subscription for every org that
    /// has no row in org_subscriptions. Before this migration, orgs created prior
    /// to AddSubscriptionTables (and any org created when RegisterCommand didn't
    /// yet create subscriptions) were hitting the paywall unconditionally.
    ///
    /// Prefers the Free plan; falls back to the Pro plan; leaves plan_id NULL if
    /// neither is seeded yet (the seeder runs on the next app start and admins
    /// can then attach a plan).
    ///
    /// status and tier are stored as strings (HasConversion&lt;string&gt;()).
    /// </summary>
    public partial class AddTrialSubscriptionBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO org_subscriptions
                    (id, org_id, plan_id, status, quantity, trial_end, created_at, updated_at)
                SELECT
                    gen_random_uuid(),
                    o.id,
                    COALESCE(
                        (SELECT id FROM subscription_plans WHERE tier = 'Free' LIMIT 1),
                        (SELECT id FROM subscription_plans WHERE tier = 'Pro'  LIMIT 1)
                    ),
                    'Trialing',
                    1,
                    now() + interval '14 days',
                    now(),
                    now()
                FROM orgs o
                WHERE NOT EXISTS (
                    SELECT 1 FROM org_subscriptions s WHERE s.org_id = o.id
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-reversible data backfill: removing the trialing subs created above
            // would re-break those orgs. Intentionally a no-op; roll forward with
            // another data migration if a reversal is ever needed.
        }
    }
}
