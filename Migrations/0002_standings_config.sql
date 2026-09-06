-- 0002: Configurable standings rules.
--
-- Adds the per-tenant StandingsJson blob (point values, forfeit score,
-- ordered tiebreakers) read by StandingsConfigService. '{}' means the
-- historic default rules; real rules are set in the Webmaster UI.

ALTER TABLE SiteConfig ADD COLUMN StandingsJson TEXT NOT NULL DEFAULT '{}';
