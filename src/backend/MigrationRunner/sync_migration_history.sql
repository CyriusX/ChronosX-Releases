-- ============================================================================
-- Script para sincronizar o histórico de migrations do EF Core
-- ============================================================================
-- IMPORTANTE: O EF Core está configurado para usar __ef_migrations_history (lowercase)
-- Veja: InfrastructureServiceCollectionExtensions.cs linha 30
-- ============================================================================

-- 1. Criar tabela de histórico se não existir (lowercase - como o EF Core espera)
CREATE TABLE IF NOT EXISTS __ef_migrations_history (
    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
    "ProductVersion" character varying(32) NOT NULL
);

-- 2. Inserir as migrations já aplicadas (apenas se não existirem)
INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260310200919_InitialCreate', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260312123906_AddProjectsTable', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion")
VALUES ('20260313002015_CreateOrgPoliciesTable', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- 3. Remover tabela incorreta (maiúscula) se existir
DROP TABLE IF EXISTS "__EFMigrationsHistory";
