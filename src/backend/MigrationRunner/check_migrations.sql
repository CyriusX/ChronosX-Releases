-- Verificar histórico de migrations
SELECT * FROM "__EFMigrationsHistory";

-- Verificar se as tabelas existem
SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;
