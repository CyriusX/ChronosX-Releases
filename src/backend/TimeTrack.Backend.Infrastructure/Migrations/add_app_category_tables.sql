-- Migration: AddAppCategoryTables
-- CX-143: Sistema de Categorização de Apps/Sites (Lista Global + Override por Org)
-- Creates tables for global app categorization and organization-specific overrides

-- ============================================================================
-- TABLE: app_category_global
-- Lista global curada pela equipe TimeTrack
-- Nunca editada por clientes
-- ============================================================================

CREATE TABLE IF NOT EXISTS app_category_global (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    identifier TEXT NOT NULL,
    identifier_type TEXT NOT NULL CHECK (identifier_type IN ('exe', 'domain')),
    display_name TEXT NOT NULL,
    productivity TEXT NOT NULL CHECK (productivity IN ('productive', 'neutral', 'distraction')),
    subcategory TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    -- One entry per identifier
    CONSTRAINT uq_app_category_global_identifier UNIQUE (identifier)
);

-- Index for fast lookup by identifier
CREATE INDEX IF NOT EXISTS ix_app_category_global_identifier
    ON app_category_global (identifier);

-- Index for filtering by productivity
CREATE INDEX IF NOT EXISTS ix_app_category_global_productivity
    ON app_category_global (productivity);

-- Index for searching by display name
CREATE INDEX IF NOT EXISTS ix_app_category_global_display_name
    ON app_category_global (display_name);

COMMENT ON TABLE app_category_global IS 'Global curated list of app/site categories maintained by TimeTrack team';
COMMENT ON COLUMN app_category_global.identifier IS 'Process name (e.g., whatsapp.exe) or domain (e.g., youtube.com)';
COMMENT ON COLUMN app_category_global.identifier_type IS 'Type: exe (desktop app) or domain (website)';
COMMENT ON COLUMN app_category_global.display_name IS 'Human-readable name for display';
COMMENT ON COLUMN app_category_global.productivity IS 'Classification: productive, neutral, or distraction';
COMMENT ON COLUMN app_category_global.subcategory IS 'Granular category (development, communication, social_media, etc.)';

-- ============================================================================
-- TABLE: app_category_override
-- Overrides por organização - permite Admin personalizar categorias
-- Prevalece sobre a lista global
-- ============================================================================

CREATE TABLE IF NOT EXISTS app_category_override (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    identifier TEXT NOT NULL,
    identifier_type TEXT NOT NULL CHECK (identifier_type IN ('exe', 'domain')),
    display_name TEXT,
    productivity TEXT NOT NULL CHECK (productivity IN ('productive', 'neutral', 'distraction')),
    subcategory TEXT NOT NULL,
    note TEXT,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    -- One override per identifier per organization
    CONSTRAINT uq_app_category_override_org_identifier UNIQUE (org_id, identifier)
);

-- Index for fast lookup by org + identifier
CREATE INDEX IF NOT EXISTS ix_app_category_override_org_identifier
    ON app_category_override (org_id, identifier);

-- Index for listing all overrides of an org
CREATE INDEX IF NOT EXISTS ix_app_category_override_org_id
    ON app_category_override (org_id);

-- Index for filtering by productivity
CREATE INDEX IF NOT EXISTS ix_app_category_override_productivity
    ON app_category_override (org_id, productivity);

COMMENT ON TABLE app_category_override IS 'Organization-specific category overrides made by Admins';
COMMENT ON COLUMN app_category_override.identifier IS 'Process name or domain to override';
COMMENT ON COLUMN app_category_override.display_name IS 'Optional custom display name (can rename apps)';
COMMENT ON COLUMN app_category_override.note IS 'Admin justification for the override (e.g., "WhatsApp is official support channel")';
COMMENT ON COLUMN app_category_override.created_by IS 'User who created this override';

-- ============================================================================
-- TRIGGER: Auto-update updated_at
-- ============================================================================

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_app_category_global_updated_at
    BEFORE UPDATE ON app_category_global
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_app_category_override_updated_at
    BEFORE UPDATE ON app_category_override
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- SEED DATA: Common apps/sites (~80 entries)
-- ============================================================================

-- === PRODUCTIVE - Development ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('code.exe', 'exe', 'VS Code', 'productive', 'development'),
('code64.exe', 'exe', 'VS Code', 'productive', 'development'),
('devenv.exe', 'exe', 'Visual Studio', 'productive', 'development'),
('idea64.exe', 'exe', 'IntelliJ IDEA', 'productive', 'development'),
('idea.exe', 'exe', 'IntelliJ IDEA', 'productive', 'development'),
('rider64.exe', 'exe', 'JetBrains Rider', 'productive', 'development'),
('webstorm64.exe', 'exe', 'WebStorm', 'productive', 'development'),
('pycharm64.exe', 'exe', 'PyCharm', 'productive', 'development'),
('sublime_text.exe', 'exe', 'Sublime Text', 'productive', 'development'),
('notepad++.exe', 'exe', 'Notepad++', 'productive', 'development'),
('cursor.exe', 'exe', 'Cursor', 'productive', 'development'),
('androidstudio64.exe', 'exe', 'Android Studio', 'productive', 'development'),
('postman.exe', 'exe', 'Postman', 'productive', 'development'),
('docker desktop.exe', 'exe', 'Docker Desktop', 'productive', 'devops');

-- === PRODUCTIVE - Design ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('figma.exe', 'exe', 'Figma', 'productive', 'design'),
('photoshop.exe', 'exe', 'Adobe Photoshop', 'productive', 'design'),
('illustrator.exe', 'exe', 'Adobe Illustrator', 'productive', 'design'),
('xd.exe', 'exe', 'Adobe XD', 'productive', 'design');

-- === PRODUCTIVE - Productivity Tools ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('excel.exe', 'exe', 'Excel', 'productive', 'productivity_tools'),
('winword.exe', 'exe', 'Word', 'productive', 'productivity_tools'),
('powerpnt.exe', 'exe', 'PowerPoint', 'productive', 'productivity_tools'),
('onenote.exe', 'exe', 'OneNote', 'productive', 'productivity_tools'),
('msaccess.exe', 'exe', 'Access', 'productive', 'productivity_tools');

-- === PRODUCTIVE - Communication (work) ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('outlook.exe', 'exe', 'Outlook', 'productive', 'communication'),
('slack.exe', 'exe', 'Slack', 'productive', 'communication'),
('teams.exe', 'exe', 'Microsoft Teams', 'productive', 'meetings'),
('lync.exe', 'exe', 'Skype for Business', 'productive', 'meetings'),
('zoom.exe', 'exe', 'Zoom', 'productive', 'meetings');

-- === PRODUCTIVE - Domains ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('github.com', 'domain', 'GitHub', 'productive', 'development'),
('gitlab.com', 'domain', 'GitLab', 'productive', 'development'),
('bitbucket.org', 'domain', 'Bitbucket', 'productive', 'development'),
('linear.app', 'domain', 'Linear', 'productive', 'productivity_tools'),
('notion.so', 'domain', 'Notion', 'productive', 'productivity_tools'),
('atlassian.net', 'domain', 'Jira/Confluence', 'productive', 'productivity_tools'),
('jira.atlassian.com', 'domain', 'Jira', 'productive', 'productivity_tools'),
('confluence.atlassian.com', 'domain', 'Confluence', 'productive', 'documentation'),
('figma.com', 'domain', 'Figma', 'productive', 'design'),
('stackoverflow.com', 'domain', 'Stack Overflow', 'productive', 'documentation'),
('docs.google.com', 'domain', 'Google Docs', 'productive', 'productivity_tools'),
('sheets.google.com', 'domain', 'Google Sheets', 'productive', 'productivity_tools'),
('slides.google.com', 'domain', 'Google Slides', 'productive', 'productivity_tools'),
('meet.google.com', 'domain', 'Google Meet', 'productive', 'meetings'),
('calendar.google.com', 'domain', 'Google Calendar', 'productive', 'productivity_tools'),
('outlook.live.com', 'domain', 'Outlook Web', 'productive', 'communication'),
('outlook.office.com', 'domain', 'Outlook Web', 'productive', 'communication');

-- === NEUTRAL - Communication (personal) ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('whatsapp.exe', 'exe', 'WhatsApp Desktop', 'neutral', 'communication'),
('telegram.exe', 'exe', 'Telegram Desktop', 'neutral', 'communication'),
('discord.exe', 'exe', 'Discord', 'neutral', 'communication'),
('signal.exe', 'exe', 'Signal', 'neutral', 'communication');

-- === NEUTRAL - Browsers ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('chrome.exe', 'exe', 'Chrome', 'neutral', 'browser_general'),
('firefox.exe', 'exe', 'Firefox', 'neutral', 'browser_general'),
('msedge.exe', 'exe', 'Edge', 'neutral', 'browser_general'),
('brave.exe', 'exe', 'Brave', 'neutral', 'browser_general'),
('opera.exe', 'exe', 'Opera', 'neutral', 'browser_general');

-- === NEUTRAL - System ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('explorer.exe', 'exe', 'Windows Explorer', 'neutral', 'file_manager'),
('cmd.exe', 'exe', 'Command Prompt', 'neutral', 'system'),
('powershell.exe', 'exe', 'PowerShell', 'neutral', 'system'),
('windowsterminal.exe', 'exe', 'Windows Terminal', 'neutral', 'system'),
('taskmgr.exe', 'exe', 'Task Manager', 'neutral', 'system'),
('systemsettings.exe', 'exe', 'Windows Settings', 'neutral', 'system');

-- === NEUTRAL - Domains ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('gmail.com', 'domain', 'Gmail', 'neutral', 'communication'),
('google.com', 'domain', 'Google', 'neutral', 'browser_general'),
('bing.com', 'domain', 'Bing', 'neutral', 'browser_general'),
('duckduckgo.com', 'domain', 'DuckDuckGo', 'neutral', 'browser_general');

-- === DISTRACTION - Social Media ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('instagram.com', 'domain', 'Instagram', 'distraction', 'social_media'),
('facebook.com', 'domain', 'Facebook', 'distraction', 'social_media'),
('twitter.com', 'domain', 'Twitter/X', 'distraction', 'social_media'),
('x.com', 'domain', 'Twitter/X', 'distraction', 'social_media'),
('tiktok.com', 'domain', 'TikTok', 'distraction', 'social_media'),
('linkedin.com', 'domain', 'LinkedIn', 'distraction', 'social_media'),
('reddit.com', 'domain', 'Reddit', 'distraction', 'social_media'),
('pinterest.com', 'domain', 'Pinterest', 'distraction', 'social_media'),
('threads.net', 'domain', 'Threads', 'distraction', 'social_media');

-- === DISTRACTION - Entertainment ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('youtube.com', 'domain', 'YouTube', 'distraction', 'entertainment'),
('youtu.be', 'domain', 'YouTube', 'distraction', 'entertainment'),
('netflix.com', 'domain', 'Netflix', 'distraction', 'entertainment'),
('primevideo.com', 'domain', 'Amazon Prime Video', 'distraction', 'entertainment'),
('disneyplus.com', 'domain', 'Disney+', 'distraction', 'entertainment'),
('hbomax.com', 'domain', 'HBO Max', 'distraction', 'entertainment'),
('twitch.tv', 'domain', 'Twitch', 'distraction', 'entertainment'),
('vimeo.com', 'domain', 'Vimeo', 'distraction', 'entertainment'),
('spotify.exe', 'exe', 'Spotify', 'distraction', 'music_streaming'),
('spotify.com', 'domain', 'Spotify Web', 'distraction', 'music_streaming'),
('apple.com/music', 'domain', 'Apple Music', 'distraction', 'music_streaming');

-- === DISTRACTION - Gaming ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('steam.exe', 'exe', 'Steam', 'distraction', 'gaming'),
('epicgameslauncher.exe', 'exe', 'Epic Games', 'distraction', 'gaming'),
('origin.exe', 'exe', 'EA Origin', 'distraction', 'gaming'),
('battlenet.exe', 'exe', 'Battle.net', 'distraction', 'gaming'),
('gog galaxy.exe', 'exe', 'GOG Galaxy', 'distraction', 'gaming');

-- === DISTRACTION - News ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('g1.globo.com', 'domain', 'G1', 'distraction', 'news'),
('uol.com.br', 'domain', 'UOL', 'distraction', 'news'),
('folha.uol.com.br', 'domain', 'Folha de S.Paulo', 'distraction', 'news'),
('estadao.com.br', 'domain', 'Estadão', 'distraction', 'news'),
('bbc.com', 'domain', 'BBC', 'distraction', 'news'),
('cnn.com', 'domain', 'CNN', 'distraction', 'news');

-- === DISTRACTION - Shopping ===
INSERT INTO app_category_global (identifier, identifier_type, display_name, productivity, subcategory) VALUES
('amazon.com.br', 'domain', 'Amazon', 'distraction', 'shopping'),
('mercadolivre.com.br', 'domain', 'Mercado Livre', 'distraction', 'shopping'),
('magazineluiza.com.br', 'domain', 'Magazine Luiza', 'distraction', 'shopping'),
('americanas.com.br', 'domain', 'Americanas', 'distraction', 'shopping'),
('aliexpress.com', 'domain', 'AliExpress', 'distraction', 'shopping');
