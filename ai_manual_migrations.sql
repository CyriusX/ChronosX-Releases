
-- ============================================================
-- Manual Migration: 20260425170000_AddPatternAndAnomalyTables
-- ============================================================
START TRANSACTION;

CREATE TABLE IF NOT EXISTS user_patterns (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    org_id uuid NOT NULL,
    pattern_tag character varying(100) NOT NULL,
    detected_at date NOT NULL,
    strength double precision NOT NULL,
    evidence jsonb NOT NULL,
    description text,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT PK_user_patterns PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_user_patterns_user_id_pattern_tag_detected_at
    ON user_patterns (user_id, pattern_tag, detected_at);

CREATE INDEX IF NOT EXISTS IX_user_patterns_user_id_is_active_detected_at
    ON user_patterns (user_id, is_active, detected_at);

CREATE TABLE IF NOT EXISTS behavioral_anomalies (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    org_id uuid NOT NULL,
    anomaly_type character varying(100) NOT NULL,
    severity character varying(20) NOT NULL,
    detected_at date NOT NULL,
    evidence jsonb NOT NULL,
    baseline_value double precision,
    actual_value double precision,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT PK_behavioral_anomalies PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS IX_behavioral_anomalies_user_id_detected_at
    ON behavioral_anomalies (user_id, detected_at);

CREATE INDEX IF NOT EXISTS IX_behavioral_anomalies_org_id_severity_detected_at
    ON behavioral_anomalies (org_id, severity, detected_at);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260425170000_AddPatternAndAnomalyTables', '8.0.0')
ON CONFLICT DO NOTHING;

COMMIT;

-- ============================================================
-- Manual Migration: 20260425190000_AddSmartAlertsTable
-- ============================================================
START TRANSACTION;

CREATE TABLE IF NOT EXISTS smart_alerts (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    about_user_id uuid,
    org_id uuid NOT NULL,
    alert_type character varying(100) NOT NULL,
    message text NOT NULL,
    severity character varying(20) NOT NULL,
    action_type character varying(50),
    was_read boolean NOT NULL DEFAULT false,
    was_acted boolean NOT NULL DEFAULT false,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT PK_smart_alerts PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS IX_smart_alerts_user_id_was_read_created_at
    ON smart_alerts (user_id, was_read, created_at);

CREATE INDEX IF NOT EXISTS IX_smart_alerts_org_id_severity_created_at
    ON smart_alerts (org_id, severity, created_at);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260425190000_AddSmartAlertsTable', '8.0.0')
ON CONFLICT DO NOTHING;

COMMIT;
