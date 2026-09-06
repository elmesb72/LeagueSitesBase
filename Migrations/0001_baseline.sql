-- 0001: Baseline schema.
--
-- The complete LeagueSites schema as it existed when the migration system
-- was introduced (September 2026), including universal seed rows (game
-- statuses, roles, a placeholder SiteConfig). Tenant-specific values are
-- never seeded here; they are set through the site's admin UI.
--
-- Conventions for every migration file (see DatabaseMigrator):
--   - Files are named NNNN_description.sql and apply strictly in order.
--   - Do NOT include BEGIN/COMMIT; the runner wraps each file in a
--     transaction (with foreign_keys off, dump-style).
--   - Never edit an applied migration; add a new one instead.

CREATE TABLE IF NOT EXISTS "Season" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Year"	INTEGER NOT NULL,
	"Subseason"	TEXT NOT NULL,
	"StartDate"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "UserLoginSource" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Source"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO UserLoginSource VALUES(1,'Google');
INSERT INTO UserLoginSource VALUES(2,'Microsoft');
INSERT INTO UserLoginSource VALUES(3,'Facebook');
CREATE TABLE IF NOT EXISTS "UserLogin" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"UserID"	INTEGER NOT NULL,
	"LoginSourceID"	INTEGER NOT NULL,
	"Name"	TEXT NOT NULL,
	"Email"	TEXT NOT NULL,
	"IsPrimary"	INTEGER NOT NULL DEFAULT 1,
	FOREIGN KEY("LoginSourceID") REFERENCES "UserLoginSource"("ID"),
	FOREIGN KEY("UserID") REFERENCES "User"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO UserLogin VALUES(1,1,1,'Dev Admin','admin@example.com',1);
CREATE TABLE IF NOT EXISTS "UserRole" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"UserID"	INTEGER NOT NULL,
	"RoleID"	INTEGER NOT NULL,
	FOREIGN KEY("RoleID") REFERENCES "Role"("ID"),
	FOREIGN KEY("UserID") REFERENCES "User"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO UserRole VALUES(1,1,4);
CREATE TABLE IF NOT EXISTS "Role" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO Role VALUES(1,'Manager');
INSERT INTO Role VALUES(2,'Scorer');
INSERT INTO Role VALUES(3,'Executive');
INSERT INTO Role VALUES(4,'Webmaster');
INSERT INTO Role VALUES(5,'Reporter');
CREATE TABLE IF NOT EXISTS "InvitationEmail" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"InvitationID"	INTEGER NOT NULL,
	"Email"	TEXT NOT NULL,
	FOREIGN KEY("InvitationID") REFERENCES "Invitation"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO InvitationEmail VALUES(1,1,'admin@example.com');
CREATE TABLE IF NOT EXISTS "InvitationRole" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"InvitationID"	INTEGER NOT NULL,
	"RoleID"	INTEGER NOT NULL,
	FOREIGN KEY("RoleID") REFERENCES "Role"("ID"),
	FOREIGN KEY("InvitationID") REFERENCES "Invitation"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "Player" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"FirstName"	INTEGER NOT NULL,
	"LastName"	INTEGER NOT NULL,
	"Number"	TEXT,
	"ShortCode"	TEXT NOT NULL CHECK(length("ShortCode") <= 9) UNIQUE,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "User" (
	"ID"	INTEGER NOT NULL UNIQUE,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO User VALUES(1);
CREATE TABLE IF NOT EXISTS "Invitation" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"PlayerID"	INTEGER,
	"TeamID"	INTEGER NOT NULL,
	"UserID"	INTEGER,
	"StatusID"	INTEGER NOT NULL,
	"EmergencyContactInfo"	TEXT,
	FOREIGN KEY("StatusID") REFERENCES "InvitationStatus"("ID"),
	FOREIGN KEY("TeamID") REFERENCES "Team"("ID"),
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	FOREIGN KEY("UserID") REFERENCES "User"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO Invitation VALUES(1,NULL,1,1,4,NULL);
CREATE TABLE IF NOT EXISTS "GameStatus" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO GameStatus VALUES(1,'Upcoming');
INSERT INTO GameStatus VALUES(2,'Cancelled');
INSERT INTO GameStatus VALUES(3,'Postponed');
INSERT INTO GameStatus VALUES(4,'Deleted');
INSERT INTO GameStatus VALUES(5,'Played');
INSERT INTO GameStatus VALUES(6,'Forfeit (Home)');
INSERT INTO GameStatus VALUES(7,'Forfeit (Away)');
CREATE TABLE IF NOT EXISTS "InvitationStatus" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO InvitationStatus VALUES(1,'Active');
INSERT INTO InvitationStatus VALUES(2,'Substitute');
INSERT INTO InvitationStatus VALUES(3,'Retired');
INSERT INTO InvitationStatus VALUES(4,'Other');
CREATE TABLE IF NOT EXISTS "Event" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Type"	INTEGER NOT NULL,
	"Date"	TEXT NOT NULL,
	"UserID"	INTEGER NOT NULL,
	"Resource"	TEXT NOT NULL,
	"Summary"	TEXT NOT NULL,
	"Description"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO Event VALUES(1,'Update','2024-08-11 16:45:45.8882345',1,'/OAuth/Google','Registered new user','[{"ID":1,"PlayerID":null,"TeamID":1,"UserID":1,"Player":null,"StatusID":4,"EmergencyContactInfo":null,"InvitationEmails":[{"ID":1,"InvitationID":1,"Email":"admin@example.com"}],"InvitationRoles":[]}]');
CREATE TABLE IF NOT EXISTS "PlayerBio" (
	"PlayerID"	INTEGER NOT NULL UNIQUE,
	"Bats"	TEXT,
	"Throws"	TEXT,
	"Positions"	TEXT,
	"Height"	INTEGER,
	"Weight"	INTEGER,
	"Birthdate"	TEXT,
	"From"	TEXT,
	"ReferredBy"	TEXT,
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	PRIMARY KEY("PlayerID")
);
CREATE TABLE IF NOT EXISTS "Team" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Location"	TEXT NOT NULL,
	"Name"	TEXT NOT NULL,
	"Abbreviation"	TEXT NOT NULL,
	"Active"	INTEGER NOT NULL DEFAULT 0,
	"BackgroundColor"	TEXT NOT NULL DEFAULT 'FFFFFF',
	"Color"	TEXT NOT NULL DEFAULT '000000',
	"Hidden"	INTEGER DEFAULT 0,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO Team VALUES(1,'Webmaster','Fake Team','WFT',0,'FFFFFF','000000',1);
CREATE TABLE IF NOT EXISTS "SocialPlatform" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"BaseUrl"	TEXT,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO SocialPlatform VALUES(1,'Twitter','https://twitter.com/');
INSERT INTO SocialPlatform VALUES(2,'Instagram','https://instagram.com/');
INSERT INTO SocialPlatform VALUES(3,'Facebook','https://facebook.com');
INSERT INTO SocialPlatform VALUES(4,'YouTube','https://www.youtube.com/channel/');
CREATE TABLE IF NOT EXISTS "TeamSocial" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"TeamID"	INTEGER NOT NULL,
	"SocialPlatformID"	INTEGER NOT NULL,
	"Account"	TEXT NOT NULL,
	FOREIGN KEY("TeamID") REFERENCES "Team"("ID"),
	FOREIGN KEY("SocialPlatformID") REFERENCES "SocialPlatform"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "PlayerSocial" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"PlayerID"	INTEGER NOT NULL,
	"SocialPlatformID"	INTEGER NOT NULL,
	"Account"	TEXT NOT NULL,
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	FOREIGN KEY("SocialPlatformID") REFERENCES "SocialPlatform"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "Tournament" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"SeasonID"	INTEGER NOT NULL,
	FOREIGN KEY("SeasonID") REFERENCES "Season"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "BracketRound" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"BracketID"	INTEGER NOT NULL,
	FOREIGN KEY("BracketID") REFERENCES "TournamentBracket"("ID"),
	PRIMARY KEY("ID")
);
CREATE TABLE IF NOT EXISTS "RoundSeries" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"RoundID"	INTEGER NOT NULL,
	"Number"	INTEGER NOT NULL,
	"Format"	TEXT NOT NULL,
	"HostOrder"	TEXT NOT NULL,
	"Matchup"	TEXT NOT NULL,
	FOREIGN KEY("RoundID") REFERENCES "BracketRound"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "SeriesGame" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"SeriesID"	INTEGER NOT NULL,
	"GameNumber"	INTEGER NOT NULL,
	"GameID"	INTEGER,
	FOREIGN KEY("SeriesID") REFERENCES "RoundSeries"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "RoundRobinGame" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"TournamentRoundRobinID"	INTEGER NOT NULL,
	"GameID"	INTEGER,
	FOREIGN KEY("TournamentRoundRobinID") REFERENCES "TournamentRoundRobin"("ID"),
	FOREIGN KEY("GameID") REFERENCES "Game"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "Location" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"FormalName"	TEXT,
	"City"	TEXT NOT NULL,
	"Address"	TEXT,
	"MapsPlaceID"	TEXT,
	"Active"	INTEGER,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "BattingEvent" (
	"GameID"	INTEGER NOT NULL,
	"IsHostTeam"	INTEGER NOT NULL,
	"Index"	INTEGER NOT NULL,
	"PlayerID"	INTEGER NOT NULL,
	"Before"	TEXT,
	"During"	TEXT NOT NULL,
	"After"	TEXT,
	"PitchSequence"	TEXT,
	"Notes"	TEXT,
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	FOREIGN KEY("GameID") REFERENCES "Game"("ID"),
	PRIMARY KEY("GameID","IsHostTeam","Index")
);
CREATE TABLE IF NOT EXISTS "BattingLineupEntry" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"GameID"	INTEGER NOT NULL,
	"IsHostTeam"	INTEGER NOT NULL,
	"Row"	INTEGER NOT NULL,
	"PlayerID"	INTEGER NOT NULL,
	"FirstAB"	INTEGER,
	"BattingSide"	TEXT,
	"Positions"	TEXT,
	"Out"	INTEGER,
	FOREIGN KEY("GameID") REFERENCES "Game"("ID"),
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "Game" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"SeasonID"	INTEGER NOT NULL,
	"Date"	TEXT NOT NULL,
	"HostTeamID"	INTEGER NOT NULL,
	"VisitingTeamID"	INTEGER NOT NULL,
	"LocationID"	INTEGER NOT NULL,
	"StatusID"	INTEGER NOT NULL DEFAULT 0,
	"ScoreHost"	INTEGER,
	"ScoreVisitor"	INTEGER,
	FOREIGN KEY("SeasonID") REFERENCES "Season"("ID"),
	FOREIGN KEY("HostTeamID") REFERENCES "Team"("ID"),
	FOREIGN KEY("LocationID") REFERENCES "Location"("ID"),
	FOREIGN KEY("StatusID") REFERENCES "GameStatus"("ID"),
	FOREIGN KEY("VisitingTeamID") REFERENCES "Team"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "News" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"AuthorID"	INTEGER NOT NULL,
	"Date"	TEXT NOT NULL,
	"Edited"	TEXT,
	"Title"	TEXT NOT NULL,
	"Contents"	TEXT NOT NULL,
	"Source"	TEXT NOT NULL,
	"IsDeleted"	INTEGER NOT NULL DEFAULT 0,
	"IsHidden"	INTEGER NOT NULL DEFAULT 0, AuthorInvitationID INTEGER NOT NULL DEFAULT 0 REFERENCES Invitation(ID),
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("AuthorID") REFERENCES "User"("ID")
);
CREATE TABLE IF NOT EXISTS "TournamentRoundRobin" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"TournamentID"	INTEGER NOT NULL,
	"SeedingConfiguration"	TEXT,
	"Historical"	INTEGER NOT NULL DEFAULT 1,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("TournamentID") REFERENCES "Tournament"("ID")
);
CREATE TABLE IF NOT EXISTS "TournamentBracket" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"TournamentID"	INTEGER NOT NULL,
	"SeedingConfiguration"	TEXT,
	"Format"	TEXT NOT NULL,
	"Historical"	INTEGER NOT NULL DEFAULT 1,
	PRIMARY KEY("ID"),
	FOREIGN KEY("TournamentID") REFERENCES "Tournament"("ID")
);
CREATE TABLE IF NOT EXISTS "SiteConfig" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"ShortName"	TEXT NOT NULL,
	"HomeJson"	TEXT NOT NULL,
	"HistoryJson"	TEXT NOT NULL DEFAULT '[]',
	PRIMARY KEY("ID" AUTOINCREMENT)
);
INSERT INTO SiteConfig VALUES(1,'Empty Generic League','EGL','{"aboutBlurb":"Welcome to the official website for the <i>Empty Generic League</i>! This league is a sports league where we play sports together.","newsMaxAgeDays":30,"newsMinItems":3,"executives":{"Commissioner":"Example Commissioner","Treasurer":"Example Treasurer"},"socials":{"Facebook":"","Twitter":"","Instagram":"","Discord":""},"links":{"Church League Fastball":"https://churchleaguefastball.ca"},"information":{"League Rules":"#","Waiver Form":"#"}}','[]');
