-- ============================================================================
-- SCRIPT DE DELEÇÃO DE USUÁRIOS - TimeTracking
-- Emails: junin15z33@gmail.com, junin_15-33@hotmail.com
-- Data: 2026-03-23
-- ============================================================================
-- IMPORTANTE: Execute este script no console do Neon.tech ou pgAdmin
-- ============================================================================

-- ============================================================================
-- PASSO 1: VERIFICAR USUÁRIOS EXISTENTES
-- ============================================================================
SELECT
    u.id,
    u.email,
    u.display_name,
    u.role,
    u.status,
    u.created_at,
    o.name as organization_name,
    o.id as org_id
FROM users u
LEFT JOIN orgs o ON u.org_id = o.id
WHERE u.email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com');

-- ============================================================================
-- PASSO 2: VERIFICAR DADOS RELACIONADOS (serão deletados via CASCADE)
-- ============================================================================
SELECT
    'devices' as tabela,
    d.user_id,
    COUNT(*) as total_registros
FROM devices d
JOIN users u ON d.user_id = u.id
WHERE u.email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com')
GROUP BY d.user_id

UNION ALL

SELECT
    'activity_sessions' as tabela,
    a.user_id,
    COUNT(*) as total_registros
FROM activity_sessions a
JOIN users u ON a.user_id = u.id
WHERE u.email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com')
GROUP BY a.user_id

UNION ALL

SELECT
    'daily_focus_scores' as tabela,
    dfs.user_id,
    COUNT(*) as total_registros
FROM daily_focus_scores dfs
JOIN users u ON dfs.user_id = u.id
WHERE u.email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com')
GROUP BY dfs.user_id;

-- ============================================================================
-- PASSO 3: VERIFICAR APP_CATEGORY_OVERRIDE (tem ON DELETE RESTRICT)
-- ============================================================================
SELECT
    aco.id,
    aco.identifier,
    aco.display_name,
    aco.productivity,
    u.email as created_by_email
FROM app_category_override aco
JOIN users u ON aco.created_by = u.id
WHERE u.email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com');

-- ============================================================================
-- PASSO 4: DELETAR OVERRIDES DE CATEGORIA (se houver)
-- ============================================================================
DELETE FROM app_category_override
WHERE created_by IN (
    SELECT id FROM users
    WHERE email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com')
);

-- ============================================================================
-- PASSO 5: DELETAR AUDIT_LOG (user_id é nullable, mas vamos limpar)
-- ============================================================================
DELETE FROM audit_log
WHERE user_id IN (
    SELECT id FROM users
    WHERE email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com')
);

-- ============================================================================
-- PASSO 6: DELETAR OS USUÁRIOS
-- ============================================================================
-- O CASCADE deletará automaticamente:
-- - devices
-- - refresh_tokens
-- - activity_sessions
-- - focus_sessions
-- - idle_periods
-- - password_reset_tokens
-- - daily_focus_scores
-- - daily_summaries

DELETE FROM users
WHERE email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com');

-- ============================================================================
-- PASSO 7: VERIFICAR ORGANIZAÇÕES ÓRFÃS (sem usuários)
-- ============================================================================
SELECT
    o.id,
    o.name,
    o.slug,
    o.created_at,
    COUNT(u.id) as total_users
FROM orgs o
LEFT JOIN users u ON u.org_id = o.id
GROUP BY o.id, o.name, o.slug, o.created_at
HAVING COUNT(u.id) = 0;

-- ============================================================================
-- PASSO 8 (OPCIONAL): DELETAR ORGANIZAÇÕES ÓRFÃS
-- ============================================================================
-- Descomente as linhas abaixo se quiser deletar organizações sem usuários:

-- DELETE FROM policies WHERE org_id IN (
--     SELECT o.id FROM orgs o
--     LEFT JOIN users u ON u.org_id = o.id
--     WHERE u.id IS NULL
-- );
--
-- DELETE FROM projects WHERE org_id IN (
--     SELECT o.id FROM orgs o
--     LEFT JOIN users u ON u.org_id = o.id
--     WHERE u.id IS NULL
-- );
--
-- DELETE FROM orgs o
-- WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.org_id = o.id);

-- ============================================================================
-- CONFIRMAÇÃO FINAL
-- ============================================================================
SELECT 'DELEÇÃO CONCLUÍDA COM SUCESSO!' as status;

SELECT
    u.id,
    u.email
FROM users u
WHERE u.email IN ('junin15z33@gmail.com', 'junin_15-33@hotmail.com');
-- Deve retornar 0 linhas se deletado com sucesso
