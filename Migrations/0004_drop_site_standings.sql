-- 0004: Drop the league-wide standings rules column.
--
-- Superseded by per-season rules (Season.StandingsJson, migration 0003).
-- Shipped one deploy cycle after 0003 on purpose: the previous build still
-- mapped this column in EF, so keeping it for a cycle preserved rollback
-- compatibility. From this migration on, rolling back past the per-season
-- build is not supported (restore a .bak instead).

ALTER TABLE SiteConfig DROP COLUMN StandingsJson;
