---
inclusion: auto
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
to all tenants are acceptable in the repo. Use the `Empty.db.sql` file as the
canonical schema reference for new tenant databases.

For tenant-specific data seeding (e.g. populating a new SiteConfig row for a
new tenant), use one of these approaches instead:

- Run SQL directly against the tenant's database via SSH
- Build an admin API endpoint that seeds or updates the data
- Include the seed step in the tenant onboarding runbook (in LeagueSitesTerraform/README.md)

## Static assets

Tenant-specific static assets (logos, favicon, PDFs) live on the persistent
volume at `/var/db/static/` on each VM, served directly by Apache. They are
not part of the frontend build and are not stored in any git repo.
