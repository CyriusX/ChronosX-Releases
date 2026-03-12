-- Migration: AddProjectsTable
-- Creates the projects table for TimeTrack

CREATE TABLE IF NOT EXISTS projects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES organizations(id) ON DELETE RESTRICT,
    name VARCHAR(255) NOT NULL,
    description VARCHAR(1000),
    color VARCHAR(7) NOT NULL DEFAULT '#4A9FFF',
    status VARCHAR(20) NOT NULL DEFAULT 'Active',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    updated_at TIMESTAMP WITH TIME ZONE
);

-- Create unique index on org_id + name
CREATE UNIQUE INDEX IF NOT EXISTS ix_projects_org_id_name ON projects(org_id, name);

-- Create index on org_id for faster queries
CREATE INDEX IF NOT EXISTS ix_projects_org_id ON projects(org_id);
