-- Script to mark all migrations as applied in __EFMigrationsHistory
-- Use this ONLY if the database schema already matches all migrations
-- Run this in your PostgreSQL database (e.g., via pgAdmin, psql, or EasyPanel SQL editor)

-- Check if table exists first
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory') THEN
        CREATE TABLE "__EFMigrationsHistory" (
            "MigrationId" character varying(150) NOT NULL,
            "ProductVersion" character varying(32) NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
        );
    END IF;
END
$$;

-- Insert all migrations (only if not already present)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES
    ('20260310200919_InitialCreate', '8.0.0'),
    ('20260312123906_AddProjectsTable', '8.0.0'),
    ('20260313002015_CreateOrgPoliciesTable', '8.0.0'),
    ('20260314214805_AddAppCategoryAndFocusScoreTables', '8.0.0'),
    ('20260321181926_AddFilePathToActivitySession', '8.0.0'),
    ('20260321203000_AddAppSubcategoryToActivitySession', '8.0.0'),
    ('20260403034048_AddMachineMetricsTable', '8.0.0'),
    ('20260403140618_AddAgentEventLogsTable', '8.0.0'),
    ('20260403144423_AddRemoteCommandsAndDeviceInfo', '8.0.0'),
    ('20260409224255_AddDeviceHealthFields', '8.0.0'),
    ('20260410185330_AddTasksAndMembers', '8.0.0'),
    ('20260411030501_AddLinearIntegrationAndDeadlines', '8.0.0'),
    ('20260411040000_AddBillableProjectFields', '8.0.0'),
    ('20260411050000_AddLinearOAuthFields', '8.0.0'),
    ('20260412120000_AddActivitySessionUserIdIndex', '8.0.0'),
    ('20260415120000_ExpandTaskDescriptionTo5000', '8.0.0'),
    ('20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes', '8.0.0'),
    ('20260417120000_AddProjectCreatorAndSoftDelete', '8.0.0'),
    ('20260418145616_AddSubscriptionTables', '8.0.0'),
    ('20260418185156_AddDevToolsEnabledUntilUtcToUsers', '8.0.0'),
    ('20260418231811_AddBillingInvoices', '8.0.0'),
    ('20260419202623_AddRefundFieldsToBillingInvoice', '8.0.0'),
    ('20260420211819_AddTrialSubscriptionBackfill', '8.0.0'),
    ('20260420224132_AddIsPlatformAdminToUsers', '8.0.0'),
    ('20260423184243_AddEvidencePolicyFields', '8.0.0'),
    ('20260423190525_AddEvidenceItemsAndStorageKeys', '8.0.0'),
    ('20260423201010_AddStorageQuotaFields', '8.0.0'),
    ('20260425130000_AddIdleJustificationSupport', '8.0.0'),
    ('20260425154636_AddAiModulePhase1', '8.0.0'),
    ('20260425170000_AddPatternAndAnomalyTables', '8.0.0'),
    ('20260425190000_AddSmartAlertsTable', '8.0.0'),
    ('20260507000000_AddOrgInviteLinksAndOnboarding', '8.0.0'),
    ('20260512133429_AddWeeklyReportSchedulesTable', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Verify
SELECT COUNT(*) as total_migrations FROM "__EFMigrationsHistory";
