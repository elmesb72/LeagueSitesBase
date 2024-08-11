BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "Season" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Year"	INTEGER NOT NULL,
	"Subseason"	TEXT NOT NULL,
	"StartDate"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "News" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Date"	TEXT NOT NULL,
	"Title"	TEXT NOT NULL,
	"Contents"	TEXT NOT NULL,
	"Source"	TEXT NOT NULL,
	"IsDeleted"	INTEGER NOT NULL DEFAULT 0,
	"IsHidden"	INTEGER NOT NULL DEFAULT 0,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "UserLoginSource" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Source"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "UserLogin" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"UserID"	INTEGER NOT NULL,
	"LoginSourceID"	INTEGER NOT NULL,
	"Name"	TEXT NOT NULL,
	"Email"	TEXT NOT NULL,
	"IsPrimary"	INTEGER NOT NULL DEFAULT 1,
	FOREIGN KEY("UserID") REFERENCES "User"("ID"),
	FOREIGN KEY("LoginSourceID") REFERENCES "UserLoginSource"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "UserRole" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"UserID"	INTEGER NOT NULL,
	"RoleID"	INTEGER NOT NULL,
	FOREIGN KEY("RoleID") REFERENCES "Role"("ID"),
	FOREIGN KEY("UserID") REFERENCES "User"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "Role" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "InvitationEmail" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"InvitationID"	INTEGER NOT NULL,
	"Email"	TEXT NOT NULL,
	FOREIGN KEY("InvitationID") REFERENCES "Invitation"("ID"),
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "InvitationRole" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"InvitationID"	INTEGER NOT NULL,
	"RoleID"	INTEGER NOT NULL,
	FOREIGN KEY("InvitationID") REFERENCES "Invitation"("ID"),
	FOREIGN KEY("RoleID") REFERENCES "Role"("ID"),
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
CREATE TABLE IF NOT EXISTS "Invitation" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"PlayerID"	INTEGER,
	"TeamID"	INTEGER NOT NULL,
	"UserID"	INTEGER,
	"StatusID"	INTEGER NOT NULL,
	"EmergencyContactInfo"	TEXT,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	FOREIGN KEY("UserID") REFERENCES "User"("ID"),
	FOREIGN KEY("StatusID") REFERENCES "InvitationStatus"("ID"),
	FOREIGN KEY("TeamID") REFERENCES "Team"("ID")
);
CREATE TABLE IF NOT EXISTS "GameStatus" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "InvitationStatus" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
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
	PRIMARY KEY("PlayerID"),
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID")
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
CREATE TABLE IF NOT EXISTS "SocialPlatform" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"BaseUrl"	TEXT,
	PRIMARY KEY("ID" AUTOINCREMENT)
);
CREATE TABLE IF NOT EXISTS "TeamSocial" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"TeamID"	INTEGER NOT NULL,
	"SocialPlatformID"	INTEGER NOT NULL,
	"Account"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("TeamID") REFERENCES "Team"("ID"),
	FOREIGN KEY("SocialPlatformID") REFERENCES "SocialPlatform"("ID")
);
CREATE TABLE IF NOT EXISTS "PlayerSocial" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"PlayerID"	INTEGER NOT NULL,
	"SocialPlatformID"	INTEGER NOT NULL,
	"Account"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	FOREIGN KEY("SocialPlatformID") REFERENCES "SocialPlatform"("ID")
);
CREATE TABLE IF NOT EXISTS "Tournament" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"SeasonID"	INTEGER NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("SeasonID") REFERENCES "Season"("ID")
);
CREATE TABLE IF NOT EXISTS "TournamentBracket" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"TournamentID"	INTEGER NOT NULL,
	"HigherSeedSource"	TEXT NOT NULL,
	"Format"	TEXT NOT NULL,
	PRIMARY KEY("ID"),
	FOREIGN KEY("TournamentID") REFERENCES "Tournament"("ID")
);
CREATE TABLE IF NOT EXISTS "BracketRound" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"BracketID"	INTEGER NOT NULL,
	PRIMARY KEY("ID"),
	FOREIGN KEY("BracketID") REFERENCES "TournamentBracket"("ID")
);
CREATE TABLE IF NOT EXISTS "RoundSeries" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"RoundID"	INTEGER NOT NULL,
	"Number"	INTEGER NOT NULL,
	"Format"	TEXT NOT NULL,
	"HostOrder"	TEXT NOT NULL,
	"Matchup"	TEXT NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("RoundID") REFERENCES "BracketRound"("ID")
);
CREATE TABLE IF NOT EXISTS "SeriesGame" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"SeriesID"	INTEGER NOT NULL,
	"GameNumber"	INTEGER NOT NULL,
	"GameID"	INTEGER,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("SeriesID") REFERENCES "RoundSeries"("ID")
);
CREATE TABLE IF NOT EXISTS "TournamentRoundRobin" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"Name"	TEXT NOT NULL,
	"TournamentID"	INTEGER NOT NULL,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("TournamentID") REFERENCES "Tournament"("ID")
);
CREATE TABLE IF NOT EXISTS "RoundRobinGame" (
	"ID"	INTEGER NOT NULL UNIQUE,
	"TournamentRoundRobinID"	INTEGER NOT NULL,
	"GameID"	INTEGER,
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("TournamentRoundRobinID") REFERENCES "TournamentRoundRobin"("ID"),
	FOREIGN KEY("GameID") REFERENCES "Game"("ID")
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
	PRIMARY KEY("GameID","IsHostTeam","Index"),
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	FOREIGN KEY("GameID") REFERENCES "Game"("ID")
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
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("PlayerID") REFERENCES "Player"("ID"),
	FOREIGN KEY("GameID") REFERENCES "Game"("ID")
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
	PRIMARY KEY("ID" AUTOINCREMENT),
	FOREIGN KEY("SeasonID") REFERENCES "Season"("ID"),
	FOREIGN KEY("LocationID") REFERENCES "Location"("ID"),
	FOREIGN KEY("StatusID") REFERENCES "GameStatus"("ID"),
	FOREIGN KEY("HostTeamID") REFERENCES "Team"("ID"),
	FOREIGN KEY("VisitingTeamID") REFERENCES "Team"("ID")
);
COMMIT;
