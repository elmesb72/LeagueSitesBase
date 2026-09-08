-- 0003: Per-season standings rules.
--
-- Standings rules move from one league-wide setting (SiteConfig.StandingsJson,
-- now unused and dropped in a follow-up migration) to a per-season value, so
-- rule changes between seasons never re-rank historical standings, records,
-- or playoff seeding.
--
-- Every existing season is stamped with the platform's corrected canonical
-- rules: points, wins, then head-to-head wins and head-to-head run
-- differential among tied teams. This is retroactive on purpose: the old
-- computation broke ties by OVERALL run differential, which was never how
-- these leagues actually ranked teams. Individual seasons can be re-edited
-- afterwards under Executive > Standings.

ALTER TABLE Season ADD COLUMN StandingsJson TEXT NOT NULL DEFAULT '';

UPDATE Season SET StandingsJson = '{"winsValue":2,"tiesValue":1,"lossesValue":0,"forfeitWinnerScore":7,"forfeitLoserScore":0,"tiebreakers":["Points","Wins","HeadToHeadWins","HeadToHeadRunDifferential"]}';
