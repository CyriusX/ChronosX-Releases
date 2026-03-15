-- ============================================================================
-- Script para sincronizar o histórico de migrations do EF Core
-- ============================================================================
-- PROBLEMA: A tabela audit_log já existe, mas a migration não está registrada
-- SOLUÇÃO: Inserir manualmente os registros na tabela __EFMigrationsHistory
--
-- Execute este script no banco PostgreSQL antes de iniciar a aplicação
-- ============================================================================

-- 1. Criar a tabela de histórico se não existir
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
    "ProductVersion" character varying(32) NOT NULL
);

-- 2. Inserir as migrations já aplicadas (apenas se não existirem)
-- Migration 1: InitialCreate (10/03/2026)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260310200919_InitialCreate', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Migration 2: AddProjectsTable (12/03/2026)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260312123906_AddProjectsTable', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Migration 3: CreateOrgPoliciesTable (13/03/2026)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260313002015_CreateOrgPoliciesTable', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- 3. Verificar o resultado
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

-- ============================================================================
-- NOTA: Se existirem tabelas que NÃO foram criadas ainda, você precisará
-- executar as migrations SQL manuais ou criar novas migrations do EF Core.
-- ============================================================================
