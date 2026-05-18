const { Client } = require('pg');

async function baselineMigrations() {
  const client = new Client({
    connectionString: 'postgres://chronosx:chronosx@easypanel.cyriusx.com:6543/chronosx?sslmode=disable'
  });

  try {
    console.log('🔌 Conectando ao PostgreSQL...');
    await client.connect();
    console.log('✅ Conectado!\n');

    // Verificar se a tabela __EFMigrationsHistory existe
    console.log('🔍 Verificando tabela __EFMigrationsHistory...');
    const checkTable = await client.query(`
      SELECT EXISTS (
        SELECT FROM information_schema.tables
        WHERE table_schema = 'public'
        AND table_name = '__EFMigrationsHistory'
      );
    `);

    if (!checkTable.rows[0].exists) {
      console.log('📝 Criando tabela __EFMigrationsHistory...');
      await client.query(`
        CREATE TABLE "__EFMigrationsHistory" (
          "MigrationId" character varying(150) CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
          "ProductVersion" character varying(32) NOT NULL
        );
      `);
      console.log('✅ Tabela criada!\n');
    } else {
      console.log('✅ Tabela já existe\n');
    }

    // Lista de migrations
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

    // Verificar migrations já registradas
    const existing = await client.query('SELECT "MigrationId" FROM "__EFMigrationsHistory"');
    const existingIds = new Set(existing.rows.map(r => r.MigrationId));
    const pending = migrations.filter(m => !existingIds.has(m));

    console.log(`📊 Status das migrations:`);
    console.log(`   - Total: ${migrations.length}`);
    console.log(`   - Já registradas: ${existingIds.size}`);
    console.log(`   - A registrar: ${pending.length}\n`);

    if (pending.length === 0) {
      console.log('✅ Todas as migrations já estão registradas. Nada a fazer.');
      return;
    }

    // Inserir migrations pendentes
    console.log('📝 Registrando migrations como aplicadas (baseline)...');
    for (const migration of pending) {
      await client.query(
        'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ($1, $2) ON CONFLICT ("MigrationId") DO NOTHING',
        [migration, '9.0.4']
      );
      console.log(`   ✅ ${migration}`);
    }

    // Verificar resultado final
    const finalCount = await client.query('SELECT COUNT(*) FROM "__EFMigrationsHistory"');
    console.log(`\n🎉 Baseline concluído!`);
    console.log(`   - Migrations registradas: ${finalCount.rows[0].count}/${migrations.length}`);
    console.log('\n✨ A API agora deve iniciar sem erro de migrations pendentes.');

  } catch (error) {
    console.error('❌ Erro:', error.message);
    process.exit(1);
  } finally {
    await client.end();
    console.log('\n👋 Conexão encerrada.');
  }
}

baselineMigrations();
