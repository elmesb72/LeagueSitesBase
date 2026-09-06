---
inclusion: auto
description: Multi-tenancy guidelines — keep tenant-specific data out of the shared codebase; secrets live in Infisical; static assets live on per-VM volumes
---

# Multi-tenancy guidelines

This is a multi-tenant application. Each tenant runs on its own VM with its own
SQLite database. The codebase is shared across all tenants.

## No tenant-specific content in the repo

The backend repo must never contain tenant-specific data, configuration, or
migration scripts. This includes:

- Tenant names, domains, or branding
- Seed data for specific tenants (e.g. SiteConfig rows, team names)
- Migration scripts that insert or update tenant-specific values
- API keys, secrets, or credentials (these live in Infisical)

## Schema migrations

Schema changes (CREATE TABLE, ALTER TABLE, ADD COLUMN) that apply universally
to all tenants are acceptable in the repo. They are written as sequential SQL
files in `Migrations/` (`NNNN_description.sql`) and applied automatically at
backend startup by `DatabaseMigrator`, which tracks the schema version in
SQLite's `PRAGMA user_version`. `0001_baseline.sql` is the canonical full
schema (plus universal seed rows) for new tenant databases; never edit an
applied migration — add a new one. Do not include BEGIN/COMMIT in migration
files; the runner wraps each file in its own transaction.

For tenant-specific data seeding (e.g. populating a new SiteConfig row for a
new tenant), use one of these approaches instead:

- Set it through the site's admin UI (preferred)
- Run SQL directly against the tenant's database via SSH
- Build an admin API endpoint that seeds or updates the data
- Include the seed step in the tenant onboarding runbook (in LeagueSitesTerraform/README.md)

## Static assets

Tenant-specific static assets (logos, favicon, PDFs) live on the persistent
volume at `/var/db/static/` on each VM, served directly by Apache. They are
not part of the frontend build and are not stored in any git repo.
