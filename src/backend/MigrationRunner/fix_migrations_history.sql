-- Verificar ambas as tabelas
SELECT '__EFMigrationsHistory (quoted)' as source, "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory"
UNION ALL
SELECT '__ef_migrations_history (lowercase)' as source, "MigrationId", "ProductVersion" FROM "__ef_migrations_history";

-- Verificar se a tabela lowercase tem dados
SELECT COUNT(*) as count_lower FROM "__ef_migrations_history";

-- Dropar a tabela lowercase (duplicada)
DROP TABLE IF EXISTS "__ef_migrations_history";
