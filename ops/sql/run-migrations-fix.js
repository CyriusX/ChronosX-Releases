const pg = require('pg');

const connectionString = 'postgres://postgres:2ffc8a164abfaf63657e@easypanel.cyriusx.com:6544/chronosx?sslmode=disable';

const migrations = [
    '20260310200919_InitialCreate',
    '20260312123906_AddProjectsTable',
    '20260313002015_CreateOrgPoliciesTable',
    '20260314214805_AddAppCategoryAndFocusScoreTables',
    '20260321181926_AddFilePathToActivitySession',
    '20260321203000_AddAppSubcategoryToActivitySession',
    '20260403034048_AddMachineMetricsTable',
    '20260403140618_AddAgentEventLogsTable',
    '20260403144423_AddRemoteCommandsAndDeviceInfo',
    '20260409224255_AddDeviceHealthFields',
    '20260410185330_AddTasksAndMembers',
    '20260411030501_AddLinearIntegrationAndDeadlines',
    '20260411040000_AddBillableProjectFields',
    '20260411050000_AddLinearOAuthFields',
    '20260412120000_AddActivitySessionUserIdIndex',
    '20260415120000_ExpandTaskDescriptionTo5000',
    '20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes',
    '20260417120000_AddProjectCreatorAndSoftDelete',
    '20260418145616_AddSubscriptionTables',
    '20260418185156_AddDevToolsEnabledUntilUtcToUsers',
    '20260418231811_AddBillingInvoices',
    '20260419202623_AddRefundFieldsToBillingInvoice',
    '20260420211819_AddTrialSubscriptionBackfill',
    '20260420224132_AddIsPlatformAdminToUsers',
    '20260423184243_AddEvidencePolicyFields',
    '20260423190525_AddEvidenceItemsAndStorageKeys',
    '20260423201010_AddStorageQuotaFields',
    '20260425130000_AddIdleJustificationSupport',
    '20260425154636_AddAiModulePhase1',
    '20260425170000_AddPatternAndAnomalyTables',
    '20260425190000_AddSmartAlertsTable',
    '20260507000000_AddOrgInviteLinksAndOnboarding',
    '20260512133429_AddWeeklyReportSchedulesTable'
];

async function run() {
    const client = new pg.Client({ connectionString });

    try {
        await client.connect();
        console.log('Connected to database');

        // Create table if not exists
        await client.query(`
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
        `);
        console.log('Ensured __EFMigrationsHistory table exists');

        // Insert migrations
        let inserted = 0;
        let skipped = 0;

        for (const migrationId of migrations) {
            const result = await client.query(
                'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ($1, $2) ON CONFLICT ("MigrationId") DO NOTHING',
                [migrationId, '8.0.0']
            );

            if (result.rowCount > 0) {
                inserted++;
                console.log(`✓ ${migrationId}`);
            } else {
                skipped++;
                console.log(`- ${migrationId} (already exists)`);
            }
        }

        // Count total
        const totalResult = await client.query('SELECT COUNT(*) as count FROM "__EFMigrationsHistory"');
        console.log(`\nDone! Total migrations in history: ${totalResult.rows[0].count}`);
        console.log(`Inserted: ${inserted}, Skipped: ${skipped}`);

    } catch (err) {
        console.error('Error:', err.message);
        process.exit(1);
    } finally {
        await client.end();
    }
}

run();
