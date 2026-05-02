-- Create SiteConfig table and seed with CLFB tenant data.
-- Run against the tenant's SQLite DB: sqlite3 /var/db/CLFB.db < 001-site-config.sql

CREATE TABLE IF NOT EXISTS "SiteConfig" (
    "ID"        INTEGER NOT NULL UNIQUE,
    "Name"      TEXT NOT NULL,
    "ShortName" TEXT NOT NULL,
    "HomeJson"  TEXT NOT NULL,
    "HistoryJson" TEXT NOT NULL DEFAULT '[]',
    PRIMARY KEY("ID" AUTOINCREMENT)
);

INSERT INTO "SiteConfig" ("Name", "ShortName", "HomeJson", "HistoryJson")
VALUES (
    'Church League Fastball',
    'CLFB',
    '{"aboutBlurb":"<i>Church League Fastball</i> is a league that values the spirit of friendly competition. Meant as an introductory league for players looking for more competition than offered by slow- and three-pitch leagues, the league envisions you bringing out your neighbour or friend to learn the game of fastpitch softball.","newsMaxAgeDays":30,"newsMinItems":3,"executives":{"President":"Ryan McMillan","Treasurer":"John Vleeming"},"socials":{"Facebook":"","Twitter":"","Instagram":"","Discord":""},"links":{"Kitchener Fastball League":"http://www.kitchenerfastballleague.ca/","Intercounty Fastball League":"https://www.hometeamsonline.com/teams/?u=INTERCOUNTY_FASTBALL_LEAGUE&s=softball"},"information":{"League Rules (2025)":"/files/rules.pdf","Printable Scoresheet":"/files/scoresheet.pdf"}}',
    '[{"year":2020,"result":"No season"},{"year":2015,"result":"West Hills Rays"}]'
);
